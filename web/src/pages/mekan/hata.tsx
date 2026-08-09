import { dogrulamaHatalari, hataMesaji } from '../../api/client';

// --- Ortak yardimcilar ------------------------------------------------------

export interface HataYapisi {
  ozet: string;
  liste: string[];
}

export function hataYapisiOlustur(hata: unknown, varsayilan: string): HataYapisi {
  return { ozet: hataMesaji(hata, varsayilan), liste: dogrulamaHatalari(hata) };
}

export function HataKutusu({ hata }: { hata: HataYapisi | null }) {
  if (!hata) return null;

  return (
    <div
      role="alert"
      className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
    >
      <p>{hata.ozet}</p>
      {/* Tek mesaj zaten ozette gorunuyor; liste yalnizca birden fazla alan
          hatali oldugunda ek bilgi tasir. */}
      {hata.liste.length > 1 && (
        <ul className="mt-base list-disc space-y-[2px] pl-stack-md">
          {hata.liste.map((mesaj) => (
            <li key={mesaj}>{mesaj}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
