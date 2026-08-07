import { api } from './client';

export type OdemeDurumu = 'Pending' | 'Succeeded' | 'Failed' | 'Refunded' | 'Cancelled';

export interface OdemeDenemesi {
  type: string;
  occurredAtUtc: string;
  message: string;
}

export type OdemeYontemi = 'Card' | 'BankTransfer';

export interface OdemeDetayi {
  id: string;
  reservationId: string;
  status: OdemeDurumu;
  provider: string;
  method: OdemeYontemi;
  /**
   * Kartta saglayicinin islem kimligi; havalede kullanicinin havale
   * aciklamasina yazacagi kod (LOCA-XXXXXXXX). Yonetici gelen ekstreyi
   * bu kodla esliyor.
   */
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
  method: OdemeYontemi = 'Card',
): Promise<OdemeDetayi> {
  const { data } = await api.post<OdemeDetayi>(
    '/payments',
    { reservationId, method },
    { headers: { 'Idempotency-Key': idempotencyKey } },
  );

  return data;
}

/** Havale acikken banka bilgileri; kapaliyken `null`. */
export interface HavaleTalimati {
  bankName: string;
  accountName: string;
  iban: string;
  deadlineHours: number;
}

export interface OdemeYontemleri {
  cardEnabled: boolean;
  bankTransferEnabled: boolean;
  instructions: HavaleTalimati | null;
}

/**
 * Su an acik olan odeme yontemleri.
 *
 * Sunucuya SORULUYOR, istemcide sabit yazilmiyor: panelden havale
 * kapatildiginda dugme kendiliginden kayboluyor. Sabit yazilsaydi dugme
 * durmaya devam eder, basan kullanici 409 alirdi.
 */
export async function odemeYontemleriGetir(): Promise<OdemeYontemleri> {
  const { data } = await api.get<OdemeYontemleri>('/payments/methods');
  return data;
}

/** Yonetici havalenin hesaba gectigini onaylar; biletler uretilir. */
export async function havaleOnayla(
  odemeId: string,
  referans?: string,
): Promise<OdemeTamamlamaSonucu> {
  const { data } = await api.post<OdemeTamamlamaSonucu>(
    `/payments/${odemeId}/bank-transfer/confirm`,
    { reference: referans?.trim() || null },
  );

  return data;
}

/** Yonetici havalenin gelmedigini bildirir; koltuklar hemen satisa doner. */
export async function havaleReddet(
  odemeId: string,
  sebep: string,
): Promise<OdemeTamamlamaSonucu> {
  const { data } = await api.post<OdemeTamamlamaSonucu>(
    `/payments/${odemeId}/bank-transfer/reject`,
    { reason: sebep },
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
