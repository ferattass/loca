import { useCallback, useState } from 'react';

interface QrKodProps {
  kod: string;
}

/**
 * Bilet dogrulama kodunu okunabilir bicimde gosterir.
 *
 * TODO: projede QR gorseli ureten bir kutuphane yok ve buraya eklenmesi
 * istenmedi. Kod bu yuzden GERCEK BIR QR GORSELI DEGIL, harf araligiyla
 * yazilmis secilebilir metindir. Gercek QR gorseli eklenince yalnizca bu
 * bilesenin ici degisecek; disariya verdigi `kod` prop'u ayni kalabilir.
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
    <div className="rounded-md border border-outline-variant bg-surface-variant/30 px-stack-sm py-stack-sm">
      <p className="font-body text-body-sm text-on-surface-variant">
        Girişte bu kod okutulacak
      </p>

      <p className="mt-base select-all break-all rounded-md bg-surface-container-low px-stack-sm py-base font-mono text-body-md tracking-[0.25em] text-on-surface">
        {kod}
      </p>

      <button
        type="button"
        onClick={kopyala}
        className="mt-stack-sm rounded-md border border-outline-variant px-stack-sm py-base font-body text-body-sm text-on-surface disabled:opacity-40"
      >
        {kopyalandi ? 'Kopyalandı' : 'Kodu kopyala'}
      </button>
    </div>
  );
}
