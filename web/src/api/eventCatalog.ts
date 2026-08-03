import { api } from './client';
import type { SayfaliSonuc } from './catalog';

/**
 * Kesfet (ana sayfa) ekraninin veri katmani.
 *
 * GET /events herkese acik (AllowAnonymous). "mine" gonderilmedigi surece
 * sunucu varsayilan olarak yalnizca kamuya acik durumdaki etkinlikleri
 * doner (Published, SalesOpen, SalesClosed, Completed — bkz.
 * GetEventsQueryHandler.OnlyPublic). Ancak bu kural cagirani admin ise
 * devre disi kalir (OnlyPublic = !mine && !admin): admin girisliyken bu
 * sayfaya gelen bir kullanici, sunucudan yayinlanmamis / iptal edilmis
 * etkinlikleri de gorebilir. Kesfet herkese acik bir vitrin oldugu icin
 * `gorunurDurumdaMi` ile istemci tarafinda ikinci bir filtre uygulaniyor —
 * gercek guvenlik siniri yine sunucuda, burasi yalnizca yanlislikla admin'e
 * ozel bir taslagin herkese acik vitrinde belirmesini onluyor.
 */

export type EtkinlikDurumu =
  | 'Draft'
  | 'PendingApproval'
  | 'Published'
  | 'SalesOpen'
  | 'SalesClosed'
  | 'Completed'
  | 'Cancelled'
  | 'Suspended';

/** GET /events satiri (EventListItem — Loca.Application/Features/Events/Common/EventDtos.cs). */
export interface EtkinlikOzeti {
  id: string;
  title: string;
  categoryName: string;
  cityName: string;
  venueName: string;
  eventDateUtc: string;
  durationMinutes: number;
  status: EtkinlikDurumu;
  // Dosyayi servis eden bir uc yok (bkz. EtkinlikKarti.tsx afis yer
  // tutucusu); arayuz bu alani goruntulemek icin KULLANMIYOR, yalnizca
  // sunucu sozlesmesiyle birebir durmasi icin tutuluyor.
  posterFileId: string | null;
  minimumAge: number | null;
  // Sunucudaki DTO alan adi `MinPrice` (EventDtos.cs) — camelCase
  // serilestirmede `minPrice` olarak gelir. Kesfet gorevindeki
  // `minimumPrice` adi gercek sozlesmeyle uyusmuyordu, koda gore duzeltildi.
  minPrice: number | null;
  currency: string | null;
  sessionCount: number;
}

export interface EtkinlikListesiParametreleri {
  sayfaNo?: number;
  sayfaBoyutu?: number;
}

export async function etkinlikleriGetir(
  parametreler: EtkinlikListesiParametreleri = {},
): Promise<SayfaliSonuc<EtkinlikOzeti>> {
  const { sayfaNo = 1, sayfaBoyutu = 24 } = parametreler;

  const { data } = await api.get<SayfaliSonuc<EtkinlikOzeti>>('/events', {
    params: { pageNumber: sayfaNo, pageSize: sayfaBoyutu },
  });

  return data;
}

const GORUNUR_DURUMLAR = new Set<EtkinlikDurumu>(['Published', 'SalesOpen', 'SalesClosed']);

/**
 * "Yaklasan etkinlik" listesi: tarihi ileride VE herkese gosterilebilir
 * durumda olanlar, en yakin tarihten baslayarak siralanir.
 *
 * Sunucuda ayri bir "trend etkinlikler" ya da "sadece gelecek" ucu yok;
 * kesfet sayfasindaki her turetme (one cikanlar, mekan ozeti, en yogun
 * sehir) bu listeden besleniyor.
 */
export function yaklasanlariAyikla(etkinlikler: EtkinlikOzeti[]): EtkinlikOzeti[] {
  const simdi = Date.now();

  return etkinlikler
    .filter(
      (etkinlik) =>
        GORUNUR_DURUMLAR.has(etkinlik.status) && new Date(etkinlik.eventDateUtc).getTime() > simdi,
    )
    .sort((a, b) => new Date(a.eventDateUtc).getTime() - new Date(b.eventDateUtc).getTime());
}

export interface MekanOzetSatiri {
  venueName: string;
  cityName: string;
  yaklasanEtkinlikSayisi: number;
}

/**
 * Mekan basina yaklasan etkinlik sayisi, azalan sirada.
 *
 * Sunucuda "mekana gore etkinlik sayisi" doner bir uc yok; bu yuzden zaten
 * cekilmis olan sayfadan turetiliyor. Sonuc bu nedenle YALNIZCA o sayfadaki
 * kayitlari yansitir — katalogun tamami degil, kucuk bir kesfet listesi
 * icin yeterli bir yaklasik deger.
 */
export function mekanlaraGoreOzetle(yaklasanlar: EtkinlikOzeti[]): MekanOzetSatiri[] {
  const sayac = new Map<string, MekanOzetSatiri>();

  for (const etkinlik of yaklasanlar) {
    const mevcut = sayac.get(etkinlik.venueName);

    if (mevcut) {
      mevcut.yaklasanEtkinlikSayisi += 1;
    } else {
      sayac.set(etkinlik.venueName, {
        venueName: etkinlik.venueName,
        cityName: etkinlik.cityName,
        yaklasanEtkinlikSayisi: 1,
      });
    }
  }

  return [...sayac.values()].sort((a, b) => b.yaklasanEtkinlikSayisi - a.yaklasanEtkinlikSayisi);
}

/**
 * Yaklasan etkinlik sayisi en yuksek sehir.
 *
 * "Etrafinda neler oluyor" basligindaki sehir adi icin kullanilir. Kullanici
 * konumu YOK — bu yuzden "yakinimdaki" degil, veride en yogun olan sehir
 * gosteriliyor.
 */
export function enYogunSehir(yaklasanlar: EtkinlikOzeti[]): string | null {
  const sayac = new Map<string, number>();

  for (const etkinlik of yaklasanlar) {
    sayac.set(etkinlik.cityName, (sayac.get(etkinlik.cityName) ?? 0) + 1);
  }

  let secili: string | null = null;
  let enYuksek = 0;

  for (const [sehir, adet] of sayac) {
    if (adet > enYuksek) {
      secili = sehir;
      enYuksek = adet;
    }
  }

  return secili;
}
