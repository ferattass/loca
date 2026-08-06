import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';

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
export function SiteBasligi() {
  const navigate = useNavigate();
  const { kullanici, refreshToken, oturumKapat } = useAuthStore();
  const [menuAcik, setMenuAcik] = useState(false);

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

  const baglantilar = [
    { yol: '/', metin: 'Keşfet', gorunur: true },
    { yol: '/rezervasyonlarim', metin: 'Rezervasyonlarım', gorunur: girisYapildi },
    { yol: '/biletlerim', metin: 'Biletlerim', gorunur: girisYapildi },
    { yol: '/hesabim', metin: 'Hesabım', gorunur: girisYapildi },
    { yol: '/etkinlikler/yeni', metin: 'Etkinlik oluştur', gorunur: organizatorMu },
    { yol: '/kapi', metin: 'Kapı', gorunur: organizatorMu },
    // Yonetim sayfalari tek baglantiya indi: Mekanlar ve Planlar artik
    // yonetim kabugunun kendi menusunde. Ust menu sekiz baglantiya
    // ciktiginda hangisinin ne oldugu ancak okunarak anlasiliyordu.
    { yol: '/yonetim', metin: 'Yönetim', gorunur: adminMi },
  ].filter((baglanti) => baglanti.gorunur);

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

        {/* Genis ekranda yatay menu, dar ekranda acilir menu. Tasarimdaki
            arama kutusu HENUZ YOK: arama ucu Gun 8'de geliyor, calismayan
            bir kutu koymak kullaniciyi yaniltirdi. */}
        <nav aria-label="Ana menü" className="hidden flex-1 items-center gap-stack-sm md:flex">
          {baglantilar.map((baglanti) => (
            <NavLink
              key={baglanti.yol}
              to={baglanti.yol}
              end={baglanti.yol === '/'}
              className={({ isActive }) =>
                `rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
                  isActive
                    ? 'bg-primary-container/25 text-primary'
                    : 'text-on-surface-variant hover:text-on-surface'
                }`
              }
            >
              {baglanti.metin}
            </NavLink>
          ))}
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
            className="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-outline text-on-surface transition-colors hover:bg-surface-container-high md:hidden"
          >
            {menuAcik ? <CarpiIkonu className="h-5 w-5" /> : <MenuIkonu className="h-5 w-5" />}
          </button>
        </div>
      </div>

      {menuAcik && (
        <nav
          id="mobil-menu"
          aria-label="Ana menü"
          className="border-t border-outline-variant/40 px-container-margin-mobile py-stack-sm md:hidden"
        >
          {girisYapildi && (
            <p className="mb-base px-stack-sm font-body text-body-sm text-on-surface-variant">
              {kullanici?.fullName}
            </p>
          )}

          <ul className="flex flex-col">
            {baglantilar.map((baglanti) => (
              <li key={baglanti.yol}>
                <NavLink
                  to={baglanti.yol}
                  end={baglanti.yol === '/'}
                  onClick={() => setMenuAcik(false)}
                  // Dokunma hedefi metin yuksekligi kadar degil, parmakla
                  // secilebilecek kadar buyuk.
                  className={({ isActive }) =>
                    `block rounded-md px-stack-sm py-stack-sm font-body text-body-md transition-colors ${
                      isActive
                        ? 'bg-primary-container/25 font-semibold text-primary'
                        : 'text-on-surface-variant'
                    }`
                  }
                >
                  {baglanti.metin}
                </NavLink>
              </li>
            ))}
          </ul>

          {/* Oturum eylemleri menunun ICINDE: baslikta yer yok ve cikisin
              bir yerde bulunabilir olmasi gerekiyor. */}
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
