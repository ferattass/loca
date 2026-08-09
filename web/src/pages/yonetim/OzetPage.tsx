import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { hataMesaji } from '../../api/client';
import { ozetGetir, type AdminOzeti } from '../../api/admin';
import { UyariIkonu } from '../../components/ui/Ikon';
import { paraKurussuz, saatBicimi } from '../../lib/bicim';


/** Ozet 30 saniyede bir tazeleniyor: panel acik birakilip izleniyor. */
const TAZELEME_MS = 30_000;

/**
 * Yonetim panelinin acilis ekrani.
 *
 * <b>Once bugun, sonra sistem.</b> Panele bakan kisinin ilk sorusu
 * neredeyse her zaman "bugun ne oldu"; altyapi durumu ancak bir sey ters
 * gittiginde onemli. Sirasi bu yuzden boyle.
 */
export function OzetPage() {
  const { data, isPending, isError, error, dataUpdatedAt } = useQuery<AdminOzeti>({
    queryKey: ['admin-overview'],
    queryFn: ozetGetir,
    refetchInterval: TAZELEME_MS,
  });

  return (
    <div className="mx-auto max-w-5xl">
      <header className="mb-stack-md flex flex-wrap items-end justify-between gap-base">
        <div>
          <h1 className="font-headline text-headline-md text-on-surface">Özet</h1>
          <p className="font-body text-body-sm text-on-surface-variant">
            Bugünün satışı ve sistemin durumu.
          </p>
        </div>

        {dataUpdatedAt > 0 && (
          <p className="font-body text-body-sm text-on-surface-variant">
            {saatBicimi.format(new Date(dataUpdatedAt))} itibarıyla
          </p>
        )}
      </header>

      {isPending && (
        <div className="grid animate-pulse gap-stack-sm sm:grid-cols-2 lg:grid-cols-4" aria-hidden="true">
          {[0, 1, 2, 3].map((sira) => (
            <div key={sira} className="h-24 rounded-lg bg-surface-variant/40" />
          ))}
        </div>
      )}

      {isError && (
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hataMesaji(error, 'Özet yüklenemedi.')}
        </p>
      )}

      {data && (
        <>
          <section aria-label="Bugün" className="grid gap-stack-sm sm:grid-cols-2 lg:grid-cols-4">
            <Kutu
              baslik="Bugünkü satış"
              deger={paraKurussuz(data.today.totalAmount)}
              alt={`${data.today.succeededCount} ödeme`}
              vurgulu
            />
            <Kutu
              baslik="Kesilen bilet"
              deger={String(data.ticketsIssuedToday)}
              alt="bugün"
            />
            <Kutu
              baslik="İade"
              deger={paraKurussuz(data.today.refundedAmount)}
              alt={`${data.today.refundedCount} işlem`}
              // Iade tutari satistan DUSULMUYOR, ayri duruyor: tek bir net
              // rakama indirilirse iade oraninin arttigi fark edilmez.
              uyarili={data.today.refundedCount > 0}
            />
            <Kutu
              baslik="Başarısız ödeme"
              deger={String(data.today.failedCount)}
              alt="bugün"
              uyarili={data.today.failedCount > 0}
            />
          </section>

          <section aria-label="Şu an" className="mt-stack-md grid gap-stack-sm sm:grid-cols-3">
            <Kutu
              baslik="Bekleyen rezervasyon"
              deger={String(data.pendingReservations)}
              alt="koltukları tutulu"
            />
            <Kutu
              baslik="Yaklaşan oturum"
              deger={String(data.upcomingSessions)}
              alt="satışta"
            />
            <Kutu
              baslik="Ödeme sağlayıcısı"
              deger={data.activePaymentProvider}
              alt={data.activePaymentProvider === 'Mock' ? 'taklit — gerçek tahsilat yok' : 'canlı akış'}
              uyarili={data.activePaymentProvider === 'Mock'}
            />
          </section>

          <div className="mt-stack-md grid gap-stack-sm lg:grid-cols-2">
            <section
              aria-label="Kuyruk"
              className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-md"
            >
              <div className="flex items-center justify-between">
                <h2 className="font-headline text-title-lg text-on-surface">Kuyruk</h2>
                <Link
                  to="/yonetim/sistem"
                  className="font-body text-body-sm text-primary underline underline-offset-2"
                >
                  Ayrıntı
                </Link>
              </div>

              <dl className="mt-stack-sm space-y-base">
                <Satir baslik="Bekleyen" deger={data.queue.pending} />
                <Satir baslik="Yeniden denenecek" deger={data.queue.retryable} />
                <Satir
                  baslik="Ölü mektup"
                  deger={data.queue.deadLettered}
                  // Sifirdan buyukse birinin bakmasi gerekiyor: bu mesajlari
                  // is akisi bir daha ele almiyor, yani birinin e-postasi
                  // hic gitmemis olabilir.
                  uyarili={data.queue.deadLettered > 0}
                />
              </dl>

              {data.queue.deadLettered > 0 && (
                <p className="mt-stack-sm flex items-start gap-base rounded-md border border-tertiary/40 bg-tertiary-container/10 px-stack-sm py-base font-body text-body-sm text-tertiary">
                  <UyariIkonu className="mt-[2px] h-4 w-4 shrink-0" />
                  Bu mesajlar bir daha denenmiyor. Sebep giderildikten sonra elle kuyruğa
                  konmaları gerekiyor.
                </p>
              )}
            </section>

            <section
              aria-label="Sağlık"
              className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-md"
            >
              <h2 className="font-headline text-title-lg text-on-surface">Sağlık</h2>

              <ul className="mt-stack-sm space-y-stack-sm">
                <SaglikSatiri baslik="Veritabanı" ayakta={data.health.database} />
                <SaglikSatiri
                  baslik="Redis (kilit)"
                  ayakta={data.health.redis}
                  aciklama={
                    data.health.redis
                      ? undefined
                      : 'Sistem çalışmaya devam ediyor; koltuk kilitleme katmanlı savunmanın ilk halkasını kaybetti.'
                  }
                />
              </ul>
            </section>
          </div>
        </>
      )}
    </div>
  );
}

function Kutu({
  baslik,
  deger,
  alt,
  vurgulu = false,
  uyarili = false,
}: {
  baslik: string;
  deger: string;
  alt: string;
  vurgulu?: boolean;
  uyarili?: boolean;
}) {
  return (
    <div
      className={`rounded-lg border p-stack-md ${
        vurgulu
          ? 'border-primary/40 bg-primary-container/15'
          : 'border-outline-variant/40 bg-surface-variant/20'
      }`}
    >
      <p className="font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
        {baslik}
      </p>
      <p
        className={`mt-base font-headline text-headline-md tabular-nums ${
          uyarili ? 'text-tertiary' : vurgulu ? 'text-primary' : 'text-on-surface'
        }`}
      >
        {deger}
      </p>
      <p className="font-body text-body-sm text-on-surface-variant">{alt}</p>
    </div>
  );
}

function Satir({
  baslik,
  deger,
  uyarili = false,
}: {
  baslik: string;
  deger: number;
  uyarili?: boolean;
}) {
  return (
    <div className="flex items-center justify-between font-body text-body-md">
      <dt className="text-on-surface-variant">{baslik}</dt>
      <dd
        className={`tabular-nums font-semibold ${uyarili ? 'text-tertiary' : 'text-on-surface'}`}
      >
        {deger}
      </dd>
    </div>
  );
}

function SaglikSatiri({
  baslik,
  ayakta,
  aciklama,
}: {
  baslik: string;
  ayakta: boolean;
  aciklama?: string;
}) {
  return (
    <li>
      <div className="flex items-center gap-base font-body text-body-md text-on-surface">
        {/* Renk tek basina birakilmadi: durum metni de yaziliyor, renk
            korlugu olan kullanici yesil ile kirmiziyi ayirt edemez. */}
        <span
          aria-hidden="true"
          className={`h-2.5 w-2.5 rounded-full ${ayakta ? 'bg-primary' : 'bg-error'}`}
        />
        <span className="flex-1">{baslik}</span>
        <span className={ayakta ? 'text-primary' : 'text-error'}>
          {ayakta ? 'Çalışıyor' : 'Ulaşılamıyor'}
        </span>
      </div>

      {aciklama && (
        <p className="ml-[18px] mt-base font-body text-body-sm text-on-surface-variant">
          {aciklama}
        </p>
      )}
    </li>
  );
}
