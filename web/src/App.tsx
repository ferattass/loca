import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';

import { ProtectedRoute } from './components/ProtectedRoute';
import { SiteKabugu } from './components/SiteKabugu';
import { SeatStatePreview } from './components/SeatStatePreview';
import { useOturumBaslat } from './hooks/useOturumBaslat';
import { EtkinlikOlusturPage } from './pages/EtkinlikOlusturPage';
import { ForgotPasswordPage } from './pages/ForgotPasswordPage';
import { HomePage } from './pages/HomePage';
import { KapiOkutmaPage } from './pages/KapiOkutmaPage';
import { KesfetPage } from './pages/KesfetPage';
import { KoltukSecimPage } from './pages/KoltukSecimPage';
import { LoginPage } from './pages/LoginPage';
import { MekanYonetimPage } from './pages/MekanYonetimPage';
import { OturmaPlaniYonetimPage } from './pages/OturmaPlaniYonetimPage';
import { OdemePage } from './pages/OdemePage';
import { BiletlerimPage } from './pages/BiletlerimPage';
import { RegisterPage } from './pages/RegisterPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';
import { RezervasyonPage } from './pages/RezervasyonPage';
import { RezervasyonlarimPage } from './pages/RezervasyonlarimPage';
import { SeatLayoutPage } from './pages/SeatLayoutPage';
import { UnauthorizedPage } from './pages/UnauthorizedPage';
import { RoleRoute } from './components/RoleRoute';

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
        {/* Kimlik ekranlari kabugun DISINDA: gezinme menusu henuz
            erisilemeyecek sayfalari kullanicinin onune koyardi. */}
        <Route path="/giris" element={<LoginPage />} />
        <Route path="/kayit" element={<RegisterPage />} />
        <Route path="/sifremi-unuttum" element={<ForgotPasswordPage />} />
        <Route path="/sifre-sifirla" element={<ResetPasswordPage />} />
        <Route path="/yetkisiz" element={<UnauthorizedPage />} />

        {/* Kalan her sayfa ortak kabugu (baslik + alt bilgi) paylasiyor.
            Duzen rota seviyesinde: her sayfanin kendi icine baslik koymasi
            gerekseydi biri unutulur ve o ekranda gezinme kaybolurdu. */}
        <Route element={<SiteKabugu />}>
        {/* Kok sayfa GIRIS ISTEMEZ: etkinlik vitrini herkese acik olmali,
            kullanici neyi satin alacagini gormeden kayit olmak zorunda
            kalmasin. Korumali olsaydi arama motorlari da hicbir seyi
            goremezdi. */}
        <Route path="/" element={<KesfetPage />} />

        <Route
          path="/hesabim"
          element={
            <ProtectedRoute>
              <HomePage />
            </ProtectedRoute>
          }
        />

        {/* Koltuk secimi giris ISTEMEZ: kullanici salonun doluluk durumunu
            gormeden kayit olmak zorunda kalmasin. Rezervasyon acma ucu
            sunucuda zaten kimlik dogrulamasi istiyor; giris yapmamis
            kullanici "Koltuklari kilitle" dedigi anda 401 alir. */}
        <Route path="/oturumlar/:id/koltuklar" element={<KoltukSecimPage />} />

        <Route
          path="/rezervasyonlar/:id"
          element={
            <ProtectedRoute>
              <RezervasyonPage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/rezervasyonlarim"
          element={
            <ProtectedRoute>
              <RezervasyonlarimPage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/odeme/:rezervasyonId"
          element={
            <ProtectedRoute>
              <OdemePage />
            </ProtectedRoute>
          }
        />

        <Route
          path="/biletlerim"
          element={
            <ProtectedRoute>
              <BiletlerimPage />
            </ProtectedRoute>
          }
        />

        {/* Etkinlik olusturma organizatore acik; admin de erisebilir cunku
            sunucudaki OrganizerOnly policy'si admin'i de kapsiyor. */}
        <Route
          path="/etkinlikler/yeni"
          element={
            <RoleRoute roller={['Organizer', 'Admin']}>
              <EtkinlikOlusturPage />
            </RoleRoute>
          }
        />

        {/* Kapi ekrani organizatore acik: bileti okutan kisi etkinligin
            sahibi veya gorevlisi. Sunucu ayrica biletin ait oldugu
            etkinligin sahipligini kontrol ediyor — organizator baska bir
            organizatorun biletini okutamiyor. */}
        <Route
          path="/kapi"
          element={
            <RoleRoute roller={['Organizer', 'Admin']}>
              <KapiOkutmaPage />
            </RoleRoute>
          }
        />

        {/* Mekan, salon ve oturma plani sistemin REFERANS verisi: organizator
            bile degistiremez, yalnizca secer. Sunucuda da bu uclarin tamami
            AdminOnly policy'siyle korunuyor. */}
        <Route
          path="/yonetim/mekanlar"
          element={
            <RoleRoute roller={['Admin']}>
              <MekanYonetimPage />
            </RoleRoute>
          }
        />

        <Route
          path="/yonetim/oturma-planlari"
          element={
            <RoleRoute roller={['Admin']}>
              <OturmaPlaniYonetimPage />
            </RoleRoute>
          }
        />

        {/* Koltuk plani yonetimi yalnizca admin'e acik; sunucuda da
            toggle-active ucu AdminOnly policy'siyle korunuyor. */}
        <Route
          path="/oturma-planlari/:id"
          element={
            <RoleRoute roller={['Admin']}>
              <SeatLayoutPage />
            </RoleRoute>
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

        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
