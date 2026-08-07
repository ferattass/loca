import { useEffect, useState } from 'react';

import type { EtkinlikKategorisi } from '../api/events';

/** Hizli tarih secenekleri. Adres cubugunda `ne_zaman` degeri olarak duruyor. */
export type ZamanSecimi = 'hepsi' | 'bugun' | 'yarin' | 'hafta';

const ZAMANLAR: { deger: ZamanSecimi; metin: string }[] = [
  { deger: 'hepsi', metin: 'Tüm tarihler' },
  { deger: 'bugun', metin: 'Bugün' },
  { deger: 'yarin', metin: 'Yarın' },
  { deger: 'hafta', metin: 'Bu hafta' },
];

interface KesfetSuzgeciProps {
  kategoriler: EtkinlikKategorisi[];
  seciliKategori: string | null;
  onKategori: (id: string | null) => void;
  zaman: ZamanSecimi;
  onZaman: (deger: ZamanSecimi) => void;
  arama: string;
  onArama: (deger: string) => void;
  sonucSayisi: number | null;
}

/**
 * Kesfet ekraninin suzgec cubugu.
 *
 * <b>Arama, tarih ve kategori TEK SATIRDA.</b> Onceden ekranda yalnizca
 * kategori daireleri vardi; kullanici belirli bir etkinligi ariyorsa
 * yapabilecegi tek sey listeyi gozle taramakti — oysa sunucuda hem
 * baslik aramasi hem tarih araligi suzgeci var, ikisi de arayuzden hic
 * cagrilmiyordu.
 *
 * <para>
 * Suzgecler yapiskan (sticky): liste asagi kaydirilirken kaybolmuyorlar.
 * Kaybolsalardi kullanici filtreyi degistirmek icin her seferinde en
 * yukari donmek zorunda kalirdi.
 * </para>
 */
export function KesfetSuzgeci({
  kategoriler,
  seciliKategori,
  onKategori,
  zaman,
  onZaman,
  arama,
  onArama,
  sonucSayisi,
}: KesfetSuzgeciProps) {
  // Yazilan metin bilesende, sorgu disarida: her tus vurusunda sunucuya
  // gitmemek icin arada bir gecikme var. Dogrudan baglansaydi "konser"
  // yazarken alti ayri istek giderdi.
  const [metin, setMetin] = useState(arama);

  useEffect(() => setMetin(arama), [arama]);

  useEffect(() => {
    if (metin === arama) return;

    const zamanlayici = window.setTimeout(() => onArama(metin), 350);

    return () => window.clearTimeout(zamanlayici);
  }, [metin, arama, onArama]);

  return (
    <div className="sticky top-[57px] z-40 -mx-container-margin-mobile border-b border-outline-variant/40 bg-surface-container-lowest/95 px-container-margin-mobile py-stack-sm backdrop-blur md:-mx-container-margin-desktop md:px-container-margin-desktop">
      <div className="mx-auto flex max-w-7xl flex-col gap-stack-sm">
        <div className="flex flex-wrap items-center gap-stack-sm">
          <div className="relative min-w-0 flex-1">
            <AramaIkonu />
            <input
              type="search"
              value={metin}
              onChange={(olay) => setMetin(olay.target.value)}
              placeholder="Etkinlik, sanatçı ya da mekân ara"
              aria-label="Etkinlik ara"
              className="w-full rounded-full border border-outline-variant bg-surface-container-low py-stack-sm pl-11 pr-stack-sm font-body text-body-md text-on-surface placeholder:text-on-surface-variant/60 focus:border-primary focus:outline-none"
            />
          </div>

          <div className="flex flex-wrap gap-base">
            {ZAMANLAR.map((secenek) => (
              <button
                key={secenek.deger}
                type="button"
                onClick={() => onZaman(secenek.deger)}
                aria-pressed={zaman === secenek.deger}
                className={`whitespace-nowrap rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
                  zaman === secenek.deger
                    ? 'bg-primary text-on-primary font-semibold'
                    : 'border border-outline-variant text-on-surface-variant hover:text-on-surface'
                }`}
              >
                {secenek.metin}
              </button>
            ))}
          </div>
        </div>

        {/* Kategoriler yatay serit: daire ızgarası dikeyde cok yer
            kapliyordu ve alti etkinligi ekranin altina itiyordu. Yatay
            kaydirma dar ekranda calisiyor. */}
        <div className="flex gap-base overflow-x-auto pb-1">
          <KategoriDugmesi secili={seciliKategori === null} onClick={() => onKategori(null)}>
            Tümü
          </KategoriDugmesi>

          {kategoriler.map((kategori) => (
            <KategoriDugmesi
              key={kategori.id}
              secili={seciliKategori === kategori.id}
              onClick={() => onKategori(seciliKategori === kategori.id ? null : kategori.id)}
            >
              {kategori.name}
            </KategoriDugmesi>
          ))}

          {sonucSayisi !== null && (
            <span className="ml-auto shrink-0 self-center whitespace-nowrap font-body text-body-sm text-on-surface-variant">
              {sonucSayisi} etkinlik
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

function KategoriDugmesi({
  secili,
  onClick,
  children,
}: {
  secili: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={secili}
      className={`shrink-0 whitespace-nowrap rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
        secili
          ? 'bg-primary-container/30 font-semibold text-primary'
          : 'text-on-surface-variant hover:bg-surface-container-high hover:text-on-surface'
      }`}
    >
      {children}
    </button>
  );
}

/** Arama ikonu. Emoji degil SVG: rengi temadan geliyor. */
function AramaIkonu() {
  return (
    <svg
      viewBox="0 0 24 24"
      aria-hidden="true"
      className="pointer-events-none absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-on-surface-variant/70"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
    >
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-3.5-3.5" />
    </svg>
  );
}

/**
 * Secilen zaman araliginin UTC sinirlari.
 *
 * Yerel gune gore hesaplaniyor: kullanici "bugun" derken kendi
 * takvimindeki gunu kastediyor ve Turkiye UTC+3 oldugu icin UTC gun
 * sinirlari kullanilsaydi gece 00:00-03:00 arasi etkinlikler yarina
 * duserdi.
 */
export function zamanAraligi(secim: ZamanSecimi): { bas?: string; bit?: string } {
  if (secim === 'hepsi') return {};

  const simdi = new Date();
  const gunBasi = new Date(simdi.getFullYear(), simdi.getMonth(), simdi.getDate());

  const bas = new Date(gunBasi);
  const bit = new Date(gunBasi);

  if (secim === 'bugun') {
    bit.setDate(bit.getDate() + 1);
  } else if (secim === 'yarin') {
    bas.setDate(bas.getDate() + 1);
    bit.setDate(bit.getDate() + 2);
  } else {
    bit.setDate(bit.getDate() + 7);
  }

  return { bas: bas.toISOString(), bit: bit.toISOString() };
}
