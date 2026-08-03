import { api } from './client';
import type { MekanOzeti, SayfaliSonuc } from './catalog';

/**
 * Mekan ve salon yonetimi uclari (yonetici paneli).
 *
 * Butun uclar AdminOnly. Silme istekleri sunucuda bagli kayit kontrolu
 * yapiyor (mekanda salon varsa ya da salonda oturma plani varsa 409 doner);
 * burada ayrica bir on kontrol yok, sonuc sunucudan geldigi gibi gosterilir.
 *
 * Liste ogesi ve sayfalama sekli katalog ucundakiyle (`/venues`) ayni oldugu
 * icin `MekanOzeti` ve `SayfaliSonuc` buradan degil `catalog.ts`'den
 * kullaniliyor; ayni sekli iki yerde tanimlamak ileride biri degisince
 * digerinin unutulmasina yol acardi.
 */

export interface MekanSalonu {
  id: string;
  name: string;
  capacity: number;
  isActive: boolean;
}

export interface MekanDetayi {
  id: string;
  cityId: string;
  cityName: string;
  name: string;
  address: string;
  description: string | null;
  phoneNumber: string | null;
  imageFileId: string | null;
  isActive: boolean;
  halls: MekanSalonu[];
}

export interface MekanFormVerisi {
  cityId: string;
  name: string;
  address: string;
  description: string;
  phoneNumber: string;
}

export interface SalonFormVerisi {
  name: string;
  capacity: number;
}

export interface MekanListesiParametreleri {
  sehirId: string;
  arama: string;
  sayfaNo: number;
  sayfaBoyutu: number;
}

export async function mekanListesiGetir(
  parametreler: MekanListesiParametreleri,
): Promise<SayfaliSonuc<MekanOzeti>> {
  const { sehirId, arama, sayfaNo, sayfaBoyutu } = parametreler;

  const { data } = await api.get<SayfaliSonuc<MekanOzeti>>('/venues', {
    // Bos deger sunucuya gonderilmiyor: cityId="" filtre uygulanmasi
    // gerektigi anlamina degil, filtre olmadigi anlamina gelir.
    params: {
      cityId: sehirId || undefined,
      arama: arama || undefined,
      pageNumber: sayfaNo,
      pageSize: sayfaBoyutu,
    },
  });

  return data;
}

export async function mekanDetayiGetir(id: string): Promise<MekanDetayi> {
  const { data } = await api.get<MekanDetayi>(`/venues/${id}`);
  return data;
}

/** Mekani olusturur ve kimligini doner. */
export async function mekanOlustur(veri: MekanFormVerisi): Promise<string> {
  const { data } = await api.post<string>('/venues', veri);
  return data;
}

// Sehir degistirilemez: mekan bir kere bir sehre baglaniyor, tasinmiyor.
// Bu yuzden guncelleme govdesi olusturma govdesinden `cityId` eksik.
export async function mekanGuncelle(
  id: string,
  veri: Omit<MekanFormVerisi, 'cityId'>,
): Promise<void> {
  await api.put(`/venues/${id}`, veri);
}

/** Baglii salonu varsa sunucu 409 doner. */
export async function mekanSil(id: string): Promise<void> {
  await api.delete(`/venues/${id}`);
}

/** Salonu olusturur ve kimligini doner. */
export async function salonEkle(mekanId: string, veri: SalonFormVerisi): Promise<string> {
  const { data } = await api.post<string>(`/venues/${mekanId}/halls`, veri);
  return data;
}

// Su anki ekranda salon duzenleme formu yok (istenen davranista yalnizca
// ekleme ve silme var); uc yine de sozlesme tam karsilansin diye burada
// duruyor, ileride ihtiyac oldugunda arayuze baglanabilir.
export async function salonGuncelle(id: string, veri: SalonFormVerisi): Promise<void> {
  await api.put(`/halls/${id}`, veri);
}

/** Baglii oturma plani varsa sunucu 409 doner. */
export async function salonSil(id: string): Promise<void> {
  await api.delete(`/halls/${id}`);
}
