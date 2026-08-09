import { useState } from 'react';
import { type AdminOdeme } from '../../../api/admin';
import { para } from '../../../lib/bicim';

/**
 * Havale onay/ret penceresi.
 *
 * <b>Iki karar tek bilesende</b> cunku ikisi de ayni kaydin ayni alanina
 * bakip aciklama kodunu dogruluyor; ayri pencereler yazilsaydi o kod iki
 * yerde gosterilir ve biri degistiginde digeri unutulurdu.
 *
 * <para>
 * Onayda ekstre numarasi ISTEGE BAGLI, redde sebep ZORUNLU. Fark bilincli:
 * onay parayi gordugunun beyani, ret ise koltuklari geri alan ve musteriye
 * bildirim gonderen bir karar — gerekcesiz kayda gecmemeli.
 * </para>
 */
export function HavaleKarariOnayi({
  odeme,
  onay,
  bekliyor,
  hata,
  onIptal,
  onGonder,
}: {
  odeme: AdminOdeme;
  onay: boolean;
  bekliyor: boolean;
  hata: string | null;
  onIptal: () => void;
  onGonder: (metin: string) => void;
}) {
  const [metin, setMetin] = useState('');

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/60 px-container-margin-mobile">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="havale-basligi"
        className="w-full max-w-md rounded-lg border border-outline-variant/40 bg-surface-container p-stack-md"
      >
        <h2 id="havale-basligi" className="font-headline text-title-lg text-on-surface">
          {onay ? 'Havale onayı' : 'Havale reddi'}
        </h2>

        <p className="mt-base font-body text-body-md text-on-surface">
          {odeme.userFullName} · {para(odeme.amount, odeme.currency)}
        </p>
        <p className="font-body text-body-sm text-on-surface-variant">{odeme.eventTitle}</p>

        {/* Aciklama kodu one cikiyor: yonetici ekstredeki hareketi bununla
            esliyor ve onaylamadan once gozuyle dogrulamasi gereken tek sey. */}
        {odeme.providerReference && (
          <p className="mt-stack-sm rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface-variant">
            Ekstrede aranacak açıklama:{' '}
            <code className="font-mono font-semibold text-primary">
              {odeme.providerReference}
            </code>
          </p>
        )}

        <p
          className={`mt-stack-sm rounded-md border px-stack-sm py-base font-body text-body-sm ${
            onay
              ? 'border-primary/40 bg-primary-container/15 text-primary'
              : 'border-tertiary/40 bg-tertiary-container/10 text-tertiary'
          }`}
        >
          {onay
            ? 'Biletler üretilecek ve koltuklar satılmış sayılacak. Geri almanın yolu iade akışı.'
            : 'Rezervasyon iptal edilecek ve koltuklar hemen satışa dönecek.'}
        </p>

        <label
          htmlFor="havale-metin"
          className="mt-stack-sm block font-body text-body-sm text-on-surface-variant"
        >
          {onay ? 'Ekstredeki işlem numarası (isteğe bağlı)' : 'Ret sebebi'}
        </label>
        <textarea
          id="havale-metin"
          value={metin}
          onChange={(olay) => setMetin(olay.target.value)}
          rows={onay ? 2 : 3}
          className="mt-base w-full rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface"
          placeholder={onay ? 'Örn. FT2026080712345' : 'Örn. süre doldu, ödeme ulaşmadı'}
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
            onClick={() => onGonder(metin.trim())}
            disabled={bekliyor || (!onay && metin.trim().length === 0)}
            className={`rounded-md px-stack-md py-base font-body text-body-sm font-semibold disabled:opacity-50 ${
              onay ? 'bg-primary text-on-primary' : 'bg-error text-on-error'
            }`}
          >
            {bekliyor ? 'İşleniyor' : onay ? 'Onayla' : 'Reddet'}
          </button>
        </div>
      </div>
    </div>
  );
}
