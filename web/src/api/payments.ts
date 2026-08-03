import { api } from './client';

export type OdemeDurumu = 'Pending' | 'Succeeded' | 'Failed' | 'Refunded' | 'Cancelled';

export interface OdemeDenemesi {
  type: string;
  occurredAtUtc: string;
  message: string;
}

export interface OdemeDetayi {
  id: string;
  reservationId: string;
  status: OdemeDurumu;
  provider: string;
  providerReference: string | null;
  /**
   * Gercek saglayicida doludur; kullanici bu adrese yonlendirilir ve odeme
   * orada tamamlanip sunucuya webhook ile doner.
   *
   * Taklit saglayicida bu alan BOSTUR — o zaman tamamlama istemciden
   * dogrudan `/payments/{id}/complete` ile tetiklenir.
   */
  redirectUrl: string | null;
  amount: number;
  currency: string;
  completedAtUtc: string | null;
  failureReason: string | null;
  createdAt: string;
  attempts: OdemeDenemesi[];
}

export interface Bilet {
  id: string;
  ticketNumber: string;
  qrCode: string;
  eventTitle: string;
  seatLabel: string;
  ticketTypeName: string;
  eventStartsAtUtc: string;
  price: number;
  currency: string;
}

export interface OdemeTamamlamaSonucu {
  paymentId: string;
  status: OdemeDurumu;
  reservationId: string;
  stateChanged: boolean;
  tickets: Bilet[];
}

/**
 * Bir rezervasyon icin odeme baslatir.
 *
 * `idempotencyKey` cagiran tarafindan uretilir ve AYNI DENEME boyunca
 * degismez — rezervasyon acmadaki ile ayni gerekce: ag koptugunda veya
 * kullanici "Odemeyi baslat"a iki kez basinca sunucu ikinci bir odeme kaydi
 * acmaz, ilkinin sonucunu doner.
 */
export async function odemeBaslat(
  reservationId: string,
  idempotencyKey: string,
): Promise<OdemeDetayi> {
  const { data } = await api.post<OdemeDetayi>(
    '/payments',
    { reservationId },
    { headers: { 'Idempotency-Key': idempotencyKey } },
  );

  return data;
}

export async function odemeGetir(id: string): Promise<OdemeDetayi> {
  const { data } = await api.get<OdemeDetayi>(`/payments/${id}`);
  return data;
}

/**
 * Taklit saglayicida odemeyi tamamlar ve uretilen biletleri doner.
 *
 * Biletler yalnizca BU cevapta gelir; `GET /payments/{id}` biletleri tekrar
 * dondurmez. Bu yuzden basarili tamamlamada donen `tickets` listesi ekranda
 * ayrica saklanmali, sonradan yeniden sorgulanamaz.
 */
export async function odemeTamamla(id: string): Promise<OdemeTamamlamaSonucu> {
  const { data } = await api.post<OdemeTamamlamaSonucu>(`/payments/${id}/complete`);
  return data;
}
