import { useEffect } from 'react';
import axios from 'axios';

import { useAuthStore } from '../stores/authStore';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api/v1';

/**
 * Sayfa yuklemesi basina tek yenileme denemesi.
 *
 * Bileşen icinde degil modul seviyesinde tutulur; sebebi asagida.
 */
let acilisYenilemesi: Promise<void> | null = null;

function oturumuGeriGetir(): Promise<void> {
  const { refreshToken, oturumAc, oturumKapat, hazirIsaretle } = useAuthStore.getState();

  if (!refreshToken) {
    hazirIsaretle();
    return Promise.resolve();
  }

  return axios
    .post(`${BASE_URL}/auth/refresh`, { refreshToken })
    .then(({ data }) => oturumAc(data))
    .catch(() => oturumKapat())
    .finally(() => hazirIsaretle());
}

/**
 * Uygulama acilisinda oturumu geri getirir.
 *
 * Access token yalnizca bellekte tutuldugu icin sayfa yenilendiginde kaybolur.
 * Elde refresh token varsa bir kez yenileme denenir.
 *
 * <b>Neden modul seviyesinde kilit var:</b> React gelistirme kipinde
 * (StrictMode) effect'leri bilerek iki kez calistirir. Koruma olmadan ayni
 * refresh token ile iki istek gider; sunucu her yenilemede token'i degistirdigi
 * icin ikinci istek iptal edilmis bir token sunar ve sunucu bunu "token
 * calinmis olabilir" sayarak kullanicinin butun oturumlarini kapatir.
 * Yani kendi guvenlik onlemimiz, istemcinin dikkatsizligi yuzunden
 * kullaniciyi her sayfa yenilemesinde disari atardi.
 */
export function useOturumBaslat() {
  useEffect(() => {
    acilisYenilemesi ??= oturumuGeriGetir();
  }, []);
}
