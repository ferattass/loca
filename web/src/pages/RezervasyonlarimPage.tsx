import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { hataMesaji } from '../api/client';
import { rezervasyonlarimGetir, type RezervasyonOzeti } from '../api/reservations';
import { sureBicimle } from '../hooks/useGeriSayim';
import { para } from '../lib/bicim';
import { tarihSaatBicimi } from '../lib/bicim';


const ROZET: Record<RezervasyonOzeti['status'], { metin: string; sinif: string }> = {
  Pending: { metin: 'Odeme bekleniyor', sinif: 'border-tertiary/50 text-tertiary' },
  Confirmed: { metin: 'Tamamlandi', sinif: 'border-primary/50 text-primary' },
  Cancelled: { metin: 'Iptal edildi', sinif: 'border-outline-variant text-on-surface-variant' },
  Expired: { metin: 'Suresi doldu', sinif: 'border-outline-variant text-on-surface-variant' },
};

/** Kullanicinin rezervasyonlari, yeniden eskiye. */
export function RezervasyonlarimPage() {
  const { data, isPending, isError, error } = useQuery<RezervasyonOzeti[]>({
    queryKey: ['my-reservations'],
    queryFn: rezervasyonlarimGetir,
  });

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-3xl">
        <h1 className="mb-stack-md font-headline text-headline-md text-on-surface">
          Rezervasyonlarim
        </h1>

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
            {hataMesaji(error, 'Rezervasyonlar yuklenemedi.')}
          </p>
        )}

        {data?.length === 0 && (
          <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-stack-md font-body text-body-sm text-on-surface-variant">
            Henuz rezervasyonun yok.
          </p>
        )}

        <ul className="space-y-stack-sm">
          {data?.map((kayit) => {
            const rozet = ROZET[kayit.status];
            const bekliyor = kayit.status === 'Pending' && kayit.remainingSeconds > 0;

            return (
              <li key={kayit.id}>
                <Link
                  to={`/rezervasyonlar/${kayit.id}`}
                  className="block rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-sm transition-colors hover:border-primary/60"
                >
                  <div className="flex flex-wrap items-start justify-between gap-base">
                    <div>
                      <p className="font-body text-body-md text-on-surface">{kayit.eventTitle}</p>
                      <p className="font-body text-body-sm text-on-surface-variant">
                        {kayit.venueName} · {tarihSaatBicimi.format(new Date(kayit.sessionStartsAtUtc))}
                      </p>
                    </div>

                    <span
                      className={`rounded-full border px-base py-[2px] font-body text-[11px] ${rozet.sinif}`}
                    >
                      {rozet.metin}
                    </span>
                  </div>

                  <p className="mt-base font-body text-body-sm text-on-surface-variant">
                    {kayit.seatCount} koltuk · {para(kayit.totalAmount)}
                    {/*
                      Kalan sure sunucudan gelen deger; listede canli saymiyor.
                      Her satir icin ayri zamanlayici kurmak, uzun listede
                      gereksiz is olurdu — canli sayac detay sayfasinda.
                    */}
                    {bekliyor && ` · ${sureBicimle(kayit.remainingSeconds)} kaldi`}
                  </p>
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
    </main>
  );
}
