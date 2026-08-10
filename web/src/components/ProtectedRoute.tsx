import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';

import { useAuthStore } from '../stores/authStore';

interface ProtectedRouteProps {
  children: ReactNode;
}

/**
 * Oturum kontrolu.
 *
 * Yalnizca "giris yapilmis mi" sorusunu sorar. Rol kisiti gereken sayfalar
 * icin RoleRoute kullanilir — iki sorunun cevapsizlik sonucu farkli:
 * oturum yoksa giris ekranina, yetki yoksa yetkisiz ekranina gidilir.
 *
 * Bu kontrol bir GUVENLIK onlemi degil, bir kullanici deneyimi onlemidir:
 * tarayicidaki JavaScript her zaman degistirilebilir. Gercek koruma sunucuda,
 * <c>[Authorize]</c> ve policy'lerde. Buradaki amac, yetkisi olmayan
 * kullaniciya bos ya da hata dolu bir ekran gostermemek.
 */
export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const location = useLocation();
  const { kullanici, accessToken, hazir } = useAuthStore();

  // Sayfa yenilendiginde access token bellekte olmadigi icin once yenileme
  // denenir. O bitmeden karar verilirse kullanici bir anligina giris
  // ekranina atilir ve geri doner — bu titreme yasanmasin diye beklenir.
  if (!hazir) {
    return (
      <div className="min-h-screen flex items-center justify-center" role="status">
        <span className="sr-only">Oturum kontrol ediliyor</span>
        <span className="h-8 w-8 animate-spin rounded-full border-2 border-primary border-t-transparent" />
      </div>
    );
  }

  if (!accessToken || !kullanici) {
    // Nereye gitmek istedigi tasinir; giristen sonra oraya donulur.
    //
    // SORGU DIZESI DE TASINIYOR. Yalnizca `pathname` tasinsaydi adresteki
    // parametreler sessizce dusuyordu ve en pahali yerde: odeme
    // saglayicisindan donuldugunde adres `/odeme/{rez}?odemeId={odeme}`
    // oluyor, `odemeId` de arayuzun "saglayicidan donuldu, tamamlama
    // cagrisini yap" isareti. Oturum o sirada yenilenmek zorunda kalirsa
    // parametre kayboluyor, ekran odeme formunu bastan gosteriyor ve
    // parasini odemis kullanici ikinci kez odemeye davet ediliyordu.
    const hedef = location.pathname + location.search;

    return <Navigate to="/giris" state={{ hedef }} replace />;
  }

  return <>{children}</>;
}
