import { api } from './client';

export interface EtkinlikKategorisi {
  id: string;
  name: string;
  slug: string;
}

export interface EtkinlikOlusturIstegi {
  categoryId: string;
  title: string;
  description: string;
  cancellationPolicy: string;
  cityId: string;
  venueId: string;
  hallId: string;
  eventDateUtc: string;
  durationMinutes: number;
  salesStartsAtUtc: string;
  salesEndsAtUtc: string;
  minimumAge: number | null;
}

export interface OturumOlusturIstegi {
  hallId: string;
  seatLayoutId: string;
  startsAtUtc: string;
  endsAtUtc: string;
  salesStartsAtUtc: string;
  salesEndsAtUtc: string;
}

export interface BiletTuruOlusturIstegi {
  name: string;
  price: number;
  currency: string;
  quota: number;
  salesStartsAtUtc: string;
  salesEndsAtUtc: string;
  requiresVerification: boolean;
  seatSectionId: string | null;
}

export async function kategorileriGetir(): Promise<EtkinlikKategorisi[]> {
  const { data } = await api.get<EtkinlikKategorisi[]>('/event-categories');
  return data;
}

/** Etkinligi taslak olarak olusturur ve kimligini doner. */
export async function etkinlikOlustur(istek: EtkinlikOlusturIstegi): Promise<string> {
  const { data } = await api.post<string>('/events', istek);
  return data;
}

export async function oturumEkle(
  etkinlikId: string,
  istek: OturumOlusturIstegi,
): Promise<string> {
  const { data } = await api.post<string>(`/events/${etkinlikId}/sessions`, istek);
  return data;
}

export async function biletTuruEkle(
  etkinlikId: string,
  istek: BiletTuruOlusturIstegi,
): Promise<string> {
  const { data } = await api.post<string>(`/events/${etkinlikId}/ticket-types`, istek);
  return data;
}

/**
 * Gorsel yukler ve dosya kimligini doner.
 *
 * Tur dogrulamasi sunucuda, uzantiya degil dosyanin ilk baytlarina bakilarak
 * yapiliyor; buradaki `accept` yalnizca dosya seciciyi daraltir, koruma degil.
 */
export async function gorselYukle(dosya: File): Promise<string> {
  const govde = new FormData();
  govde.append('dosya', dosya);

  const { data } = await api.post<{ id: string }>('/files', govde, {
    // Content-Type ELLE VERILMIYOR: FormData'nin sinir (boundary) degerini
    // tarayici uretiyor, elle yazilan bir baslik onu ezer ve sunucu govdeyi
    // cozemez.
    headers: { 'Content-Type': undefined },
  });

  return data.id;
}

export async function afisBagla(etkinlikId: string, dosyaId: string | null): Promise<void> {
  await api.patch(`/events/${etkinlikId}/poster`, { posterFileId: dosyaId });
}

export async function onayaGonder(etkinlikId: string): Promise<void> {
  await api.post(`/events/${etkinlikId}/submit-for-approval`);
}
