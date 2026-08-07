import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { hataMesaji } from '../api/client';
import {
  mekanlariGetir,
  sehirleriGetir,
  type MekanOzeti,
  type Sehir,
} from '../api/catalog';
import { etkinlikleriGetir } from '../api/eventCatalog';

/**
 * Mekânlar — herkese acik salon listesi.
 *
 * <b>Boyle bir sayfa yoktu:</b> mekan verisi yalnizca yonetim panelinde
 * ve etkinlik detayinda gorunuyordu. Kullanicinin "evime yakin sahnede ne
 * var" sorusunu sorabilecegi bir yer kalmiyordu, oysa katalog sehir ve
 * mekana gore zaten duzenli.
 *
 * <para>
 * Sehirler <b>yaklasan etkinlik sayisiyla</b> gosteriliyor. Salt mekan
 * listesi, bos bir sahneyle dolu bir sahneyi ayirt ettirmezdi.
 * </para>
 */
export function MekanlarPage() {
  const sehirSorgu = useQuery<Sehir[]>({
    queryKey: ['cities'],
    queryFn: sehirleriGetir,
  });

  const mekanSorgu = useQuery({
    queryKey: ['venues-all'],
    queryFn: () => mekanlariGetir(undefined),
  });

  // Etkinlik sayisini mekana gore cikarmak icin katalog bir kez cekiliyor.
  // Her mekan icin ayri sorgu atmak on bir istek demek olurdu.
  const etkinlikSorgu = useQuery({
    queryKey: ['discover-events', null, 'hepsi', ''],
    queryFn: () => etkinlikleriGetir({ sayfaBoyutu: 100 }),
  });

  const sayimlar = new Map<string, number>();

  for (const etkinlik of etkinlikSorgu.data?.items ?? []) {
    sayimlar.set(etkinlik.venueName, (sayimlar.get(etkinlik.venueName) ?? 0) + 1);
  }

  const sehirler = sehirSorgu.data ?? [];
  const mekanlar = mekanSorgu.data ?? [];

  const hata = mekanSorgu.isError
    ? hataMesaji(mekanSorgu.error, 'Mekânlar yüklenemedi.')
    : null;

  return (
    <main className="mx-auto w-full max-w-7xl px-container-margin-mobile py-stack-lg md:px-container-margin-desktop">
      <header className="mb-stack-md">
        <h1 className="font-display text-display-lg-mobile text-on-surface">Mekânlar</h1>
        <p className="mt-base font-body text-body-md text-on-surface-variant">
          Etkinliklerin düzenlendiği salonlar ve sahneler.
        </p>
      </header>

      {hata && (
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hata}
        </p>
      )}

      {mekanSorgu.isPending && (
        <div className="grid animate-pulse gap-stack-sm sm:grid-cols-2 lg:grid-cols-3" aria-hidden="true">
          {[0, 1, 2, 3, 4, 5].map((sira) => (
            <div key={sira} className="h-28 rounded-lg bg-surface-variant/40" />
          ))}
        </div>
      )}

      <div className="space-y-stack-lg">
        {sehirler.map((sehir) => {
          const sehrinMekanlari = mekanlar.filter((mekan) => mekan.cityName === sehir.name);

          if (sehrinMekanlari.length === 0) return null;

          return (
            <section key={sehir.id} aria-labelledby={`sehir-${sehir.id}`}>
              <div className="mb-stack-sm flex flex-wrap items-baseline justify-between gap-base">
                <h2
                  id={`sehir-${sehir.id}`}
                  className="font-headline text-headline-md text-on-surface"
                >
                  {sehir.name}
                </h2>

                <Link
                  to={`/?sehir=${sehir.id}`}
                  className="font-body text-body-sm text-primary underline underline-offset-2"
                >
                  {sehir.name} etkinlikleri
                </Link>
              </div>

              <ul className="grid gap-stack-sm sm:grid-cols-2 lg:grid-cols-3">
                {sehrinMekanlari.map((mekan) => (
                  <MekanKarti
                    key={mekan.id}
                    mekan={mekan}
                    etkinlikSayisi={sayimlar.get(mekan.name) ?? 0}
                  />
                ))}
              </ul>
            </section>
          );
        })}
      </div>
    </main>
  );
}

function MekanKarti({ mekan, etkinlikSayisi }: { mekan: MekanOzeti; etkinlikSayisi: number }) {
  return (
    <li className="rounded-lg border border-outline-variant/40 bg-surface-container-low p-stack-sm transition-colors hover:border-primary/40">
      <h3 className="font-headline text-title-lg text-on-surface">{mekan.name}</h3>

      <p className="mt-base font-body text-body-sm text-on-surface-variant">
        {mekan.hallCount} salon
        {/* Sifir etkinlik ACIKCA yaziliyor. Satir hic gosterilmeseydi
            kullanici sayinin yuklenmedigini sanabilirdi. */}
        {' · '}
        {etkinlikSayisi > 0 ? `${etkinlikSayisi} yaklaşan etkinlik` : 'yaklaşan etkinlik yok'}
      </p>
    </li>
  );
}
