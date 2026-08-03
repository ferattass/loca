import { Button } from './ui/Button';

export interface SecimOzetiKoltugu {
  koltukId: string;
  etiket?: string;
  tur?: string;
  tutar?: number;
}

interface SecimOzetiPaneliProps {
  koltuklar: SecimOzetiKoltugu[];
  toplam: number;
  paraBicimi: Intl.NumberFormat;
  yukleniyor: boolean;
  onOdemeyeGec: () => void;
  onTemizle: () => void;
}

/**
 * Sag sutunda yapiskan duran secim ozeti.
 *
 * Bos ve dolu durum AYNI bilesende: kullanici hicbir koltuk secmeden once
 * bile panelin nerede oldugunu ve ne bekledigini gormeli. Yalnizca secim
 * varken cizilseydi panel her koltuk secildiginde aniden belirir, yerlesim
 * ziplardi.
 */
export function SecimOzetiPaneli({
  koltuklar,
  toplam,
  paraBicimi,
  yukleniyor,
  onOdemeyeGec,
  onTemizle,
}: SecimOzetiPaneliProps) {
  const bosMu = koltuklar.length === 0;

  return (
    <section aria-label="Seçimin" className="glass rounded-lg p-stack-sm md:p-stack-md">
      <h2 className="flex items-center gap-base font-headline text-title-lg text-on-surface">
        <KoltukIkonu aria-hidden className="h-5 w-5 text-primary" />
        Seçimin
      </h2>

      {bosMu ? (
        <div className="flex flex-col items-center gap-stack-sm py-stack-lg text-center">
          <KoltukIkonu aria-hidden className="h-10 w-10 text-on-surface-variant/40" />
          <p className="font-body text-body-sm text-on-surface-variant">Henüz koltuk seçilmedi</p>
        </div>
      ) : (
        <>
          <ul className="mt-stack-sm space-y-base">
            {koltuklar.map((koltuk) => (
              <li
                key={koltuk.koltukId}
                className="flex items-center justify-between gap-base font-body text-body-sm text-on-surface"
              >
                <span>
                  {koltuk.etiket}
                  {koltuk.tur && <span className="ml-base text-on-surface-variant">({koltuk.tur})</span>}
                </span>
                <span className="tabular shrink-0">{paraBicimi.format(koltuk.tutar ?? 0)}</span>
              </li>
            ))}
          </ul>

          {/*
            Ara toplam ve Toplam su an AYNI degeri gosteriyor: hizmet bedeli
            satiri yok, indirim yok. Iki ayri satir olarak birakildi ki
            ileride bir indirim/kupon eklendiginde arayuz zaten hazir olsun,
            tek satirdan ikiye cikarmak o gun ayrica bir degisiklik gerektirmesin.
          */}
          <div className="mt-stack-sm space-y-base border-t border-outline-variant/30 pt-stack-sm font-body text-body-sm text-on-surface-variant">
            <div className="flex items-center justify-between">
              <span>Ara toplam</span>
              <span className="tabular">{paraBicimi.format(toplam)}</span>
            </div>
            <div className="flex items-center justify-between font-body text-body-md font-semibold text-on-surface">
              <span>Toplam</span>
              <span className="tabular">{paraBicimi.format(toplam)}</span>
            </div>
          </div>

          <Button
            type="button"
            onClick={onOdemeyeGec}
            yukleniyor={yukleniyor}
            className="mt-stack-sm w-full"
          >
            {yukleniyor ? 'Kilitleniyor' : 'Ödemeye geç'}
          </Button>

          <button
            type="button"
            onClick={onTemizle}
            className="mt-base w-full text-center font-body text-body-sm text-primary underline underline-offset-2"
          >
            Seçimi temizle
          </button>
        </>
      )}

      {/*
        Guvenlik notu istendi ama tasarimdaki "Loca Shield (tm)" ibaresi
        var olmayan bir urunu kullaniciya varmis gibi gostermek olurdu. Onun
        yerine gercek ve zaten uyulmasi gereken bir uyari kondu: gosterilen
        tutar baglayici degil.
      */}
      <p className="mt-stack-sm font-body text-[11px] text-on-surface-variant">
        Ödenecek tutar sunucuda hesaplanır; buradaki toplam önizlemedir.
      </p>
    </section>
  );
}

/** Basit koltuk cizimi. Sirt, oturma yeri ve iki ayak. */
function KoltukIkonu({ className, ...props }: { className?: string; 'aria-hidden'?: boolean }) {
  return (
    <svg
      {...props}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
    >
      <path d="M6 10V6a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v4" />
      <path d="M4 10h16v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-5Z" />
      <path d="M6 16v3" />
      <path d="M18 16v3" />
    </svg>
  );
}
