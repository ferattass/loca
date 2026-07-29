import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';

import { ProtectedRoute } from './components/ProtectedRoute';
import { SeatStatePreview } from './components/SeatStatePreview';
import { useOturumBaslat } from './hooks/useOturumBaslat';
import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
import { HomePage } from './pages/HomePage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';
import { UnauthorizedPage } from './pages/UnauthorizedPage';

/**
 * Uygulama yonlendirmesi.
 *
 * Korumali sayfalar <c>ProtectedRoute</c> ile sarilir. Bu sarmalayici bir
 * guvenlik siniri degil kullanici deneyimi sinridir: gercek yetki kontrolu
 * sunucuda yapilir. Amaci, oturumu olmayan kullaniciya bos veri veya 401
 * dolu bir ekran gostermek yerine dogrudan giris sayfasina yonlendirmek.
 */
export default function App() {
  useOturumBaslat();

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/giris" element={<LoginPage />} />
        <Route path="/kayit" element={<RegisterPage />} />
        <Route path="/sifremi-unuttum" element={<ForgotPasswordPage />} />
        <Route path="/sifre-sifirla" element={<ResetPasswordPage />} />
        <Route path="/yetkisiz" element={<UnauthorizedPage />} />

        <Route
          path="/"
          element={
            <ProtectedRoute>
              <HomePage />
            </ProtectedRoute>
          }
        />

        {/* Gun 2'de yazilan tasarim sistemi dogrulama sayfasi. Teslimde
            kaldirilacak; simdilik token'larin dogru bagli oldugunu
            gostermek icin duruyor. */}
        <Route
          path="/tasarim"
          element={
            <div className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
              <h1 className="font-headline text-headline-md text-on-surface mb-stack-sm">
                Koltuk durumlari
              </h1>
              <SeatStatePreview />
            </div>
          }
        />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
