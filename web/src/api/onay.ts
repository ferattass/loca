import { api } from './client';
import type { SayfaliSonuc } from './admin';
import type { EtkinlikOzeti } from './eventCatalog';

export type BelgeTuru = 'VenueContract' | 'Permit' | 'Other';

export const BELGE_TURU_METNI: Record<BelgeTuru, string> = {
  VenueContract: 'Sahne sözleşmesi',
  Permit: 'Resmi izin',
  Other: 'Diğer',
};

export interface EtkinlikBelgesi {
  id: string;
  kind: BelgeTuru;
  originalFileName: string;
  /** Icerik `dosyaAdresi(uploadedFileId)` ile okunuyor. */
  uploadedFileId: string;
  sizeInBytes: number;
  note: string | null;
  uploadedAt: string;
}

/**
 * Onay bekleyen etkinlikler.
 *
 * Ayri bir uc DEGIL: etkinlik listesinin durum suzgeci. Onay ekibi
 * (moderator ve admin) tum durumlari gorebiliyor, digerleri ayni suzgeci
 * gonderse de bos liste aliyor — gorunurluk kurali tek yerde.
 */
export async function onayBekleyenleriGetir(): Promise<SayfaliSonuc<EtkinlikOzeti>> {
  const { data } = await api.get<SayfaliSonuc<EtkinlikOzeti>>('/events', {
    params: { status: 'PendingApproval', pageSize: 50 },
  });

  return data;
}

export async function etkinlikBelgeleriniGetir(
  etkinlikId: string,
): Promise<EtkinlikBelgesi[]> {
  const { data } = await api.get<EtkinlikBelgesi[]>(`/events/${etkinlikId}/documents`);
  return data;
}

/** Etkinligi yayina alir; satilabilir koltuklar da burada uretiliyor. */
export async function etkinligiYayinla(etkinlikId: string): Promise<void> {
  await api.post(`/events/${etkinlikId}/publish`);
}

/**
 * Belge dosyasini yukler ve kimligini doner.
 *
 * Gorsel ucundan AYRI uc: burada PDF de gecerli. Tek uc olup turu istek
 * govdesinden alsaydi afis alanina PDF yuklenebilirdi.
 */
export async function belgeYukle(dosya: File): Promise<string> {
  const govde = new FormData();
  govde.append('dosya', dosya);

  const { data } = await api.post<{ id: string }>('/files/belge', govde);

  return data.id;
}

export async function belgeBagla(
  etkinlikId: string,
  dosyaId: string,
  tur: BelgeTuru,
  not: string | null,
): Promise<string> {
  const { data } = await api.post<string>(`/events/${etkinlikId}/documents`, {
    uploadedFileId: dosyaId,
    kind: tur,
    note: not,
  });

  return data;
}

export async function belgeSil(etkinlikId: string, belgeId: string): Promise<void> {
  await api.delete(`/events/${etkinlikId}/documents/${belgeId}`);
}
