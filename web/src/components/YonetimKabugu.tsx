import { useState } from 'react';
import { NavLink, Outlet } from 'react-router-dom';

import { Logo } from './ui/Logo';

import { useAuthStore } from '../stores/authStore';
import { SolOkIkonu } from './ui/Ikon';

interface MenuGrubu {
  baslik: string;
  baglantilar: { yol: string; metin: string }[];
}

/**
 * Yonetim menusu.
 *
 * Gruplanmis: duz bir liste sekiz baglantiya ciktiginda hangisinin ne ise
 * yaradigi ancak okunarak anlasiliyordu. Basliklar "ne yonetiyorum"
 * sorusuna gore ayrildi — para, katalog, sistem.
 */
const MENU: MenuGrubu[] = [
  {
    baslik: 'İşletme',
    baglantilar: [
      { yol: '/yonetim', metin: 'Özet' },
      { yol: '/yonetim/odemeler', metin: 'Ödemeler' },
      { yol: '/yonetim/kullanicilar', metin: 'Kullanıcılar' },
    ],
  },
  {
    baslik: 'Katalog',
    baglantilar: [
      { yol: '/yonetim/mekanlar', metin: 'Mekânlar' },
      { yol: '/yonetim/oturma-planlari', metin: 'Oturma planları' },
    ],
  },
  {
    baslik: 'Sistem',
    baglantilar: [
      { yol: '/yonetim/sistem', metin: 'Sistem ve kuyruk' },
      { yol: '/yonetim/ayarlar', metin: 'Posta ayarları' },
    ],
  },
];

/**
 * Yonetim panelinin kabugu.
 *
 * <b>Site kabugundan AYRI.</b> Yonetim ekranlari gunluk kullanicinin
 * gordugu ust menuyle karisirsa iki sey oluyor: ust menu sekiz baglantiya
 * cikip okunamaz hâle geliyor, ve yonetici hangi baglamda oldugunu
 * kaybediyor. Ayri kabuk "buradaki her sey yonetim" sinyalini kendiliginden
 * veriyor.
 *
 * <para>
 * Bu bir GUVENLIK SINIRI DEGIL: rota <c>RoleRoute</c> ile sariliyor ve asil
 * kontrol sunucudaki <c>AdminOnly</c> policy'sinde. Kabuk yalnizca
 * gezinmeyi duzenliyor.
 * </para>
 */
export function YonetimKabugu() {
  const { kullanici } = useAuthStore();
  const [menuAcik, setMenuAcik] = useState(false);

  return (
    <div className="min-h-screen bg-surface-container-lowest">
      <header className="sticky top-0 z-50 border-b border-outline-variant/40 bg-surface-container-lowest/95 backdrop-blur">
        <div className="flex items-center gap-stack-sm px-container-margin-mobile py-stack-sm md:px-stack-md">
          <NavLink to="/" className="flex items-center gap-base">
            <Logo bicim="yatay" className="h-7 w-auto" />
          </NavLink>

          <span className="rounded-full border border-outline-variant px-stack-sm py-[2px] font-body text-[11px] uppercase tracking-widest text-on-surface-variant">
            Yönetim
          </span>

          <div className="ml-auto flex items-center gap-stack-sm">
            <span className="hidden font-body text-body-sm text-on-surface-variant sm:inline">
              {kullanici?.fullName}
            </span>

            {/* Siteye donus baglantisi: yonetici ayni zamanda kullanici ve
                paneli test ederken sik sik on tarafa geciyor. */}
            <NavLink
              to="/"
              className="inline-flex items-center gap-base rounded-full border border-outline px-stack-sm py-1 font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
            >
              <SolOkIkonu className="h-4 w-4" />
              Siteye dön
            </NavLink>

            <button
              type="button"
              aria-expanded={menuAcik}
              aria-controls="yonetim-menu"
              onClick={() => setMenuAcik((acik) => !acik)}
              className="rounded-md border border-outline px-stack-sm py-1 font-body text-body-sm text-on-surface md:hidden"
            >
              Menü
            </button>
          </div>
        </div>
      </header>

      <div className="flex">
        <nav
          id="yonetim-menu"
          aria-label="Yönetim menüsü"
          className={`${
            menuAcik ? 'block' : 'hidden'
          } w-full shrink-0 border-b border-outline-variant/40 px-container-margin-mobile py-stack-sm md:sticky md:top-[57px] md:block md:h-[calc(100vh-57px)] md:w-56 md:border-b-0 md:border-r md:px-stack-sm md:py-stack-md`}
        >
          {MENU.map((grup) => (
            <div key={grup.baslik} className="mb-stack-md">
              <p className="mb-base px-base font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
                {grup.baslik}
              </p>

              <ul className="space-y-[2px]">
                {grup.baglantilar.map((baglanti) => (
                  <li key={baglanti.yol}>
                    <NavLink
                      to={baglanti.yol}
                      // Ozet kok yolda; end olmadan diger tum yonetim
                      // sayfalarinda da secili gorunurdu.
                      end={baglanti.yol === '/yonetim'}
                      onClick={() => setMenuAcik(false)}
                      className={({ isActive }) =>
                        `block rounded-md px-base py-base font-body text-body-sm transition-colors ${
                          isActive
                            ? 'bg-primary-container/25 font-semibold text-primary'
                            : 'text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface'
                        }`
                      }
                    >
                      {baglanti.metin}
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </nav>

        <main className="min-w-0 flex-1 px-container-margin-mobile py-stack-md md:px-stack-md">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
