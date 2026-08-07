import { api } from './client';
import type { OdemeDurumu, OdemeYontemi } from './payments';

export interface GunlukSatis {
  succeededCount: number;
  totalAmount: number;
  refundedCount: number;
  refundedAmount: number;
  failedCount: number;
  currency: string;
}

export interface KuyrukDurumu {
  pending: number;
  retryable: number;
  deadLettered: number;
}

export interface SistemSagligi {
  database: boolean;
  redis: boolean;
}

export interface AdminOzeti {
  generatedAtUtc: string;
  today: GunlukSatis;
  queue: KuyrukDurumu;
  health: SistemSagligi;
  activePaymentProvider: string;
  ticketsIssuedToday: number;
  pendingReservations: number;
  upcomingSessions: number;
}

export async function ozetGetir(): Promise<AdminOzeti> {
  const { data } = await api.get<AdminOzeti>('/admin/overview');
  return data;
}

export interface SayfaliSonuc<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface AdminOdeme {
  id: string;
  reservationId: string;
  status: OdemeDurumu;
  provider: string;
  method: OdemeYontemi;
  providerReference: string | null;
  amount: number;
  currency: string;
  userFullName: string;
  userEmail: string;
  eventTitle: string;
  sessionStartsAtUtc: string;
  seatCount: number;
  completedAtUtc: string | null;
  failureReason: string | null;
  createdAt: string;
}

export interface OdemeFiltresi {
  status?: OdemeDurumu;
  search?: string;
  pageNumber?: number;
  method?: OdemeYontemi;
}

export async function adminOdemeleriGetir(
  filtre: OdemeFiltresi,
): Promise<SayfaliSonuc<AdminOdeme>> {
  const { data } = await api.get<SayfaliSonuc<AdminOdeme>>('/admin/payments', {
    // Bos degerler gonderilmiyor: sunucu bos metni "bos olana gore filtrele"
    // diye yorumlamasa da sorgu dizesi gereksiz sisiyor ve react-query'nin
    // onbellek anahtari her tuş vurusunda degisiyordu.
    params: {
      status: filtre.status,
      search: filtre.search?.trim() || undefined,
      pageNumber: filtre.pageNumber ?? 1,
      method: filtre.method,
    },
  });

  return data;
}

/**
 * Odemeyi iade eder.
 *
 * Sunucu tarafinda biletler iptal olur ve koltuklar satisa doner; bu yuzden
 * arayuzde onay isteniyor. Geri alinamaz bir islem.
 */
export async function odemeIadeEt(odemeId: string, sebep: string): Promise<void> {
  await api.post(`/payments/${odemeId}/refund`, { reason: sebep });
}

export type RolAdi = 'Customer' | 'Organizer' | 'Moderator' | 'Admin';

export interface AdminKullanici {
  id: string;
  fullName: string;
  email: string;
  emailConfirmed: boolean;
  roles: RolAdi[];
  reservationCount: number;
  ticketCount: number;
  createdAt: string;
}

export async function adminKullanicilariGetir(filtre: {
  search?: string;
  role?: RolAdi;
  pageNumber?: number;
}): Promise<SayfaliSonuc<AdminKullanici>> {
  const { data } = await api.get<SayfaliSonuc<AdminKullanici>>('/admin/users', {
    params: {
      search: filtre.search?.trim() || undefined,
      role: filtre.role,
      pageNumber: filtre.pageNumber ?? 1,
    },
  });

  return data;
}

export interface AcilanHesap {
  userId: string;
  email: string;
  /** Posta sunucusu tanimli degilse false; hesap yine de acilmis olur. */
  resetLinkSent: boolean;
}

/**
 * Organizator/sanatci icin hesap acar.
 *
 * Sifre YOK: sunucu rastgele bir sifre uretip kullaniciya sifirlama
 * baglantisi gonderiyor. Yonetici bir sifre belirleseydi onu kullaniciya
 * bir kanaldan iletmesi gerekirdi ve o kanalda kalici olarak dururdu.
 */
export async function hesapAc(istek: {
  email: string;
  fullName: string;
  phoneNumber: string | null;
  roles: RolAdi[];
}): Promise<AcilanHesap> {
  const { data } = await api.post<AcilanHesap>('/admin/users', istek);
  return data;
}

export async function rolDegistir(
  kullaniciId: string,
  rol: RolAdi,
  ver: boolean,
): Promise<void> {
  await api.post(`/admin/users/${kullaniciId}/roles`, { roleName: rol, grant: ver });
}

export type KuyrukFiltresi = 'Pending' | 'Retryable' | 'DeadLettered' | 'Processed';

/** Mesajin govdesi BILEREK yok: kisisel veri iceriyor, sunucu hic gondermiyor. */
export interface KuyrukMesaji {
  id: string;
  type: string;
  retryCount: number;
  errorMessage: string | null;
  correlationId: string | null;
  occurredAtUtc: string;
  processedAtUtc: string | null;
}

export async function kuyrukGetir(durum: KuyrukFiltresi): Promise<KuyrukMesaji[]> {
  const { data } = await api.get<KuyrukMesaji[]>('/admin/queue', { params: { durum } });
  return data;
}

export async function mesajiKuyrugaKoy(mesajId: string): Promise<void> {
  await api.post(`/admin/queue/${mesajId}/requeue`);
}

/**
 * SMTP ayarlari.
 *
 * Sifrenin KENDISI hicbir zaman gelmiyor, yalnizca tanimli olup olmadigi
 * (`hasPassword`). Gelseydi panele erisen herkes posta hesabinin sifresini
 * okuyabilirdi ve o sifre baska yerlerde de kullaniliyor olabilir.
 */
export interface SmtpAyarlari {
  host: string;
  port: number;
  useSsl: boolean;
  userName: string | null;
  hasPassword: boolean;
  fromAddress: string;
  fromName: string;
  /** `Database` panelden girilmis, `Configuration` sunucu dosyasindan, `None` tanimsiz. */
  source: 'Database' | 'Configuration' | 'Mixed' | 'None';
  isConfigured: boolean;
}

export async function smtpAyarlariGetir(): Promise<SmtpAyarlari> {
  const { data } = await api.get<SmtpAyarlari>('/admin/settings/smtp');
  return data;
}

export interface SmtpKayit {
  host: string;
  port: number;
  useSsl: boolean;
  userName: string | null;
  /** Bos birakilirsa mevcut sifre KORUNUR. Silmek icin `clearPassword`. */
  password: string | null;
  fromAddress: string;
  fromName: string;
  clearPassword: boolean;
}

export async function smtpAyarlariKaydet(ayarlar: SmtpKayit): Promise<void> {
  await api.put('/admin/settings/smtp', ayarlar);
}

export interface SmtpTestSonucu {
  succeeded: boolean;
  error: string | null;
}

/** Sunucuya baglanmayi dener, posta GONDERMEZ. */
export async function smtpBaglantisiDene(): Promise<SmtpTestSonucu> {
  const { data } = await api.post<SmtpTestSonucu>('/admin/settings/smtp/test');
  return data;
}

export type AyarKaynagi = SmtpAyarlari['source'];

export interface HavaleAyarlari {
  enabled: boolean;
  bankName: string;
  accountName: string;
  iban: string;
  /**
   * Havale ile odenen rezervasyonun kac saat ayakta kalacagi.
   *
   * Kart odemesinden AYRI olmak zorunda: koltuk kilidi on dakika ve havale
   * banka saatlerine bagli — on dakikalik pencerede havale yapilamaz.
   */
  deadlineHours: number;
}

/**
 * Odeme ayarlari.
 *
 * Anahtarlarin KENDISI hicbir zaman gelmiyor, yalnizca tanimli olup
 * olmadiklari. `activeProvider` panelden degistirilemiyor: saglayici secimi
 * acilista bir kez yapiliyor. Ekranda gorunmesinin sebebi, anahtar girip
 * "neden calismiyor" diye sorulmasini onlemek.
 */
export interface OdemeAyarlari {
  activeProvider: string;
  hasApiKey: boolean;
  hasSecretKey: boolean;
  useSandbox: boolean;
  callbackUrl: string;
  returnUrl: string;
  source: AyarKaynagi;
  iyzicoConfigured: boolean;
  bankTransfer: HavaleAyarlari;
  /** Bilet fiyatinin ustune eklenen yuzde. Komisyon DEGIL. */
  serviceFeePercent: number;
  /** Bilet basina en az alinacak tutar. */
  serviceFeeMinPerTicket: number;
}

export async function odemeAyarlariGetir(): Promise<OdemeAyarlari> {
  const { data } = await api.get<OdemeAyarlari>('/admin/settings/payment');
  return data;
}

export interface OdemeAyarKayit {
  /** Bos birakilirsa mevcut anahtar KORUNUR. Silmek icin `clearIyzicoKeys`. */
  apiKey: string | null;
  secretKey: string | null;
  useSandbox: boolean;
  callbackUrl: string;
  returnUrl: string;
  bankTransferEnabled: boolean;
  bankName: string;
  accountName: string;
  iban: string;
  deadlineHours: number;
  clearIyzicoKeys: boolean;
  serviceFeePercent: number;
  serviceFeeMinPerTicket: number;
}

export async function odemeAyarlariKaydet(ayarlar: OdemeAyarKayit): Promise<void> {
  await api.put('/admin/settings/payment', ayarlar);
}
