import { useState } from 'react';
import { Link } from 'react-router-dom';


/**
 * Havale talimati.
 *
 * <b>Aciklama kodu en one cikiyor:</b> yonetici gelen ekstreyi bu kodla
 * esliyor ve kod yazilmadan gonderilen para hangi rezervasyona ait
 * bulunamiyor. Kod kopyalanabilir bir dugmeye bagli, cunku elle yazilirken
 * bir karakter kaybolmasi odemenin eslesmemesi demek.
 */
export function HavaleTalimatiKarti({
  talimat,
  kod,
  tutar,
  sonOdeme,
}: {
  talimat: { bankName: string; accountName: string; iban: string; deadlineHours: number } | null;
  kod: string | null;
  tutar: string;
  sonOdeme: number;
}) {
  const [kopyalandi, setKopyalandi] = useState(false);

  const kopyala = async (metin: string) => {
    try {
      await navigator.clipboard.writeText(metin);
      setKopyalandi(true);
      window.setTimeout(() => setKopyalandi(false), 2000);
    } catch {
      // Pano izni yoksa sessiz kaliyor: metin zaten ekranda ve secilebilir,
      // kullaniciya "kopyalanamadi" demenin bir faydasi yok.
    }
  };

  return (
    <section
      aria-label="Havale bilgileri"
      className="space-y-stack-sm rounded-lg border border-primary/40 bg-primary-container/10 p-stack-sm"
    >
      <p className="font-body text-body-md font-semibold text-on-surface">
        Havale bilgileri hazır — ödeme bekleniyor
      </p>

      {talimat ? (
        <dl className="space-y-base font-body text-body-sm">
          <Satir etiket="Banka" deger={talimat.bankName} />
          <Satir etiket="Hesap sahibi" deger={talimat.accountName} />
          <Satir etiket="IBAN" deger={talimat.iban.replace(/(.{4})/g, '$1 ').trim()} tekAralik />
          <Satir etiket="Tutar" deger={tutar} />
        </dl>
      ) : (
        <p className="font-body text-body-sm text-on-surface-variant">
          Banka bilgileri şu an alınamadı. Sayfayı yenile.
        </p>
      )}

      {kod && (
        <div className="rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base">
          <p className="font-body text-body-sm text-on-surface-variant">
            Havale açıklamasına mutlaka bu kodu yaz:
          </p>
          <div className="mt-base flex flex-wrap items-center gap-stack-sm">
            <code className="font-mono text-body-md font-semibold text-primary">{kod}</code>
            <button
              type="button"
              onClick={() => void kopyala(kod)}
              className="rounded-full border border-outline px-stack-sm py-1 font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
            >
              {kopyalandi ? 'Kopyalandı' : 'Kopyala'}
            </button>
          </div>
          <p className="mt-base font-body text-body-sm text-on-surface-variant/70">
            Kod yazılmazsa ödemen hangi rezervasyona ait olduğu anlaşılamaz ve onay gecikir.
          </p>
        </div>
      )}

      <p className="font-body text-body-sm text-on-surface-variant">
        {sonOdeme > 0
          ? `Koltukların ${Math.max(1, Math.round(sonOdeme / 3600))} saat daha tutuluyor. `
          : 'Ödeme süresi doldu. '}
        Ödemen hesaba geçtiğinde yönetim onaylar ve biletlerin{' '}
        <Link to="/biletlerim" className="text-primary underline underline-offset-2">
          Biletlerim
        </Link>{' '}
        sayfasına düşer.
      </p>
    </section>
  );
}

function Satir({
  etiket,
  deger,
  tekAralik,
}: {
  etiket: string;
  deger: string;
  tekAralik?: boolean;
}) {
  return (
    <div className="flex flex-wrap items-baseline justify-between gap-base">
      <dt className="text-on-surface-variant">{etiket}</dt>
      <dd className={`text-on-surface ${tekAralik ? 'font-mono' : 'font-semibold'}`}>{deger}</dd>
    </div>
  );
}
