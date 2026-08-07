import { BrowserRouter, Route, Routes } from 'react-router-dom';

import { ProtectedRoute } from './components/ProtectedRoute';
import { SiteKabugu } from './components/SiteKabugu';
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
import { BulunamadiPage } from './pages/BulunamadiPage';
import { EtkinlikDetayPage } from './pages/EtkinlikDetayPage';
import { ResetPasswordPage } from './pages/ResetPasswordPage';
import { RezervasyonPage } from './pages/RezervasyonPage';
import { RezervasyonlarimPage } from './pages/RezervasyonlarimPage';
import { SeatLayoutPage } from './pages/SeatLayoutPage';
import { UnauthorizedPage } from './pages/UnauthorizedPage';
import { GizlilikPage } from './pages/bilgi/GizlilikPage';
import { HakkimizdaPage } from './pages/bilgi/HakkimizdaPage';
import { IletisimPage } from './pages/bilgi/IletisimPage';
import { KullanimKosullariPage } from './pages/bilgi/KullanimKosullariPage';
import { MekanlarPage } from './pages/MekanlarPage';
import { YardimPage } from './pages/bilgi/YardimPage';
import { RoleRoute } from './components/RoleRoute';
import { YonetimKabugu } from './components/YonetimKabugu';
import { AyarlarPage } from './pages/yonetim/AyarlarPage';
import { KullanicilarPage } from './pages/yonetim/KullanicilarPage';
import { OdemeAyarlariPage } from './pages/yonetim/OdemeAyarlariPage';
import { OnayKuyruguPage } from './pages/yonetim/OnayKuyruguPage';
import { OdemelerPage } from './pages/yonetim/OdemelerPage';
import { OzetPage } from './pages/yonetim/OzetPage';
import { SistemPage } from './pages/yonetim/SistemPage';

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

        {/* Etkinlik detayi GIRIS ISTEMEZ, vitrinin devami.

            "/etkinlikler/yeni" ile CAKISMIYOR: React Router rotalari
            yazilma sirasina gore degil ozgullugune gore eslestiriyor ve
            sabit parca (yeni) degiskenden (:id) once geliyor. Sira
            onemli olsaydi asagida tanimli "yeni" rotasi hicbir zaman
            calismazdi. */}
        <Route path="/etkinlikler/:id" element={<EtkinlikDetayPage />} />

        {/* Bilgi sayfalari: alt bilgideki uc baglantinin karsiligi. Onceden
            duz metin olarak duruyorlardi cunku sayfalari yoktu. */}
        <Route path="/mekanlar" element={<MekanlarPage />} />
        <Route path="/yardim" element={<YardimPage />} />
        <Route path="/iletisim" element={<IletisimPage />} />
        <Route path="/kullanim-kosullari" element={<KullanimKosullariPage />} />
        <Route path="/hakkimizda" element={<HakkimizdaPage />} />
        <Route path="/gizlilik" element={<GizlilikPage />} />

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

        {/* Koltuk plani duzenleme yonetim kabugunun DISINDA: tam genislikte
            bir tuval istiyor ve yandaki menu calisma alanini daraltiyordu. */}
        <Route
          path="/oturma-planlari/:id"
          element={
            <RoleRoute roller={['Admin']}>
              <SeatLayoutPage />
            </RoleRoute>
          }
        />

        {/* Tanimsiz adres KABUGUN ICINDE: 404 sayfasi baslik ve alt bilgi
            olmadan aciliyordu, yani kullanicinin gezinecek hicbir menusu
            kalmiyordu — kaybolan kisiyi bir de yalniz birakmak olurdu.

            Sessizce ana sayfaya yonlendirmenin yerini aldi. Yonlendirme
            bir hatayi tamamen ortuyordu: etkinlik karti /etkinlikler/:id
            adresine bagliydi, o rota hic tanimli degildi ve "Bilet al"a
            basan kullanici ana sayfaya donuyordu. Kirik bir baglanti,
            calisan bir baglanti gibi davraniyordu. */}
        <Route path="*" element={<BulunamadiPage />} />

        </Route>

        {/* Yonetim KENDI kabugunda: site kabugunun ust menusu sekiz
            baglantiya cikip okunamaz hâle geliyordu ve yonetici hangi
            baglamda oldugunu kaybediyordu. Yetki hem burada hem sunucudaki
            AdminOnly policy'sinde; buradaki yalnizca kullanici deneyimi. */}
        <Route
          path="/yonetim"
          element={
            /* Moderator de yonetim kabuguna giriyor ama YALNIZCA onay
               kuyrugunu goruyor: menu rolune gore kuruluyor ve asil
               kisit sunucudaki policy'lerde. Ayri bir kabuk yazilsaydi
               ayni duzen iki yerde yasardi. */
            <RoleRoute roller={['Admin', 'Moderator']}>
              <YonetimKabugu />
            </RoleRoute>
          }
        >
          <Route index element={<OzetPage />} />
          <Route path="odemeler" element={<OdemelerPage />} />
          <Route path="kullanicilar" element={<KullanicilarPage />} />
          <Route path="sistem" element={<SistemPage />} />
          <Route path="ayarlar" element={<AyarlarPage />} />
          <Route path="odeme-ayarlari" element={<OdemeAyarlariPage />} />
          <Route path="onay-kuyrugu" element={<OnayKuyruguPage />} />
          <Route path="mekanlar" element={<MekanYonetimPage />} />
          <Route path="oturma-planlari" element={<OturmaPlaniYonetimPage />} />
        </Route>

      </Routes>
    </BrowserRouter>
  );
}
