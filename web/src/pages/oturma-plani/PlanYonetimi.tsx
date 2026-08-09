import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { hataMesaji } from '../../api/client';
import { planOlustur, planSil, planlariGetir } from '../../api/seatLayoutAdmin';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';

// --- Ortak secim kutusu ----------------------------------------------------


// --- Plan listesi ve olusturma ----------------------------------------------

export function PlanYonetimi({
  salonId,
  planId,
  onPlanSec,
  onHata,
}: {
  salonId: string;
  planId: string;
  onPlanSec: (id: string) => void;
  onHata: (mesaj: string | null) => void;
}) {
  const queryClient = useQueryClient();
  const [silinecekId, setSilinecekId] = useState<string | null>(null);
  const [ad, setAd] = useState('');
  const [aciklama, setAciklama] = useState('');

  const planlar = useQuery({
    queryKey: ['admin-seat-layouts', salonId],
    queryFn: () => planlariGetir(salonId),
  });

  const olustur = useMutation({
    mutationFn: () =>
      planOlustur({ hallId: salonId, name: ad, description: aciklama === '' ? null : aciklama }),
    onSuccess: async (yeniPlanId) => {
      onHata(null);
      setAd('');
      setAciklama('');
      onPlanSec(yeniPlanId);
      await queryClient.invalidateQueries({ queryKey: ['admin-seat-layouts', salonId] });
    },
    onError: (h) => onHata(hataMesaji(h, 'Oturma planı oluşturulamadı.')),
  });

  const sil = useMutation({
    mutationFn: (id: string) => planSil(id),
    onSuccess: async (_veri, silinenId) => {
      setSilinecekId(null);
      onHata(null);

      // Silinen plan o an ekranda aciksa detay panelini kapatmak gerekir;
      // aksi halde sayfa artik var olmayan bir plani gostermeye calisirdi.
      if (silinenId === planId) onPlanSec('');

      await queryClient.invalidateQueries({ queryKey: ['admin-seat-layouts', salonId] });
    },
    onError: (h) => {
      setSilinecekId(null);
      onHata(hataMesaji(h, 'Oturma planı silinemedi.'));
    },
  });

  return (
    <div className="space-y-stack-sm">
      {planlar.isPending && (
        <p role="status" className="font-body text-body-sm text-on-surface-variant">
          Planlar yükleniyor…
        </p>
      )}

      {planlar.isError && (
        <p role="alert" className="font-body text-body-sm text-error">
          {hataMesaji(planlar.error, 'Planlar yüklenemedi.')}
        </p>
      )}

      {planlar.data && planlar.data.length > 0 && (
        <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
          {planlar.data.map((plan) => (
            <li
              key={plan.id}
              className="flex flex-wrap items-center justify-between gap-stack-sm font-body text-body-sm"
            >
              <button
                type="button"
                onClick={() => onPlanSec(plan.id)}
                className={
                  plan.id === planId
                    ? 'text-left font-semibold text-primary'
                    : 'text-left text-on-surface'
                }
              >
                {plan.name} ({plan.sectionCount} bölüm)
              </button>

              {silinecekId === plan.id ? (
                <span className="flex flex-wrap items-center gap-base">
                  <span className="text-on-surface-variant">Silinsin mi?</span>
                  <Button
                    type="button"
                    gorunum="cizgili"
                    yukleniyor={sil.isPending}
                    onClick={() => sil.mutate(plan.id)}
                  >
                    Evet, sil
                  </Button>
                  <Button type="button" gorunum="sade" onClick={() => setSilinecekId(null)}>
                    Vazgeç
                  </Button>
                </span>
              ) : (
                <Button type="button" gorunum="sade" onClick={() => setSilinecekId(plan.id)}>
                  Sil
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}

      <form
        className="flex flex-wrap items-end gap-stack-sm"
        onSubmit={(olay) => {
          olay.preventDefault();
          onHata(null);
          olustur.mutate();
        }}
      >
        <TextField
          etiket="Yeni plan adı"
          value={ad}
          required
          onChange={(o) => setAd(o.target.value)}
        />
        <TextField
          etiket="Açıklama (opsiyonel)"
          value={aciklama}
          onChange={(o) => setAciklama(o.target.value)}
        />
        <Button type="submit" gorunum="cizgili" yukleniyor={olustur.isPending}>
          Plan oluştur
        </Button>
      </form>
    </div>
  );
}
