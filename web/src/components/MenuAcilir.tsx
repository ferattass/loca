import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';

export interface AcilirSecenek {
  id: string;
  metin: string;
  yol: string;
}

/**
 * Ust menudeki acilir liste (Kategoriler, Şehirler).
 *
 * <b>Menu ust menuye SIGMIYORDU.</b> Alti kategori ve bes sehir duz
 * baglanti olarak eklenseydi cubuk on dort maddeye cikar ve dar ekranda
 * tasardi. Acilir liste ikisini iki maddeye indiriyor.
 *
 * <para>
 * Gercek bir <c>button</c> ve gercek <c>a</c> etiketleri: hover ile acilan
 * bir CSS menusu klavyeyle kullanilamaz ve dokunmatik ekranda hic
 * acilmaz. Escape kapatiyor, disariya tiklamak kapatiyor.
 * </para>
 */
export function MenuAcilir({
  baslik,
  secenekler,
  aktif,
}: {
  baslik: string;
  secenekler: AcilirSecenek[];
  aktif: boolean;
}) {
  const [acik, setAcik] = useState(false);
  const kutu = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!acik) return;

    const disariTikla = (olay: MouseEvent) => {
      if (kutu.current && !kutu.current.contains(olay.target as Node)) setAcik(false);
    };

    const kacisTusu = (olay: KeyboardEvent) => {
      if (olay.key === 'Escape') setAcik(false);
    };

    document.addEventListener('mousedown', disariTikla);
    document.addEventListener('keydown', kacisTusu);

    return () => {
      document.removeEventListener('mousedown', disariTikla);
      document.removeEventListener('keydown', kacisTusu);
    };
  }, [acik]);

  if (secenekler.length === 0) return null;

  return (
    <div ref={kutu} className="relative">
      <button
        type="button"
        onClick={() => setAcik((onceki) => !onceki)}
        aria-expanded={acik}
        aria-haspopup="menu"
        className={`inline-flex items-center gap-1 whitespace-nowrap rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
          aktif
            ? 'bg-primary-container/25 text-primary'
            : 'text-on-surface-variant hover:text-on-surface'
        }`}
      >
        {baslik}
        <OkIkonu acik={acik} />
      </button>

      {acik && (
        <div
          role="menu"
          className="absolute left-0 top-full z-50 mt-1 min-w-44 rounded-lg border border-outline-variant/60 bg-surface-container p-1 shadow-lg"
        >
          {secenekler.map((secenek) => (
            <Link
              key={secenek.id}
              to={secenek.yol}
              role="menuitem"
              onClick={() => setAcik(false)}
              className="block rounded-md px-stack-sm py-base font-body text-body-sm text-on-surface-variant transition-colors hover:bg-surface-container-high hover:text-on-surface"
            >
              {secenek.metin}
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

function OkIkonu({ acik }: { acik: boolean }) {
  return (
    <svg
      viewBox="0 0 20 20"
      aria-hidden="true"
      className={`h-3.5 w-3.5 transition-transform ${acik ? 'rotate-180' : ''}`}
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="m5 8 5 5 5-5" />
    </svg>
  );
}
