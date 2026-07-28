import { api } from './client';
import type { KullaniciOzeti, OturumCevabi } from '../stores/authStore';

export interface KayitIstegi {
  email: string;
  password: string;
  fullName: string;
  phoneNumber?: string;
}

export interface GirisIstegi {
  email: string;
  password: string;
}

export async function kayitOl(istek: KayitIstegi): Promise<OturumCevabi> {
  const { data } = await api.post<OturumCevabi>('/auth/register', istek);
  return data;
}

export async function girisYap(istek: GirisIstegi): Promise<OturumCevabi> {
  const { data } = await api.post<OturumCevabi>('/auth/login', istek);
  return data;
}

export async function benKimim(): Promise<KullaniciOzeti> {
  const { data } = await api.get<KullaniciOzeti>('/auth/me');
  return data;
}

/**
 * Cikis. Sunucu token'i iptal eder.
 * Sunucu hata donse bile yerel oturum kapatilir — kullanici cikmak istedi.
 */
export async function cikisYap(refreshToken: string): Promise<void> {
  await api.post('/auth/logout', { refreshToken });
}
