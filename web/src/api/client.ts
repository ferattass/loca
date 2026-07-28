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

/** Sunucudan gelen hatayi kullaniciya gosterilecek tek satira cevirir. */
export function hataMesaji(hata: unknown, varsayilan = 'Beklenmeyen bir hata olustu.'): string {
  if (!axios.isAxiosError(hata)) return varsayilan;

  const govde = hata.response?.data as ProblemDetails | undefined;

  if (govde?.errors) {
    const ilkAlan = Object.values(govde.errors)[0];
    if (ilkAlan?.length) return ilkAlan[0];
  }

  return govde?.detail ?? varsayilan;
}
