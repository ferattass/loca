import { api } from './client';

export type RezervasyonDurumu = 'Pending' | 'Confirmed' | 'Cancelled' | 'Expired';

export interface RezervasyonKoltugu {
  eventSeatId: string;
  seatId: string;
  sectionName: string;
  rowLabel: string;
  seatNumber: number;
  ticketTypeName: string;
  price: number;
  currency: string;
}

export interface Rezervasyon {
  id: string;
  eventSessionId: string;
  eventId: string;
  eventTitle: string;
  sessionStartsAtUtc: string;
  venueName: string;
  hallName: string;
  status: RezervasyonDurumu;
  expiresAtUtc: string;
  /**
   * Kilidin bitmesine kalan saniye — SUNUCU saatiyle hesaplanmis.
   *
   * Istemci `expiresAtUtc` ile kendi saatini karsilastirsaydi, saati geri
   * alinmis bir makinede sayac hic bitmez ve kullanici suresi coktan dolmus
   * bir rezervasyonu odemeye calisirdi. Geri sayim bu sayidan baslatilir.
   */
  remainingSeconds: number;
  extensionUsed: boolean;
  totalAmount: number;
  currency: string;
  createdAt: string;
  seats: RezervasyonKoltugu[];
}

export interface RezervasyonOzeti {
  id: string;
  eventSessionId: string;
  eventId: string;
  eventTitle: string;
  sessionStartsAtUtc: string;
  venueName: string;
  status: RezervasyonDurumu;
  expiresAtUtc: string;
  remainingSeconds: number;
  seatCount: number;
  totalAmount: number;
  currency: string;
  createdAt: string;
}

/**
 * Secilen koltuklari kilitler ve rezervasyon acar.
 *
 * `idempotencyKey` cagiran tarafindan uretilir ve AYNI DENEME boyunca
 * degismez: ag koptugunda veya kullanici iki kez tikladiginda sunucu ayni
 * anahtari gorup yeni rezervasyon acmaz, ilkinin sonucunu doner. Her
 * istekte yeni anahtar uretilseydi tekrar gonderilen istek ikinci bir
 * rezervasyon acardi.
 *
 * Toplam tutar GONDERILMEZ; sunucu koltuklarin kendi fiyatlarindan hesaplar.
 */
export async function rezervasyonOlustur(
  eventSessionId: string,
  eventSeatIds: string[],
  idempotencyKey: string,
): Promise<Rezervasyon> {
  const { data } = await api.post<Rezervasyon>(
    '/reservations',
    { eventSessionId, eventSeatIds },
    { headers: { 'Idempotency-Key': idempotencyKey } },
  );

  return data;
}

export async function rezervasyonGetir(id: string): Promise<Rezervasyon> {
  const { data } = await api.get<Rezervasyon>(`/reservations/${id}`);
  return data;
}

export async function rezervasyonlarimGetir(): Promise<RezervasyonOzeti[]> {
  // Kullanici kimligi yolda tasinmiyor, token'dan okunuyor.
  const { data } = await api.get<RezervasyonOzeti[]>('/users/me/reservations');
  return data;
}

/** Kilit suresini bir kez uzatir. Ikinci deneme 409 doner. */
export async function rezervasyonUzat(id: string): Promise<Rezervasyon> {
  const { data } = await api.post<Rezervasyon>(`/reservations/${id}/extend`);
  return data;
}

export async function rezervasyonIptalEt(id: string): Promise<void> {
  await api.post(`/reservations/${id}/cancel`);
}
