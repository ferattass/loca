import { useCallback, useState } from 'react';
import { QRCodeSVG } from 'qrcode.react';

interface QrKodProps {
  kod: string;
}

/**
 * Bilet dogrulama kodunu QR olarak cizer.
 *
 * <b>SVG, canvas degil.</b> Bilet ekranda buyutulup kucultuluyor ve
 * telefondan okutuluyor; canvas sabit cozunurlukte oldugu icin
 * buyutuldugunde bulaniklasir ve okuyucu deseni secemez. SVG her olcekte
 * keskin kaliyor.
 *
 * <para>
 * Kodun kendisi QR'in YANINDA metin olarak da duruyor. QR okuyamayan bir
 * durumda (kamera izni yok, ekran cizik, okuyucu bozuk) gorevliye
 * okunabilecek bir yedek olmadan bilet ise yaramaz hale gelirdi. Kod zaten
 * karistirilabilen harfleri icermeyen bir alfabeden uretiliyor.
 * </para>
 *
 * <para>
 * QR uretimi ISTEMCIDE yapiliyor: kod zaten sunucudan geliyor, gorseli de
 * sunucudan istemek her bilet icin fazladan bir istek demek olurdu.
 * </para>
 */
export function QrKod({ kod }: QrKodProps) {
  const [kopyalandi, setKopyalandi] = useState(false);

  const kopyala = useCallback(() => {
    // Panoya erisim reddedilirse metin zaten ekranda secilebilir durumda;
    // kullanici elle de kopyalayabildigi icin hata burada sessizce yutuluyor.
    navigator.clipboard
      .writeText(kod)
      .then(() => {
        setKopyalandi(true);
        window.setTimeout(() => setKopyalandi(false), 2000);
      })
      .catch(() => undefined);
  }, [kod]);

  return (
    <div className="rounded-md border border-outline-variant bg-surface-variant/30 p-stack-sm">
      <div className="flex flex-col items-center gap-stack-sm sm:flex-row sm:items-start">
        {/*
          QR BEYAZ zemin uzerinde. Koyu tema renkleriyle cizilseydi kontrast
          orani okuyucular icin yetersiz kalirdi; okuyucu temaya degil
          desenin kontrastina bakiyor.
        */}
        <div className="shrink-0 rounded-md bg-white p-stack-sm">
          <QRCodeSVG
            value={kod}
            size={132}
            // Hata duzeltme seviyesi ortada: daha yuksegi deseni gereksiz
            // siklastirip kucuk ekranda okumayi zorlastiriyor.
            level="M"
            marginSize={0}
            title="Bilet QR kodu"
          />
        </div>

        <div className="min-w-0 flex-1">
          <p className="font-body text-body-sm text-on-surface-variant">
            Girişte bu kod okutulacak
          </p>

          <p className="mt-base select-all break-all rounded-md bg-surface-container-low px-stack-sm py-base font-mono text-body-sm tracking-[0.15em] text-on-surface">
            {kod}
          </p>

          <button
            type="button"
            onClick={kopyala}
            className="mt-base rounded-md border border-outline-variant px-stack-sm py-base font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
          >
            {kopyalandi ? 'Kopyalandı' : 'Kodu kopyala'}
          </button>
        </div>
      </div>
    </div>
  );
}
