import { hataKodu, hataMesaji } from '../../api/client';
import type { Rezervasyon } from '../../api/reservations';

/** Odeme akisinin baglandigi rezervasyon durumu icin kisa aciklama. */
export const REZERVASYON_DURUM_METNI: Record<Rezervasyon['status'], string> = {
  Pending: 'Odeme bekleniyor',
  Confirmed: 'Bu rezervasyon zaten ödendi.',
  Cancelled: 'Bu rezervasyon iptal edilmiş.',
  Expired: 'Süresi doldu, koltuklar serbest bırakıldı.',
};

/**
 * Sunucunun makine okunur odeme hata kodlari icin kullanici mesaji.
 *
 * Karar mesaj metnine degil koda gore veriliyor ki sunucudaki metin
 * degistiginde arayuz mantigi kirilmasin.
 */
export const ODEME_HATA_METNI: Record<string, string> = {
  'Payment.AlreadyPaid': 'Bu rezervasyon zaten ödendi.',
  'Payment.AlreadyPending': 'Bu rezervasyon için zaten bekleyen bir ödeme var.',
  'Payment.ReservationNotActive': 'Süresi doldu, koltuklar bırakıldı.',
  'Payment.ProviderRejected': 'Ödeme sağlayıcı işlemi reddetti.',
  'Payment.NotOwner': 'Bu rezervasyon sana ait değil.',
  'Payment.SeatsNoLongerHeld': 'Koltukların süresi doldu, tekrar seç.',
  'Payment.BankTransferDisabled': 'Havale ile ödeme şu anda kapalı.',
  'Payment.BankTransferNotCompletable':
    'Havale ödemesi burada tamamlanmaz; ödemen ulaştığında yönetim onaylar.',
};

/** Bu kodlarda koltuklar sunucu tarafinda zaten serbest birakilmistir. */
export const KOLTUK_SERBEST_KODLARI = new Set([
  'Payment.ReservationNotActive',
  'Payment.SeatsNoLongerHeld',
]);

export function odemeHatasiniAcikla(hata: unknown, varsayilan: string): { mesaj: string; kod?: string } {
  const kod = hataKodu(hata);
  const mesaj = (kod && ODEME_HATA_METNI[kod]) || hataMesaji(hata, varsayilan);
  return { mesaj, kod };
}
