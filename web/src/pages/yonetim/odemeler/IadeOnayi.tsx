import { useState } from 'react';
import { type AdminOdeme } from '../../../api/admin';
import { para } from '../../../lib/bicim';

/**
 * Iade onayi.
 *
 * <b>Sebep zorunlu.</b> Iade geri alinamayan bir islem: biletler iptal
 * oluyor ve koltuklar satisa donuyor. Sebep yazilmadan yapilabilseydi
 * aylar sonra "bu neden iade edilmis" sorusunun cevabi hicbir yerde
 * olmazdi.
 */
export function IadeOnayi({
  odeme,
  bekliyor,
  hata,
  onIptal,
  onOnay,
}: {
  odeme: AdminOdeme;
  bekliyor: boolean;
  hata: string | null;
  onIptal: () => void;
  onOnay: (sebep: string) => void;
}) {
  const [sebep, setSebep] = useState('');

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/60 px-container-margin-mobile">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="iade-basligi"
        className="w-full max-w-md rounded-lg border border-outline-variant/40 bg-surface-container p-stack-md"
      >
        <h2 id="iade-basligi" className="font-headline text-title-lg text-on-surface">
          İade onayı
        </h2>

        <p className="mt-base font-body text-body-md text-on-surface">
          {odeme.userFullName} · {para(odeme.amount, odeme.currency)}
        </p>
        <p className="font-body text-body-sm text-on-surface-variant">{odeme.eventTitle}</p>

        <p className="mt-stack-sm rounded-md border border-tertiary/40 bg-tertiary-container/10 px-stack-sm py-base font-body text-body-sm text-tertiary">
          Biletler iptal edilecek ve koltuklar satışa dönecek. Bu işlem geri alınamaz.
        </p>

        <label htmlFor="iade-sebebi" className="mt-stack-sm block font-body text-body-sm text-on-surface-variant">
          İade sebebi
        </label>
        <textarea
          id="iade-sebebi"
          value={sebep}
          onChange={(olay) => setSebep(olay.target.value)}
          rows={3}
          className="mt-base w-full rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface"
          placeholder="Örn. etkinlik iptal edildi"
        />

        {hata && (
          <p role="alert" className="mt-base font-body text-body-sm text-error">
            {hata}
          </p>
        )}

        <div className="mt-stack-sm flex justify-end gap-base">
          <button
            type="button"
            onClick={onIptal}
            className="rounded-md border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface"
          >
            Vazgeç
          </button>
          <button
            type="button"
            onClick={() => onOnay(sebep.trim())}
            disabled={bekliyor || sebep.trim().length === 0}
            className="rounded-md bg-error px-stack-md py-base font-body text-body-sm font-semibold text-on-error disabled:opacity-50"
          >
            {bekliyor ? 'İade ediliyor' : 'İade et'}
          </button>
        </div>
      </div>
    </div>
  );
}
