import { useState } from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';

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
        <Link to="/" className="flex items-center gap-base">
          <span
            aria-hidden="true"
            className="grid h-8 w-8 place-items-center rounded-md bg-primary font-headline text-body-sm font-extrabold text-on-primary"
          >
            L
          </span>
          <span className="font-display text-title-lg tracking-wide text-on-surface">LOCA</span>
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
          {girisYapildi ? (
            <>
              <span className="hidden font-body text-body-sm text-on-surface-variant sm:inline">
                {kullanici?.fullName.split(' ')[0]}
              </span>
              <button
                type="button"
                onClick={cikis}
                className="rounded-full border border-outline px-stack-sm py-1 font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
              >
                Çıkış
              </button>
            </>
          ) : (
            <>
              <Link
                to="/giris"
                className="font-body text-body-sm text-on-surface-variant hover:text-on-surface"
              >
                Giriş yap
              </Link>
              <Link
                to="/kayit"
                className="rounded-full bg-primary px-stack-sm py-1 font-body text-body-sm font-semibold text-on-primary"
              >
                Kayıt ol
              </Link>
            </>
          )}

          <button
            type="button"
            aria-expanded={menuAcik}
            aria-controls="mobil-menu"
            onClick={() => setMenuAcik((acik) => !acik)}
            className="rounded-md border border-outline px-stack-sm py-1 font-body text-body-sm text-on-surface md:hidden"
          >
            Menü
          </button>
        </div>
      </div>

      {menuAcik && (
        <nav
          id="mobil-menu"
          aria-label="Ana menü"
          className="border-t border-outline-variant/40 px-container-margin-mobile py-stack-sm md:hidden"
        >
          <ul className="flex flex-col gap-base">
            {baglantilar.map((baglanti) => (
              <li key={baglanti.yol}>
                <NavLink
                  to={baglanti.yol}
                  end={baglanti.yol === '/'}
                  onClick={() => setMenuAcik(false)}
                  className="block font-body text-body-md text-on-surface-variant"
                >
                  {baglanti.metin}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </header>
  );
}
