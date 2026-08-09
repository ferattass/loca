import { useCallback, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router-dom';

import { hataMesaji } from '../api/client';
import { koltukDurumuDegistir, planGetir, type OturmaPlani } from '../api/seatLayouts';
import { SeatMap } from '../components/SeatMap';
import { Sayfa } from '../components/ui/Sayfa';

/**
 * Oturma planinin gorsel ekrani.
 *
 * Yonetici koltuklari secip toplu olarak satisa acabilir veya kapatabilir.
 * Secim yalnizca arayuzde tutulur; kalici degisiklik ucun cagrilmasiyla olur.
 */
export function SeatLayoutPage() {
  const { id = '' } = useParams();
  const queryClient = useQueryClient();
  const [seciliIdler, setSeciliIdler] = useState<ReadonlySet<string>>(new Set());
  const [onayBekliyor, setOnayBekliyor] = useState(false);
  const [islemHatasi, setIslemHatasi] = useState<string | null>(null);

  const { data, isPending, isError, error } = useQuery<OturmaPlani>({
    queryKey: ['seat-layout', id],
    queryFn: () => planGetir(id, true),
    enabled: id.length > 0,
  });

  const durumDegistir = useMutation({
    mutationFn: async (koltukIdleri: string[]) => {
      // Uc tek koltuk aliyor; secilenler sirayla gonderiliyor.
      for (const koltukId of koltukIdleri) {
        await koltukDurumuDegistir(koltukId);
      }
    },
    onSuccess: async () => {
      setSeciliIdler(new Set());
      setOnayBekliyor(false);
      setIslemHatasi(null);
      await queryClient.invalidateQueries({ queryKey: ['seat-layout', id] });
    },
    onError: (hata) => {
      setOnayBekliyor(false);
      setIslemHatasi(hataMesaji(hata, 'Koltuk durumu degistirilemedi.'));
    },
  });

  // Referansi sabit tutuluyor: her cizimde yeni bir fonksiyon uretilseydi
  // React.memo ile sarilan koltuklarin tamami yeniden cizilir ve memo
  // hicbir ise yaramazdi.
  const koltukSec = useCallback((koltukId: string) => {
    setSeciliIdler((oncekiler) => {
      const yeni = new Set(oncekiler);

      if (yeni.has(koltukId)) {
        yeni.delete(koltukId);
      } else {
        yeni.add(koltukId);
      }

      return yeni;
    });
  }, []);

  if (isPending) {
    return (
      <Sayfa genislik="5xl">
        <div className="animate-pulse space-y-stack-sm" aria-hidden="true">
          <div className="h-6 w-64 rounded bg-surface-variant/60" />
          <div className="h-96 rounded-lg bg-surface-variant/40" />
        </div>
        <span className="sr-only" role="status">
          Oturma plani yukleniyor
        </span>
      </Sayfa>
    );
  }

  if (isError) {
    return (
      <Sayfa genislik="5xl">
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hataMesaji(error, 'Oturma plani yuklenemedi.')}
        </p>
      </Sayfa>
    );
  }

  return (
    <Sayfa genislik="5xl">
      <header className="mb-stack-md">
        <p className="font-body text-label-caps uppercase tracking-widest text-primary">
          {data.hallName}
        </p>
        <h1 className="font-headline text-headline-md text-on-surface">{data.name}</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          {data.totalSeatCount} koltuk / {data.hallCapacity} kapasite
          {data.description ? ` - ${data.description}` : ''}
        </p>
      </header>

      {seciliIdler.size > 0 && (
        <div className="mb-stack-sm flex flex-wrap items-center gap-stack-sm rounded-md border border-outline-variant/40 bg-surface-variant/30 px-stack-sm py-base">
          <p className="font-body text-body-sm text-on-surface">
            {seciliIdler.size} koltuk secildi
          </p>

          {/*
            Islem tek adimda uygulanmiyor. Yuzlerce koltugun secilebildigi bir
            ekranda tek tiklamayla toplu degisiklik yapmak, yanlislikla
            yapilan degisikligi geri almayi zorlastirir; kac koltugun
            etkilenecegi onay metninde acikca yaziyor.
          */}
          {onayBekliyor ? (
            <>
              <span className="font-body text-body-sm text-on-surface-variant">
                {seciliIdler.size} koltugun satis durumu degisecek. Onayliyor musun?
              </span>
              <button
                type="button"
                onClick={() => durumDegistir.mutate([...seciliIdler])}
                disabled={durumDegistir.isPending}
                className="rounded-md bg-primary px-stack-sm py-base font-body text-body-sm font-semibold text-on-primary disabled:opacity-60"
              >
                {durumDegistir.isPending ? 'Uygulaniyor' : 'Evet, degistir'}
              </button>
              <button
                type="button"
                onClick={() => setOnayBekliyor(false)}
                className="font-body text-body-sm text-primary underline underline-offset-2"
              >
                Vazgec
              </button>
            </>
          ) : (
            <>
              <button
                type="button"
                onClick={() => setOnayBekliyor(true)}
                className="rounded-md bg-primary px-stack-sm py-base font-body text-body-sm font-semibold text-on-primary"
              >
                Satis durumunu degistir
              </button>
              <button
                type="button"
                onClick={() => setSeciliIdler(new Set())}
                className="font-body text-body-sm text-primary underline underline-offset-2"
              >
                Secimi temizle
              </button>
            </>
          )}
        </div>
      )}

      {islemHatasi && (
        <p
          role="alert"
          className="mb-stack-sm rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
        >
          {islemHatasi}
        </p>
      )}

      <SeatMap bolumler={data.sections} seciliIdler={seciliIdler} onKoltukSec={koltukSec} />
    </Sayfa>
  );
}

