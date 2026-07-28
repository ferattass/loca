import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';

import { useAuthStore } from '../stores/authStore';

interface ProtectedRouteProps {
  children: ReactNode;
  /** Verilirse kullanicinin bu rollerden en az birine sahip olmasi gerekir. */
  roller?: string[];
}

/**
 * Oturum ve rol kontrolu.
 *
 * Bu kontrol bir GUVENLIK onlemi degil, bir kullanici deneyimi onlemidir:
 * tarayicidaki JavaScript her zaman degistirilebilir. Gercek koruma sunucuda,
 * <c>[Authorize]</c> ve policy'lerde. Buradaki amac, yetkisi olmayan
 * kullaniciya bos ya da hata dolu bir ekran gostermemek.
 */
export function ProtectedRoute({ children, roller }: ProtectedRouteProps) {
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
    return <Navigate to="/giris" state={{ hedef: location.pathname }} replace />;
  }

  if (roller && !roller.some((rol) => kullanici.roles.includes(rol))) {
    return <Navigate to="/yetkisiz" replace />;
  }

  return <>{children}</>;
}
