import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { hataMesaji } from '../api/client';
import { biletlerimGetir, type Bilet } from '../api/tickets';
import { BiletKarti } from '../components/BiletKarti';

const gunBicimi = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'full' });

/**
 * Kullanicinin biletleri.
 *
 * Biletler sunucudan YAKLASAN ONCE siralanmis geliyor; burada yeniden
 * siralanmiyor. Gecmis etkinliklerin biletleri de listede kaliyor:
 * silinmis bir bilet, "gittigimi nasil kanitlarim" sorusunu dogurur.
 */
export function BiletlerimPage() {
  const { data, isPending, isError, error } = useQuery<Bilet[]>({
    queryKey: ['tickets'],
    queryFn: () => biletlerimGetir(),
  });

  const simdi = Date.now();
  const yaklasan = data?.filter((bilet) => new Date(bilet.eventStartsAtUtc).getTime() >= simdi) ?? [];
  const gecmis = data?.filter((bilet) => new Date(bilet.eventStartsAtUtc).getTime() < simdi) ?? [];

  return (
    <main className="min-h-screen px-container-margin-mobile py-stack-lg md:px-container-margin-desktop">
      <div className="mx-auto max-w-3xl">
        <h1 className="font-headline text-headline-md text-on-surface">Biletlerim</h1>
        <p className="mt-1 font-body text-body-sm text-on-surface-variant">
          Girişte QR kodu okutman yeterli. Bileti PDF veya görsel olarak indirip telefonunda
          saklayabilirsin.
        </p>

        {isPending && (
          <div className="mt-stack-md animate-pulse space-y-stack-md" aria-hidden="true">
            <div className="h-44 rounded-lg bg-surface-variant/40" />
            <div className="h-44 rounded-lg bg-surface-variant/40" />
          </div>
        )}

        {isError && (
          <p
            role="alert"
            className="mt-stack-md rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
          >
            {hataMesaji(error, 'Biletler yüklenemedi.')}
          </p>
        )}

        {data && data.length === 0 && (
          <div className="mt-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-md py-stack-lg text-center">
            <p className="font-body text-body-md text-on-surface">Henüz biletin yok.</p>
            <p className="mt-base font-body text-body-sm text-on-surface-variant">
              Ödemesi tamamlanan her koltuk için burada bir bilet oluşur.
            </p>
            <Link
              to="/"
              className="mt-stack-sm inline-block rounded-full bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary"
            >
              Etkinlikleri keşfet
            </Link>
          </div>
        )}

        <Bolum baslik="Yaklaşan" biletler={yaklasan} />
        <Bolum baslik="Geçmiş" biletler={gecmis} sonuk />
      </div>
    </main>
  );
}

function Bolum({
  baslik,
  biletler,
  sonuk = false,
}: {
  baslik: string;
  biletler: Bilet[];
  sonuk?: boolean;
}) {
  if (biletler.length === 0) return null;

  return (
    <section className="mt-stack-lg">
      <h2 className="font-body text-label-caps uppercase tracking-widest text-on-surface-variant">
        {baslik}
      </h2>

      <ul className="mt-stack-sm space-y-stack-md">
        {biletler.map((bilet) => (
          // Gecmis biletler soluk: listede duruyorlar ama kullanicinin
          // birazdan kullanacagi biletle karistirilmamalari gerekiyor.
          <li key={bilet.id} className={sonuk ? 'opacity-60' : undefined}>
            <p className="mb-base font-body text-body-sm text-on-surface-variant">
              {gunBicimi.format(new Date(bilet.eventStartsAtUtc))}
            </p>
            <BiletKarti bilet={bilet} />
          </li>
        ))}
      </ul>
    </section>
  );
}
