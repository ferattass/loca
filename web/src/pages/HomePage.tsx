import { useQuery } from '@tanstack/react-query';

import { benKimim } from '../api/auth';
import { useAuthStore } from '../stores/authStore';

/**
 * Oturum acan kullanicinin karsilastigi ilk ekran.
 *
 * Su an yalnizca kimligi ve rollerini gosteriyor. Etkinlik listesi Gun 5'te,
 * koltuk secimi Gun 6'da buraya baglanacak.
 */
export function HomePage() {
  const kullanici = useAuthStore((durum) => durum.kullanici);

  // Rol ve hesap durumu token uretildikten sonra degismis olabilir; bu yuzden
  // ekrandaki bilgi token claim'lerinden degil sunucudan okunur.
  const { data: sunucudakiKullanici, isLoading } = useQuery({
    queryKey: ['ben'],
    queryFn: benKimim,
  });

  const goruntulenen = sunucudakiKullanici ?? kullanici;

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <header className="mb-stack-lg">
        <p className="font-body text-label-caps text-primary uppercase">Hesabım</p>
        <h1 className="font-display text-display-lg-mobile md:text-display-lg text-on-surface mt-base">
          Hoş geldin{goruntulenen ? `, ${goruntulenen.fullName.split(' ')[0]}` : ''}
        </h1>
      </header>

      <section className="glass rounded-xl p-stack-md max-w-2xl">
        <h2 className="font-headline text-headline-md text-on-surface mb-stack-sm">
          Hesap bilgileri
        </h2>

        {isLoading && !goruntulenen ? (
          <p className="font-body text-body-md text-on-surface-variant">Yukleniyor…</p>
        ) : (
          <dl className="grid gap-stack-sm sm:grid-cols-2">
            <div>
              <dt className="font-body text-body-sm text-on-surface-variant">Ad soyad</dt>
              <dd className="font-body text-body-md text-on-surface mt-1">
                {goruntulenen?.fullName}
              </dd>
            </div>

            <div>
              <dt className="font-body text-body-sm text-on-surface-variant">E-posta</dt>
              <dd className="font-body text-body-md text-on-surface mt-1">
                {goruntulenen?.email}
              </dd>
            </div>

            <div className="sm:col-span-2">
              <dt className="font-body text-body-sm text-on-surface-variant">Roller</dt>
              <dd className="flex flex-wrap gap-base mt-1">
                {goruntulenen?.roles.map((rol) => (
                  <span
                    key={rol}
                    className="rounded-full bg-primary-container/30 border border-primary/40 px-stack-sm py-1 font-body text-body-sm text-primary"
                  >
                    {rol}
                  </span>
                ))}
              </dd>
            </div>
          </dl>
        )}
      </section>

    </main>
  );
}
