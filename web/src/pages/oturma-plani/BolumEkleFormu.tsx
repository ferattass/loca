import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { hataMesaji } from '../../api/client';
import { bolumEkle } from '../../api/seatLayoutAdmin';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';

// --- Bolum ekleme (onay ister) ----------------------------------------------

export function BolumEkleFormu({
  planId,
  salonId,
  onHata,
}: {
  planId: string;
  salonId: string;
  onHata: (mesaj: string | null) => void;
}) {
  const queryClient = useQueryClient();
  const [ad, setAd] = useState('');
  const [sira, setSira] = useState('');
  const [onayBekleyen, setOnayBekleyen] = useState<{ name: string; displayOrder: number } | null>(
    null,
  );

  const ekle = useMutation({
    mutationFn: (istek: { name: string; displayOrder: number }) => bolumEkle(planId, istek),
    onSuccess: async () => {
      setOnayBekleyen(null);
      setAd('');
      setSira('');
      onHata(null);

      // Sectionlar hem plan detayinda (koltuk sayimi icin) hem de plan
      // listesinde (sectionCount rozeti icin) gorunuyor; ikisi de tazelenir.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['seat-layout', planId] }),
        queryClient.invalidateQueries({ queryKey: ['admin-seat-layouts', salonId] }),
      ]);
    },
    onError: (h) => {
      setOnayBekleyen(null);
      onHata(hataMesaji(h, 'Bölüm eklenemedi.'));
    },
  });

  if (onayBekleyen) {
    return (
      <div className="flex flex-wrap items-center gap-stack-sm rounded-md border border-outline-variant/40 bg-surface-variant/30 px-stack-sm py-base">
        <span className="font-body text-body-sm text-on-surface">
          "{onayBekleyen.name}" adında yeni bir bölüm eklenecek. Onaylıyor musun?
        </span>
        <Button type="button" yukleniyor={ekle.isPending} onClick={() => ekle.mutate(onayBekleyen)}>
          Evet, ekle
        </Button>
        <Button type="button" gorunum="sade" onClick={() => setOnayBekleyen(null)}>
          Vazgeç
        </Button>
      </div>
    );
  }

  return (
    <form
      className="flex flex-wrap items-end gap-stack-sm"
      onSubmit={(olay) => {
        olay.preventDefault();
        onHata(null);
        setOnayBekleyen({ name: ad, displayOrder: Number(sira) });
      }}
    >
      <TextField etiket="Bölüm adı" value={ad} required onChange={(o) => setAd(o.target.value)} />
      <TextField
        etiket="Görüntülenme sırası"
        type="number"
        min={0}
        value={sira}
        required
        ipucu="Bölümlerin ekranda hangi sırayla listeleneceğini belirler."
        onChange={(o) => setSira(o.target.value)}
      />
      <Button type="submit" gorunum="cizgili">
        Bölüm ekle
      </Button>
    </form>
  );
}

// --- Koltuk uretimi ----------------------------------------------------------
