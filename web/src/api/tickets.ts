import { api } from './client';

export type BiletDurumu = 'Valid' | 'Used' | 'Cancelled' | 'Refunded';

export interface Bilet {
  id: string;
  reservationId: string;
  eventId: string;
  eventSessionId: string;
  ticketNumber: string;
  qrCode: string;
  eventTitle: string;
  venueName: string;
  hallName: string;
  seatLabel: string;
  ticketTypeName: string;
  eventStartsAtUtc: string;
  price: number;
  currency: string;
  status: BiletDurumu;
  issuedAtUtc: string;
}

/**
 * Kullanicinin biletleri.
 *
 * Sunucu yaklasan etkinlikleri basa aliyor; istemci yeniden siralamiyor.
 * Iki tarafta iki farkli siralama olsaydi "en ustteki bilet" ifadesi
 * ekranla API arasinda farkli seyleri gosterirdi.
 */
export async function biletlerimGetir(rezervasyonId?: string): Promise<Bilet[]> {
  const { data } = await api.get<Bilet[]>('/tickets', {
    params: rezervasyonId ? { reservationId: rezervasyonId } : undefined,
  });

  return data;
}

export type OkutmaSonucu = 'Admitted' | 'AlreadyUsed' | 'NotValid';

export interface BiletOkutmaSonucu {
  admitted: boolean;
  verdict: OkutmaSonucu;
  /** Okutma sonrasindaki bilet durumu; ekran metni buna gore secilir. */
  status: BiletDurumu;
  /** Sunucunun kayit ozeti. Ekranda gosterilmez, yalnizca son care. */
  message: string;
  ticketId: string;
  ticketNumber: string;
  eventTitle: string;
  seatLabel: string;
  ticketTypeName: string;
  eventStartsAtUtc: string;
  /** Reddedilen okutmada biletin DAHA ONCE kullanildigi an. */
  checkedInAtUtc: string | null;
}

/**
 * Kapida QR okutur.
 *
 * Reddedilen bilet de basarili cevap doner; karar `verdict` alanina gore
 * verilir. Hata yolu yalnizca kod hicbir bilete karsilik gelmediginde
 * (404) veya yetki yoksa (403) calisir.
 */
export async function biletOkut(qrKodu: string): Promise<BiletOkutmaSonucu> {
  const { data } = await api.post<BiletOkutmaSonucu>('/tickets/check-in', { qrCode: qrKodu });

  return data;
}
