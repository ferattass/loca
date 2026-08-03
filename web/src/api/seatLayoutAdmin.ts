import { api } from './client';

/**
 * Oturma plani yonetim ekraninin API katmani.
 *
 * catalog.ts'deki uclar organizator sihirbazi icin sehir->mekan->salon
 * zincirini takip eder (sehir secilmeden mekan listelenmez). Buradaki
 * yonetici ekrani sehre bagli degil: mekan listesi dogrudan /venues'tan,
 * sayfalama siniriyla (pageSize=100) gelir. Iki akis ayni HTTP ucunu
 * kullansa bile farkli sorguya cevap verdigi icin fonksiyonlar burada
 * ayri tutuluyor; catalog.ts'e city parametresi opsiyonel yapip
 * karistirmak, organizator akisinin varsayilanlarini bozma riski tasirdi.
 */

export interface Mekan {
  id: string;
  name: string;
  cityName: string;
  isActive: boolean;
  hallCount: number;
}

export interface Salon {
  id: string;
  venueId: string;
  venueName: string;
  name: string;
  capacity: number;
  isActive: boolean;
}

export interface PlanOzeti {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  sectionCount: number;
}

export interface PlanOlusturIstek {
  hallId: string;
  name: string;
  description: string | null;
}

export interface BolumEkleIstek {
  name: string;
  displayOrder: number;
}

export interface KoltukUretIstek {
  seatSectionId: string;
  rowLabels: string[];
  seatsPerRow: number;
  horizontalSpacing: number;
  verticalSpacing: number;
  originY: number;
}

export interface KoltukUretSonuc {
  seatLayoutId: string;
  seatSectionId: string;
  generatedCount: number;
  totalSeatCount: number;
}

interface SayfaliSonuc<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export async function mekanlariGetir(): Promise<Mekan[]> {
  const { data } = await api.get<SayfaliSonuc<Mekan>>('/venues', {
    params: { pageSize: 100 },
  });

  return data.items;
}

export async function salonlariGetir(mekanId: string): Promise<Salon[]> {
  const { data } = await api.get<Salon[]>(`/venues/${mekanId}/halls`);
  return data;
}

export async function planlariGetir(salonId: string): Promise<PlanOzeti[]> {
  const { data } = await api.get<PlanOzeti[]>(`/halls/${salonId}/seat-layouts`);
  return data;
}

export async function planOlustur(istek: PlanOlusturIstek): Promise<string> {
  const { data } = await api.post<string>('/seat-layouts', istek);
  return data;
}

export async function planSil(planId: string): Promise<void> {
  await api.delete(`/seat-layouts/${planId}`);
}

export async function bolumEkle(planId: string, istek: BolumEkleIstek): Promise<string> {
  const { data } = await api.post<string>(`/seat-layouts/${planId}/sections`, istek);
  return data;
}

/**
 * Bir bolume toplu koltuk uretir.
 *
 * Sunucu 409 dondurebilir: uretim salon kapasitesini asarsa veya bolumde
 * zaten koltuk varsa. Arayuz bu iki durumu da mumkun oldugunca formda
 * onceden tahmin edip kullaniciyi uyarir, ama nihai kural sunucudadir —
 * bu yuzden hata metni oldugu gibi kullaniciya gosterilir.
 */
export async function koltukUret(
  planId: string,
  istek: KoltukUretIstek,
): Promise<KoltukUretSonuc> {
  const { data } = await api.post<KoltukUretSonuc>(
    `/seat-layouts/${planId}/generate-seats`,
    istek,
  );

  return data;
}
