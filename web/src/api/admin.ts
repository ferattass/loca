import { api } from './client';
import type { OdemeDurumu } from './payments';

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

export type RolAdi = 'Customer' | 'Organizer' | 'Admin';

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
