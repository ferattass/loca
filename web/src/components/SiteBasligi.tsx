import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router-dom';

import { sehirleriGetir, type Sehir } from '../api/catalog';
import { kategorileriGetir } from '../api/events';
import type { EtkinlikKategorisi } from '../api/events';
import { MenuAcilir, type AcilirSecenek } from './MenuAcilir';

import { Logo } from './ui/Logo';
import { CarpiIkonu, MenuIkonu } from './ui/Ikon';

import { cikisYap } from '../api/auth';
import { useAuthStore } from '../stores/authStore';

/**
 * Ust navigasyon.
 *
 * Tasarimda her ekranin ustunde ayni cubuk var; sayfalar kendi basliklarini
 * tasidiginda kullanici hangi uygulamada oldugunu ve nereye gidebilecegini
 * kaybediyordu. Kabuk tek yerde durdugu icin yeni bir sayfa eklendiginde
 * gezinme kendiliginden geliyor.
 */
/** Ust menudeki "Kurumsal" maddesinin kapsadigi yollar. */
const BILGI_YOLLARI = [
  '/hakkimizda',
  '/yardim',
  '/iletisim',
  '/kullanim-kosullari',
  '/gizlilik',
];

export function SiteBasligi() {
  const navigate = useNavigate();
  const konum = useLocation();
  const [aramaParametreleri] = useSearchParams();
  const { kullanici, refreshToken, oturumKapat } = useAuthStore();
  const [menuAcik, setMenuAcik] = useState(false);

  // Kategori ve sehir listeleri menude gerekiyor. Uzun sure onbellekte
  // kaliyorlar: ikisi de neredeyse hic degismeyen kucuk kataloglar ve her
  // sayfa gecisinde yeniden istenmelerinin bir karsiligi yok.
  const kategoriler = useQuery<EtkinlikKategorisi[]>({
    queryKey: ['event-categories'],
    queryFn: kategorileriGetir,
    staleTime: 10 * 60 * 1000,
  });

  const sehirler = useQuery<Sehir[]>({
    queryKey: ['cities'],
    queryFn: sehirleriGetir,
    staleTime: 10 * 60 * 1000,
  });

  const girisYapildi = kullanici !== null;
  const adminMi = kullanici?.roles.includes('Admin') ?? false;
  const organizatorMu = adminMi || (kullanici?.roles.includes('Organizer') ?? false);

  const cikis = async () => {
    try {
      if (refreshToken) await cikisYap(refreshToken);
    } catch {
      // Sunucuya ulasilamasa bile yerel oturum kapatilir: kullanici cikmak
      // istedi, bu istegi ag hatasi yuzunden gormezden gelmek yanlis olur.
    } finally {
      oturumKapat();
      navigate('/giris', { replace: true });
    }
  };

  // Giris yapmamis kullanici ONCEDEN tek sekme goruyordu ("Keşfet"): geri
  // kalan her baglanti oturuma bagliydi. Vitrine ilk gelen kisi menuye
  // bakip sitenin tek sayfadan ibaret oldugunu sanabilirdi. Herkese acik
  // olan ne varsa artik menude.
  //
  // "Bugün" ayni rotaya sorgu dizesiyle gidiyor; secili gorunmesi bu
  // yuzden NavLink'e birakilamiyor (NavLink sorgu dizesine bakmaz, ikisi
  // de secili gorunurdu). Aktiflik acikca hesaplaniyor.
  const zamanSecimi = aramaParametreleri.get('ne_zaman');
  const anaSayfada = konum.pathname === '/';

  const bugunSecili = anaSayfada && zamanSecimi === 'bugun';
  const haftaSecili = anaSayfada && zamanSecimi === 'hafta';
  const kategoriSecili = anaSayfada && aramaParametreleri.has('kategori');
  const sehirSecili = anaSayfada && aramaParametreleri.has('sehir');

  const kesfetSecili =
    anaSayfada && !bugunSecili && !haftaSecili && !kategoriSecili && !sehirSecili;

  // UST CUBUK MADDE DEGIL KONU TASIYOR.
  //
  // Onceki hâlde her sayfa ayri bir baglantiydi ve giris yapmis bir
  // organizatorde cubuk ON IKI maddeye cikiyordu: sigmiyor, sagdaki
  // maddeler kirpiliyor ve sayfa yatay kaydirma aciyordu. Maddeleri
  // kucultmek ya da bosluklari daraltmak yalnizca esigi oteler; sorun
  // sayinin kendisi.
  //
  // Maddeler artik konuya gore acilir listelerde. Rol bazli olanlar
  // (organizator, yonetim) yalnizca o role sahipse hic uretilmiyor:
  // gorunur ama tikladiginda 403 donen bir menu, yetkiyi menuden
  // ogrenmeye calisan kullaniciyi yaniltir.
  const kesfetSecenekleri: AcilirSecenek[] = [
    { id: 'tumu', metin: 'Tüm etkinlikler', yol: '/' },
    { id: 'bugun', metin: 'Bugün', yol: '/?ne_zaman=bugun' },
    { id: 'hafta', metin: 'Bu hafta', yol: '/?ne_zaman=hafta' },
    { id: 'mekanlar', metin: 'Mekânlar', yol: '/mekanlar' },
    // Kategoriler ayri bir acilir liste degil, bu listenin icinde bir
    // bolum: ikisi de "ne izleyeyim" sorusunun cevabi ve ayri
    // dusmelerinin bir sebebi yok.
    ...(kategoriler.data ?? []).map((kategori) => ({
      id: kategori.id,
      metin: kategori.name,
      yol: `/?kategori=${kategori.id}`,
      bolum: 'Kategoriler',
    })),
  ];

  const hesapSecenekleri: AcilirSecenek[] = girisYapildi
    ? [
        { id: 'rezervasyonlarim', metin: 'Rezervasyonlarım', yol: '/rezervasyonlarim' },
        { id: 'biletlerim', metin: 'Biletlerim', yol: '/biletlerim' },
        { id: 'hesabim', metin: 'Hesap ayarları', yol: '/hesabim' },
      ]
    : [];

  const organizatorSecenekleri: AcilirSecenek[] = organizatorMu
    ? [
        { id: 'etkinlik-yeni', metin: 'Etkinlik oluştur', yol: '/etkinlikler/yeni' },
        { id: 'kapi', metin: 'Kapıda bilet okut', yol: '/kapi' },
      ]
    : [];

  const kesfetBolumuAktif =
    kesfetSecili || bugunSecili || haftaSecili || kategoriSecili || konum.pathname === '/mekanlar';

  const hesapAktif = ['/rezervasyonlarim', '/biletlerim', '/hesabim'].includes(konum.pathname);

  const organizatorAktif = ['/etkinlikler/yeni', '/kapi'].includes(konum.pathname);

  // Yonetim tek baglantida kaliyor: kabugun kendi menusu zaten var ve
  // burada ikinci kez listelemek ayni sayfalari iki yerden yonetmek olurdu.
  const yonetimAktif = konum.pathname.startsWith('/yonetim');

  // DAR EKRANDA GRUPLAMA YOK, DUZ LISTE VAR.
  //
  // Genis ekranda maddeleri acilir listelere toplamanin sebebi yatay
  // yer darligi; mobil menu zaten dikey ve asagi dogru istedigi kadar
  // uzayabiliyor. Orada da acilir liste kullanmak, kullaniciyi ayni
  // hedefe ulasmak icin iki kez dokunmaya zorlardi — ustelik ic ice
  // acilan bir menu parmakla zor kullaniliyor.
  //
  // Kategoriler ve sehirler bu listeye GIRMIYOR: on bir madde daha
  // eklemek mobil menuyu okunamaz hâle getirirdi ve ikisi de kesfet
  // ekranindaki suzgec cubugundan zaten secilebiliyor.
  const mobilBaglantilar = [
    { yol: '/', metin: 'Keşfet', aktif: kesfetSecili },
    { yol: '/?ne_zaman=bugun', metin: 'Bugün', aktif: bugunSecili },
    { yol: '/?ne_zaman=hafta', metin: 'Bu hafta', aktif: haftaSecili },
    { yol: '/mekanlar', metin: 'Mekânlar', aktif: konum.pathname === '/mekanlar' },
    ...[...hesapSecenekleri, ...organizatorSecenekleri].map((secenek) => ({
      yol: secenek.yol,
      metin: secenek.metin,
      aktif: konum.pathname === secenek.yol,
    })),
    ...(adminMi ? [{ yol: '/yonetim', metin: 'Yönetim', aktif: yonetimAktif }] : []),
  ];

  return (
    <header className="sticky top-0 z-50 border-b border-outline-variant/40 bg-surface-container-lowest/90 backdrop-blur">
      <div className="mx-auto flex max-w-7xl items-center gap-stack-md px-container-margin-mobile py-stack-sm md:px-container-margin-desktop">
        {/* Cok dar ekranda yalnizca ISARET: yatay logo (~110 px) yaninda
            cikis ve menu dugmeleriyle birlikte dar bir telefona sigmiyor
            ve baslik satiri tasip yatay kaydirma aciyordu. */}
        <Link to="/" className="flex shrink-0 items-center" aria-label="Loca ana sayfa">
          <Logo bicim="isaret" className="h-7 w-7 sm:hidden" />
          <Logo bicim="yatay" className="hidden h-7 w-auto sm:block md:h-8" />
        </Link>

        {/* Genis ekranda yatay menu, dar ekranda acilir menu. Arama kutusu
            baslikta DEGIL kesfet ekraninin suzgec cubugunda: baslik her
            sayfada duruyor ve arama yalnizca katalogda anlamli. */}
        <nav aria-label="Ana menü" className="hidden flex-1 items-center gap-base lg:flex">
          <MenuAcilir
            baslik="Keşfet"
            aktif={kesfetBolumuAktif}
            secenekler={kesfetSecenekleri}
          />

          <MenuAcilir
            baslik="Şehirler"
            aktif={sehirSecili}
            secenekler={(sehirler.data ?? []).map((sehir) => ({
              id: sehir.id,
              metin: sehir.name,
              yol: `/?sehir=${sehir.id}`,
            }))}
          />

          {/* Bes bilgi sayfasi duz baglanti olsaydi cubuk tasardi; hepsi
              tek maddede toplandi. */}
          <MenuAcilir
            baslik="Kurumsal"
            aktif={BILGI_YOLLARI.includes(konum.pathname)}
            secenekler={[
              { id: 'hakkimizda', metin: 'Hakkımızda', yol: '/hakkimizda' },
              { id: 'yardim', metin: 'Yardım merkezi', yol: '/yardim' },
              { id: 'iletisim', metin: 'İletişim', yol: '/iletisim' },
              { id: 'kosullar', metin: 'Kullanım koşulları', yol: '/kullanim-kosullari' },
              { id: 'gizlilik', metin: 'Gizlilik', yol: '/gizlilik' },
            ]}
          />

          {/* Bu ikisi secenek listesi bosken MenuAcilir'in kendisi
              tarafindan hic cizilmiyor; ayrica kosul yazmaya gerek yok. */}
          <MenuAcilir baslik="Hesabım" aktif={hesapAktif} secenekler={hesapSecenekleri} />

          <MenuAcilir
            baslik="Organizatör"
            aktif={organizatorAktif}
            secenekler={organizatorSecenekleri}
          />

          {adminMi && (
            <Link
              to="/yonetim"
              aria-current={yonetimAktif ? 'page' : undefined}
              className={`whitespace-nowrap rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
                yonetimAktif
                  ? 'bg-primary-container/25 text-primary'
                  : 'text-on-surface-variant hover:text-on-surface'
              }`}
            >
              Yönetim
            </Link>
          )}
        </nav>

        <div className="ml-auto flex items-center gap-stack-sm">
          {/* Oturum dugmeleri MOBILDE GIZLI; ayni secenekler acilir
              menunun icinde duruyor. Basliga sigdirilmaya calisildiginda
              satir tasiyordu ve tasan sey, en cok kullanilan dugme olan
              menunun kendisiydi. */}
          {girisYapildi ? (
            <>
              <span className="hidden font-body text-body-sm text-on-surface-variant sm:inline">
                {kullanici?.fullName.split(' ')[0]}
              </span>
              <button
                type="button"
                onClick={cikis}
                className="hidden rounded-full border border-outline px-stack-sm py-1 font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high sm:inline-block"
              >
                Çıkış
              </button>
            </>
          ) : (
            <>
              <Link
                to="/giris"
                className="hidden font-body text-body-sm text-on-surface-variant hover:text-on-surface sm:inline"
              >
                Giriş yap
              </Link>
              <Link
                to="/kayit"
                className="hidden rounded-full bg-primary px-stack-sm py-1 font-body text-body-sm font-semibold text-on-primary sm:inline-block"
              >
                Kayıt ol
              </Link>
            </>
          )}

          {/* Kelime yerine ikon: "Menü" metni dar ekranda satiri tasiran
              son parcaydi. Erisilebilir ad aria-label'da duruyor. */}
          <button
            type="button"
            aria-expanded={menuAcik}
            aria-controls="mobil-menu"
            aria-label={menuAcik ? 'Menüyü kapat' : 'Menüyü aç'}
            onClick={() => setMenuAcik((acik) => !acik)}
            className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-outline text-on-surface transition-colors hover:bg-surface-container-high lg:hidden"
          >
            {menuAcik ? <CarpiIkonu className="h-5 w-5" /> : <MenuIkonu className="h-5 w-5" />}
          </button>
        </div>
      </div>

      {menuAcik && (
        <nav
          id="mobil-menu"
          aria-label="Ana menü"
          className="border-t border-outline-variant/40 px-container-margin-mobile py-stack-sm lg:hidden"
        >
          {girisYapildi && (
            <p className="mb-base px-stack-sm font-body text-body-sm text-on-surface-variant">
              {kullanici?.fullName}
            </p>
          )}

          <ul className="flex flex-col">
            {mobilBaglantilar.map((baglanti) => (
              <li key={baglanti.yol}>
                <Link
                  to={baglanti.yol}
                  onClick={() => setMenuAcik(false)}
                  aria-current={baglanti.aktif ? 'page' : undefined}
                  // Dokunma hedefi metin yuksekligi kadar degil, parmakla
                  // secilebilecek kadar buyuk.
                  className={`block rounded-md px-stack-sm py-stack-sm font-body text-body-md transition-colors ${
                    baglanti.aktif
                      ? 'bg-primary-container/25 font-semibold text-primary'
                      : 'text-on-surface-variant'
                  }`}
                >
                  {baglanti.metin}
                </Link>
              </li>
            ))}
          </ul>

          {/* Oturum eylemleri menunun ICINDE: baslikta yer yok ve cikisin
              bir yerde bulunabilir olmasi gerekiyor. */}
          {/* Dar ekranda acilir liste yerine duz baglanti: kucuk bir
              ekranda ic ice acilan menu parmakla kullanilamiyor. */}
          <div className="mt-stack-sm border-t border-outline-variant/40 pt-stack-sm">
            <p className="mb-base px-stack-sm font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
              Kategoriler
            </p>

            <div className="flex flex-wrap gap-base px-stack-sm">
              {(kategoriler.data ?? []).map((kategori) => (
                <Link
                  key={kategori.id}
                  to={`/?kategori=${kategori.id}`}
                  onClick={() => setMenuAcik(false)}
                  className="rounded-full border border-outline-variant px-stack-sm py-1 font-body text-body-sm text-on-surface-variant"
                >
                  {kategori.name}
                </Link>
              ))}
            </div>

            <p className="mb-base mt-stack-sm px-stack-sm font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
              Şehirler
            </p>

            <div className="flex flex-wrap gap-base px-stack-sm">
              {(sehirler.data ?? []).map((sehir) => (
                <Link
                  key={sehir.id}
                  to={`/?sehir=${sehir.id}`}
                  onClick={() => setMenuAcik(false)}
                  className="rounded-full border border-outline-variant px-stack-sm py-1 font-body text-body-sm text-on-surface-variant"
                >
                  {sehir.name}
                </Link>
              ))}
            </div>
          </div>

          <div className="mt-stack-sm border-t border-outline-variant/40 pt-stack-sm">
            {girisYapildi ? (
              <button
                type="button"
                onClick={() => {
                  setMenuAcik(false);
                  void cikis();
                }}
                className="w-full rounded-md border border-outline px-stack-sm py-stack-sm font-body text-body-md text-on-surface"
              >
                Çıkış yap
              </button>
            ) : (
              <div className="flex gap-stack-sm">
                <Link
                  to="/giris"
                  onClick={() => setMenuAcik(false)}
                  className="flex-1 rounded-md border border-outline px-stack-sm py-stack-sm text-center font-body text-body-md text-on-surface"
                >
                  Giriş yap
                </Link>
                <Link
                  to="/kayit"
                  onClick={() => setMenuAcik(false)}
                  className="flex-1 rounded-md bg-primary px-stack-sm py-stack-sm text-center font-body text-body-md font-semibold text-on-primary"
                >
                  Kayıt ol
                </Link>
              </div>
            )}
          </div>
        </nav>
      )}
    </header>
  );
}
