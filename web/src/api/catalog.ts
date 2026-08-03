import { api } from './client';

/**
 * Etkinlik olusturmada kullanilan referans veriler.
 *
 * Sehir, mekan ve salon sistemin referans verisi; organizator bunlari
 * olusturmuyor, var olanlar arasindan seciyor. Uclarin tamami anonim
 * erisime acik oldugu icin wizard giris ekraninda bile calisir.
 */

export interface Sehir {
  id: string;
  name: string;
  plateCode?: number;
}

export interface MekanOzeti {
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

export interface OturmaPlaniOzeti {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  sectionCount: number;
}

export interface SayfaliSonuc<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
}

export async function sehirleriGetir(): Promise<Sehir[]> {
  const { data } = await api.get<Sehir[]>('/cities');
  return data;
}

export async function mekanlariGetir(sehirId: string): Promise<MekanOzeti[]> {
  const { data } = await api.get<SayfaliSonuc<MekanOzeti>>('/venues', {
    // Wizard'da sayfalama yok: bir sehirdeki mekan sayisi acilir listeye
    // sigacak kadar az. Sinir yine de veriliyor, sinirsiz istek atilmasin.
    params: { cityId: sehirId, pageSize: 100 },
  });

  return data.items;
}

export async function salonlariGetir(mekanId: string): Promise<Salon[]> {
  const { data } = await api.get<Salon[]>(`/venues/${mekanId}/halls`);
  return data;
}

export async function planlariGetir(salonId: string): Promise<OturmaPlaniOzeti[]> {
  const { data } = await api.get<OturmaPlaniOzeti[]>(`/halls/${salonId}/seat-layouts`);
  return data;
}
