import { useEffect, useRef, useState } from 'react';

/**
 * Silme dugmesi — tek tiklamayla islem tetiklemez.
 *
 * Ilk tiklama yalnizca onay metnini acar; gercek istek ikinci tiklamada
 * gider. Istek bittiginde (basarili ya da basarisiz) onay kapanir ki
 * kullanici basarisiz bir denemeden sonra kutuda takili kalmasin.
 */
export function SilmeButonu({
  etiket,
  onayMetni,
  yukleniyor,
  onOnayla,
}: {
  etiket: string;
  onayMetni: string;
  yukleniyor: boolean;
  onOnayla: () => void;
}) {
  const [onayBekliyor, setOnayBekliyor] = useState(false);
  const oncekiYukleniyor = useRef(yukleniyor);

  useEffect(() => {
    if (oncekiYukleniyor.current && !yukleniyor) {
      setOnayBekliyor(false);
    }

    oncekiYukleniyor.current = yukleniyor;
  }, [yukleniyor]);

  if (onayBekliyor) {
    return (
      <div className="flex flex-wrap items-center gap-stack-sm">
        <span className="font-body text-body-sm text-on-surface-variant">{onayMetni}</span>
        <button
          type="button"
          onClick={onOnayla}
          disabled={yukleniyor}
          className="rounded-md bg-error px-stack-sm py-base font-body text-body-sm font-semibold text-on-error disabled:opacity-60"
        >
          {yukleniyor ? 'Siliniyor' : 'Evet, sil'}
        </button>
        <button
          type="button"
          onClick={() => setOnayBekliyor(false)}
          className="font-body text-body-sm text-primary underline underline-offset-2"
        >
          Vazgeç
        </button>
      </div>
    );
  }

  return (
    <button
      type="button"
      onClick={() => setOnayBekliyor(true)}
      className="font-body text-body-sm text-error underline underline-offset-2"
    >
      {etiket}
    </button>
  );
}
