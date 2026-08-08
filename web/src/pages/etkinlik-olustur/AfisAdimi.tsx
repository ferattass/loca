import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';

import { afisBagla, gorselYukle } from '../../api/events';
import { Button } from '../../components/ui/Button';
import { type HataBildir } from './ortak';

// --- 4 · Afiş -------------------------------------------------------------

export function AfisAdimi({
  etkinlikId,
  afisId,
  onYuklendi,
  onDevam,
  onHata,
}: {
  etkinlikId: string;
  afisId: string | null;
  onYuklendi: (dosyaId: string) => void;
  onDevam: () => void;
  onHata: HataBildir;
}) {
  const [dosya, setDosya] = useState<File | null>(null);

  const yukle = useMutation({
    mutationFn: async () => {
      const dosyaId = await gorselYukle(dosya!);
      await afisBagla(etkinlikId, dosyaId);
      return dosyaId;
    },
    onSuccess: onYuklendi,
    onError: (h) => onHata(h, 'Afiş yüklenemedi.'),
  });

  return (
    <div className="space-y-stack-sm">
      <p className="font-body text-body-sm text-on-surface-variant">
        Afiş, yayına alınmanın ön koşulu. En fazla 5 MB; tür dosyanın içeriğine bakılarak
        doğrulanır, uzantıya değil.
      </p>

      <label className="flex flex-col gap-base">
        <span className="font-body text-body-sm text-on-surface-variant">Afiş görseli</span>
        <input
          type="file"
          accept="image/png,image/jpeg,image/webp"
          onChange={(olay) => setDosya(olay.target.files?.[0] ?? null)}
          className="font-body text-body-sm text-on-surface file:mr-stack-sm file:rounded-md file:border-0 file:bg-primary file:px-stack-sm file:py-base file:font-semibold file:text-on-primary"
        />
      </label>

      {afisId && (
        <p role="status" className="font-body text-body-sm text-primary">
          Afiş yüklendi ve etkinliğe bağlandı.
        </p>
      )}

      <div className="flex flex-wrap gap-stack-sm">
        <Button
          type="button"
          gorunum="cizgili"
          disabled={dosya === null}
          yukleniyor={yukle.isPending}
          onClick={() => {
            onHata(null);
            yukle.mutate();
          }}
        >
          Yükle
        </Button>
        <Button type="button" onClick={onDevam} disabled={afisId === null}>
          Devam et
        </Button>
      </div>
    </div>
  );
}

