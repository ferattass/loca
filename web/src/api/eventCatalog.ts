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

/** GET /events/{id} icindeki oturum (EventSessionItem). */
export interface OturumOzeti {
  id: string;
  startsAtUtc: string;
  endsAtUtc: string;
  salesStartsAtUtc: string;
  salesEndsAtUtc: string;
  hallId: string;
  hallName: string;
  seatLayoutId: string;
  seatLayoutName: string;
  status: 'Scheduled' | 'Cancelled' | 'Completed';
  seatsGenerated: boolean;
}

/** GET /events/{id} icindeki bilet turu (TicketTypeItem). */
export interface BiletTuruOzeti {
  id: string;
  name: string;
  price: number;
  currency: string;
  quota: number;
  salesStartsAtUtc: string;
  salesEndsAtUtc: string;
  requiresVerification: boolean;
  seatSectionId: string | null;
  seatSectionName: string | null;
  isActive: boolean;
}

/** GET /events/{id} (EventDetail). */
export interface EtkinlikDetayi {
  id: string;
  title: string;
  description: string;
  cancellationPolicy: string;
  categoryId: string;
  categoryName: string;
  organizerId: string;
  organizerName: string;
  cityId: string;
  cityName: string;
  venueId: string;
  venueName: string;
  hallId: string;
  hallName: string;
  eventDateUtc: string;
  durationMinutes: number;
  salesStartsAtUtc: string;
  salesEndsAtUtc: string;
  status: EtkinlikDurumu;
  posterFileId: string | null;
  minimumAge: number | null;
  publishedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  sessions: OturumOzeti[];
  ticketTypes: BiletTuruOzeti[];
}

export async function etkinlikDetayiGetir(id: string): Promise<EtkinlikDetayi> {
  const { data } = await api.get<EtkinlikDetayi>(`/events/${id}`);
  return data;
}

export interface EtkinlikListesiParametreleri {
  sayfaNo?: number;
  sayfaBoyutu?: number;
  kategoriId?: string;
  sehirId?: string;
  arama?: string;
  /** ISO. Bu andan itibaren baslayanlar. */
  baslangicUtc?: string;
  /** ISO. Bu andan once baslayanlar. */
  bitisUtc?: string;
}

/**
 * Bugunun (yerel gunun) UTC siniri.
 *
 * <b>Yerel gune gore hesaplaniyor, UTC gunune gore degil.</b> Kullanici
 * "bugun" derken kendi takvimindeki gunu kastediyor; Turkiye UTC+3 oldugu
 * icin UTC gun sinirlari kullanilsaydi gece 00:00-03:00 arasindaki
 * etkinlikler "yarin" grubuna duserdi.
 */
export function bugununSiniri(): { baslangicUtc: string; bitisUtc: string } {
  const simdi = new Date();
  const baslangic = new Date(simdi.getFullYear(), simdi.getMonth(), simdi.getDate());
  const bitis = new Date(baslangic);
  bitis.setDate(bitis.getDate() + 1);

  return { baslangicUtc: baslangic.toISOString(), bitisUtc: bitis.toISOString() };
}

export async function etkinlikleriGetir(
  parametreler: EtkinlikListesiParametreleri = {},
): Promise<SayfaliSonuc<EtkinlikOzeti>> {
  const { sayfaNo = 1, sayfaBoyutu = 24, kategoriId, sehirId, arama, baslangicUtc, bitisUtc } =
    parametreler;

  const { data } = await api.get<SayfaliSonuc<EtkinlikOzeti>>('/events', {
    // Bos degerler GONDERILMIYOR: sunucu bos metni "bos olana gore
    // suz" diye yorumlamasa da sorgu dizesi sisiyor ve react-query'nin
    // onbellek anahtari her tus vurusunda degisiyor.
    params: {
      pageNumber: sayfaNo,
      pageSize: sayfaBoyutu,
      categoryId: kategoriId,
      cityId: sehirId,
      search: arama?.trim() || undefined,
      fromUtc: baslangicUtc,
      toUtc: bitisUtc,
    },
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
