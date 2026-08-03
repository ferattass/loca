import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { hataMesaji } from '../api/client';
import { rezervasyonlarimGetir, type RezervasyonOzeti } from '../api/reservations';

const paraBicimi = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' });

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

/**
 * Odemesi tamamlanmis rezervasyonlarin biletleri.
 *
 * Biletleri tek tek listeleyen bir uc yok; bu sayfa yalnizca Confirmed
 * rezervasyonlari gosterip her biri icin odeme sayfasina baglanti verir.
 * Biletin kendisi (QR dahil) yalnizca odeme tamamlanirken bir kerelik
 * dondugu icin, "Biletleri gor" o akisi tekrar acar.
 */
export function BiletlerimPage() {
  const { data, isPending, isError, error } = useQuery<RezervasyonOzeti[]>({
    queryKey: ['my-reservations'],
    queryFn: rezervasyonlarimGetir,
  });

  const biletliRezervasyonlar = data?.filter((kayit) => kayit.status === 'Confirmed') ?? [];

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-3xl">
        <h1 className="mb-stack-md font-headline text-headline-md text-on-surface">Biletlerim</h1>

        {isPending && (
          <div className="animate-pulse space-y-stack-sm" aria-hidden="true">
            <div className="h-20 rounded-lg bg-surface-variant/40" />
            <div className="h-20 rounded-lg bg-surface-variant/40" />
          </div>
        )}

        {isError && (
          <p
            role="alert"
            className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
          >
            {hataMesaji(error, 'Biletler yuklenemedi.')}
          </p>
        )}

        {data && biletliRezervasyonlar.length === 0 && (
          <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-stack-md font-body text-body-sm text-on-surface-variant">
            Henuz biletin yok.
          </p>
        )}

        <ul className="space-y-stack-sm">
          {biletliRezervasyonlar.map((kayit) => (
            <li
              key={kayit.id}
              className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-sm"
            >
              <div className="flex flex-wrap items-start justify-between gap-base">
                <div>
                  <p className="font-body text-body-md text-on-surface">{kayit.eventTitle}</p>
                  <p className="font-body text-body-sm text-on-surface-variant">
                    {kayit.venueName} · {tarihBicimi.format(new Date(kayit.sessionStartsAtUtc))}
                  </p>
                </div>

                <span className="rounded-full border border-primary/50 px-base py-[2px] font-body text-[11px] text-primary">
                  Tamamlandı
                </span>
              </div>

              <div className="mt-base flex flex-wrap items-center justify-between gap-base">
                <p className="font-body text-body-sm text-on-surface-variant">
                  {kayit.seatCount} koltuk · {paraBicimi.format(kayit.totalAmount)}
                </p>

                <Link
                  to={`/odeme/${kayit.id}`}
                  className="font-body text-body-sm text-primary underline underline-offset-2"
                >
                  Biletleri gör
                </Link>
              </div>
            </li>
          ))}
        </ul>
      </div>
    </main>
  );
}
