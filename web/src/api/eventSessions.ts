import { api } from './client';

/** Sunucunun bildirdigi koltuk durumu. `Selected` yok — o yalnizca arayuzde. */
export type KoltukDurumu = 'Available' | 'Locked' | 'Reserved' | 'Sold' | 'Disabled';

export interface MusaitlikKoltugu {
  eventSeatId: string;
  seatId: string;
  rowLabel: string;
  seatNumber: number;
  positionX: number;
  positionY: number;
  status: KoltukDurumu;
  lockedUntilUtc: string | null;
  /**
   * Kilit bu kullaniciya mi ait.
   *
   * Kilidi kimin tuttugu (`LockedByUserId`) sunucudan HIC gelmiyor: baska bir
   * kullanicinin kimligi disari verilmez. Arayuzun ihtiyaci olan tek bilgi bu.
   */
  isLockedByMe: boolean;
  price: number;
  currency: string;
  ticketTypeName: string;
}

export interface MusaitlikBolumu {
  id: string;
  name: string;
  displayOrder: number;
  seats: MusaitlikKoltugu[];
}

export interface KoltukMusaitligi {
  eventSessionId: string;
  seatLayoutId: string;
  /**
   * Etkinlik bilgileri ayni yanitta geliyor.
   *
   * Ayri bir istek atilsaydi koltuk plani ile baslik farkli anlarda gelir,
   * kullanici bir an "hangi etkinlik" bilmeden plana bakardi. Veriler zaten
   * sunucudaki ayni sorgunun join'inde.
   */
  eventId: string;
  eventTitle: string;
  venueName: string;
  hallName: string;
  startsAtUtc: string;
  salesEndsAtUtc: string;
  generatedAtUtc: string;
  sections: MusaitlikBolumu[];
}

/**
 * Oturumun koltuk durumlari.
 *
 * Anonim erisime acik: kullanici giris yapmadan da salonun doluluk durumunu
 * gorebilmeli. Giris yapilmissa kendi kilitleri `isLockedByMe` ile isaretlenir.
 */
export async function koltukMusaitligiGetir(oturumId: string): Promise<KoltukMusaitligi> {
  const { data } = await api.get<KoltukMusaitligi>(
    `/event-sessions/${oturumId}/seat-availability`,
  );

  return data;
}
