import type { ReactNode } from 'react';
import { Navigate } from 'react-router-dom';

import { ProtectedRoute } from './ProtectedRoute';
import { useAuthStore } from '../stores/authStore';

interface RoleRouteProps {
  children: ReactNode;
  /** Kullanicinin bu rollerden en az birine sahip olmasi gerekir. */
  roller: string[];
}

/**
 * Rol kisitli sayfa sarmalayicisi.
 *
 * Oturum kontrolu ProtectedRoute'ta kaldi; burasi yalnizca rol soruyor.
 * Ayri tutulmalarinin sebebi iki sorunun farkli olmasi: "kimsin" sorusunun
 * cevabi yoksa giris ekranina, "yetkin var mi" sorusunun cevabi hayirsa
 * yetkisiz ekranina gidilir. Tek bilesende toplandiginda bu iki yol
 * karisir ve giris yapmis bir kullanici yanlislikla giris ekranina
 * geri atilabilir.
 *
 * Sunucudaki policy'lerin yerini TUTMAZ: tarayicidaki JavaScript her zaman
 * degistirilebilir, gercek koruma [Authorize] ve policy'lerde.
 */
export function RoleRoute({ children, roller }: RoleRouteProps) {
  return (
    <ProtectedRoute>
      <RolKontrolu roller={roller}>{children}</RolKontrolu>
    </ProtectedRoute>
  );
}

/**
 * Rol karsilastirmasi ProtectedRoute'un icinde yapilir; disarida yapilsaydi
 * oturum henuz geri getirilmeden calisir ve kullanici rolsuz gorunurdu.
 */
function RolKontrolu({ children, roller }: RoleRouteProps) {
  const kullanici = useAuthStore((durum) => durum.kullanici);

  if (!kullanici || !roller.some((rol) => kullanici.roles.includes(rol))) {
    return <Navigate to="/yetkisiz" replace />;
  }

  return <>{children}</>;
}
