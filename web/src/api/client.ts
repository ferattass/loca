import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';

import { useAuthStore } from '../stores/authStore';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api/v1';

export const api = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
});

/** Bir istegin yenileme sonrasi yalnizca bir kez tekrar edilmesini saglar. */
type RetriableRequest = InternalAxiosRequestConfig & { _yenilendi?: boolean };

/**
 * Sunucunun donduugu RFC 7807 hata govdesi.
 * `code` alani bizim ekledigimiz makine okunur hata kodu (orn. Auth.InvalidCredentials).
 */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

/**
 * Ayni anda yurutulen tek yenileme islemi.
 *
 * Sayfa acilisinda alti istek birden 401 alirsa alti ayri yenileme cagrisi
 * gider. Sunucu her yenilemede token'i degistirdigi (rotation) icin ilki
 * basarili olur, kalan besi iptal edilmis token gonderir ve sunucu bunu
 * "token calinmis olabilir" sayarak kullanicinin butun oturumlarini kapatir.
 * Yani korumanin kendisi, dikkatsiz bir istemci yuzunden kullaniciyi disari
 * atar. Bu yuzden ilk 401 yenilemeyi baslatir, digerleri ayni sozu bekler.
 */
let yenilemeIslemi: Promise<string> | null = null;

async function accessTokenYenile(): Promise<string> {
  const { refreshToken, oturumAc, oturumKapat } = useAuthStore.getState();

  if (!refreshToken) {
    oturumKapat();
    throw new Error('Yenileme icin token yok.');
  }

  try {
    // Araya interceptor girmesin diye ham axios kullanilir; aksi hâlde bu
    // istegin kendisi 401 alirsa sonsuz donguye girilir.
    const { data } = await axios.post(`${BASE_URL}/auth/refresh`, { refreshToken });
    oturumAc(data);
    return data.accessToken;
  } catch (hata) {
    oturumKapat();
    throw hata;
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const istek = error.config as RetriableRequest | undefined;

    // Yalnizca "token gecersiz" durumunda yenileme denenir. Yetki hatasi (403)
    // yenilemeyle cozulmez; kullanicinin rolu yetmiyordur.
    if (error.response?.status !== 401 || !istek || istek._yenilendi) {
      throw error;
    }

    istek._yenilendi = true;

    yenilemeIslemi ??= accessTokenYenile().finally(() => {
      yenilemeIslemi = null;
    });

    try {
      const yeniToken = await yenilemeIslemi;
      istek.headers.Authorization = `Bearer ${yeniToken}`;
      return await api(istek);
    } catch {
      // Yenileme de basarisizsa oturum gercekten bitmistir.
      throw error;
    }
  },
);

/**
 * Sunucunun makine okunur hata kodu (orn. `Reservation.SeatNotAvailable`).
 *
 * Arayuz kararlarini MESAJ metnine degil bu koda gore vermeli: mesaj metni
 * degistiginde veya cevirisi eklendiginde koda bakan mantik kirilmaz.
 * Ozellikle rezervasyon akisinda onemli — ayni 409 iki farkli sebepten
 * gelebiliyor ve ikisinde yapilacak sey farkli.
 */
export function hataKodu(hata: unknown): string | undefined {
  if (!axios.isAxiosError(hata)) return undefined;

  return (hata.response?.data as ProblemDetails | undefined)?.code;
}

/**
 * Yanitin HTTP durum kodu; istek sunucuya hic ulasmadiysa <c>undefined</c>.
 *
 * `hataKodu` yetmediginde gerekiyor: kimlik hatalarinin govdesinde uygulamaya
 * ozel bir `code` bulunmuyor ve 401'i ayirt etmenin tek yolu durum kodu.
 * Ag kopmasiyla 401'i ayirmak da onemli — ilkinde tekrar denemek anlamli,
 * ikincisinde kullaniciyi girise gondermek gerekiyor.
 */
export function durumKodu(hata: unknown): number | undefined {
  if (!axios.isAxiosError(hata)) return undefined;

  return hata.response?.status;
}

/**
 * Sunucudan gelen alan bazli dogrulama hatalari.
 *
 * Tek satirlik ozet yerine tam liste gerektiginde kullanilir: bir formda
 * dort alan birden hataliysa kullaniciya yalnizca ilkini gostermek, onu
 * hatalari tek tek keşfetmeye zorlar.
 */
export function dogrulamaHatalari(hata: unknown): string[] {
  if (!axios.isAxiosError(hata)) return [];

  const govde = hata.response?.data as ProblemDetails | undefined;

  return Object.values(govde?.errors ?? {}).flat();
}

/**
 * Ayni hatalar, alan adina gore gruplanmis hâlde.
 *
 * Liste hâli bir kutuya toplu yaziliyor; bu hâl her mesaji KENDI alaninin
 * altina koymak icin. Ikisi de gerekiyor: kisa formda hatanin hangi alana
 * ait oldugu listeden okunabiliyor ama uzun bir formda kullanici hatayi
 * alanla eslestirmek zorunda kaliyor.
 *
 * Anahtarlar sunucudan geldigi gibi: FluentValidation ozellik adini
 * kullaniyor, yani `Host`, `FromAddress` — istemcideki alan adlariyla
 * karistirmamak icin buyuk harfle basliyorlar.
 */
export function alanHatalari(hata: unknown): Record<string, string[]> {
  if (!axios.isAxiosError(hata)) return {};

  return (hata.response?.data as ProblemDetails | undefined)?.errors ?? {};
}

/** Sunucudan gelen hatayi kullaniciya gosterilecek tek satira cevirir. */
export function hataMesaji(hata: unknown, varsayilan = 'Beklenmeyen bir hata olustu.'): string {
  // ISTEK HIC GITMEMIS OLABILIR.
  // Once yalnizca axios hatalari isleniyordu; istemcide olusan bir hata
  // (gecersiz tarih, ag kopmasi) varsayilan metne dusuyordu ve kullanici
  // "Etkinlik olusturulamadi." gibi hicbir sey soylemeyen bir cumle
  // goruyordu. Gercek sebep artik gosteriliyor.
  if (!axios.isAxiosError(hata)) {
    return hata instanceof Error && hata.message ? hata.message : varsayilan;
  }

  const govde = hata.response?.data as ProblemDetails | undefined;

  if (govde?.errors) {
    const ilkAlan = Object.values(govde.errors)[0];
    if (ilkAlan?.length) return ilkAlan[0];
  }

  // Sunucuya ulasilamadiysa response yok; axios'un kendi mesaji daha bilgili.
  if (!hata.response) return hata.message || varsayilan;

  return govde?.detail ?? varsayilan;
}

/**
 * Yuklenen dosyanin adresi.
 *
 * Adres API tabanindan turetiliyor, sabit yazilmiyor: konteynerde ve
 * canlida API baska bir kokten servis ediliyor ve sabit bir "/uploads"
 * yolu orada bos donerdi.
 */
export function dosyaAdresi(dosyaId: string | null | undefined): string | null {
  if (!dosyaId) return null;
  return `${BASE_URL}/files/${dosyaId}`;
}
