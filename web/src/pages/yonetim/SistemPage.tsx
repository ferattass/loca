import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { hataMesaji } from '../../api/client';
import {
  kuyrukGetir,
  mesajiKuyrugaKoy,
  ozetGetir,
  type AdminOzeti,
  type KuyrukFiltresi,
  type KuyrukMesaji,
} from '../../api/admin';
import { BosDurum } from '../../components/ui/BosDurum';
import { UyariIkonu } from '../../components/ui/Ikon';
import { Iskelet } from '../../components/ui/Iskelet';
import { Uyari } from '../../components/ui/Uyari';

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'short',
  timeStyle: 'medium',
});

const SEKMELER: { deger: KuyrukFiltresi; metin: string }[] = [
  { deger: 'Pending', metin: 'Bekleyen' },
  { deger: 'Retryable', metin: 'Yeniden denenecek' },
  { deger: 'DeadLettered', metin: 'Ölü mektup' },
  { deger: 'Processed', metin: 'İşlenmiş' },
];

/**
 * Sistem ve kuyruk ekrani.
 *
 * Hangfire'in kendi panosu ayri bir adreste duruyor ve isleri oradan da
 * gorulebiliyor; buradaki ekran <b>outbox kuyruguna</b> bakiyor, ki o
 * Hangfire'in gormedigi bir sey: is basariyla kostugu hâlde mesaj
 * islenmemis olabilir.
 */
export function SistemPage() {
  const queryClient = useQueryClient();
  const [sekme, setSekme] = useState<KuyrukFiltresi>('DeadLettered');

  const { data: ozet } = useQuery<AdminOzeti>({
    queryKey: ['admin-overview'],
    queryFn: ozetGetir,
  });

  const { data, isPending, isError, error } = useQuery<KuyrukMesaji[]>({
    queryKey: ['admin-queue', sekme],
    queryFn: () => kuyrukGetir(sekme),
  });

  const geriKoy = useMutation({
    mutationFn: mesajiKuyrugaKoy,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin-queue'] }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
    },
  });

  return (
    <div className="mx-auto max-w-5xl">
      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-md text-on-surface">Sistem ve kuyruk</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          Bildirim kuyruğu ve altyapı durumu.
        </p>
      </header>

      {ozet && (
        <section aria-label="Durum" className="mb-stack-md grid gap-stack-sm sm:grid-cols-3">
          <DurumKutusu baslik="Bekleyen" deger={ozet.queue.pending} />
          <DurumKutusu baslik="Yeniden denenecek" deger={ozet.queue.retryable} />
          <DurumKutusu
            baslik="Ölü mektup"
            deger={ozet.queue.deadLettered}
            uyarili={ozet.queue.deadLettered > 0}
          />
        </section>
      )}

      <div className="mb-stack-sm flex flex-wrap gap-base" role="tablist" aria-label="Kuyruk durumu">
        {SEKMELER.map((secenek) => (
          <button
            key={secenek.deger}
            type="button"
            role="tab"
            aria-selected={sekme === secenek.deger}
            onClick={() => setSekme(secenek.deger)}
            className={`rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
              sekme === secenek.deger
                ? 'bg-primary-container/25 font-semibold text-primary'
                : 'border border-outline-variant text-on-surface-variant hover:text-on-surface'
            }`}
          >
            {secenek.metin}
          </button>
        ))}
      </div>

      {sekme === 'DeadLettered' && (
        <Uyari
          ton="dikkat"
          dolgu="dar"
          className="mb-stack-sm"
          ikon={<UyariIkonu className="mt-[2px] h-4 w-4 shrink-0" />}
        >
          Bu mesajlar iş akışı tarafından bir daha denenmiyor. Kuyruğa geri koymadan önce
          hatanın sebebinin giderildiğinden emin ol; giderilmediyse mesaj yeniden tükenir.
        </Uyari>
      )}

      {isPending && (
        <Iskelet adet={3} cubuk="h-20 rounded-md bg-surface-variant/40" duzen="space-y-base" />
      )}

      {isError && <Uyari role="alert">{hataMesaji(error, 'Kuyruk yüklenemedi.')}</Uyari>}

      {geriKoy.isError && (
        <Uyari role="alert" dolgu="dar" className="mb-stack-sm">
          {hataMesaji(geriKoy.error, 'Mesaj kuyruğa konamadı.')}
        </Uyari>
      )}

      {data && data.length === 0 && <BosDurum>Bu durumda mesaj yok.</BosDurum>}

      <ul className="space-y-base">
        {data?.map((mesaj) => (
          <li
            key={mesaj.id}
            className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-sm"
          >
            <div className="flex flex-wrap items-start justify-between gap-base">
              <div className="min-w-0">
                <p className="font-body text-body-md text-on-surface">{mesaj.type}</p>
                <p className="font-body text-body-sm tabular-nums text-on-surface-variant">
                  {tarihBicimi.format(new Date(mesaj.occurredAtUtc))}
                  {' · '}
                  {mesaj.retryCount} deneme
                </p>
              </div>

              {mesaj.processedAtUtc ? (
                <span className="rounded-full border border-primary/50 px-base py-[2px] font-body text-[11px] text-primary">
                  İşlendi
                </span>
              ) : (
                sekme === 'DeadLettered' && (
                  <button
                    type="button"
                    onClick={() => geriKoy.mutate(mesaj.id)}
                    disabled={geriKoy.isPending}
                    className="rounded-md border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high disabled:opacity-50"
                  >
                    Kuyruğa geri koy
                  </button>
                )
              )}
            </div>

            {mesaj.errorMessage && (
              <p className="mt-base break-all rounded-md bg-surface-container-low px-stack-sm py-base font-mono text-[11px] text-error">
                {mesaj.errorMessage}
              </p>
            )}

            {mesaj.correlationId && (
              <p className="mt-base font-mono text-[11px] text-on-surface-variant">
                İlgili kayıt: {mesaj.correlationId}
              </p>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

function DurumKutusu({
  baslik,
  deger,
  uyarili = false,
}: {
  baslik: string;
  deger: number;
  uyarili?: boolean;
}) {
  return (
    <div className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-md">
      <p className="font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
        {baslik}
      </p>
      <p
        className={`mt-base font-headline text-headline-md tabular-nums ${
          uyarili ? 'text-tertiary' : 'text-on-surface'
        }`}
      >
        {deger}
      </p>
    </div>
  );
}
