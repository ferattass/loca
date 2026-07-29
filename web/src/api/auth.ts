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

/**
 * Sifre sifirlama baglantisi ister.
 *
 * Adres kayitli olmasa da sunucu basarili doner; hangi e-postalarin sistemde
 * oldugu bu uc denenerek ogrenilemesin diye. Arayuz de bu yuzden her durumda
 * ayni mesaji gosterir.
 */
export async function sifremiUnuttum(email: string): Promise<void> {
  await api.post('/auth/forgot-password', { email });
}

export async function sifreSifirla(token: string, newPassword: string): Promise<void> {
  await api.post('/auth/reset-password', { token, newPassword });
}

/** Basarili oldugunda sunucu tum oturumlari kapatir; yeniden giris gerekir. */
export async function sifreDegistir(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  await api.post('/auth/change-password', { currentPassword, newPassword });
}
