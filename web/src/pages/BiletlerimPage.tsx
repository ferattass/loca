import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { hataMesaji } from '../api/client';
import { biletlerimGetir, type Bilet, type BiletDurumu } from '../api/tickets';
import { BiletKarti } from '../components/BiletKarti';
import { SagOkIkonu } from '../components/ui/Ikon';

const gunBicimi = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'long' });
const saatBicimi = new Intl.DateTimeFormat('tr-TR', { timeStyle: 'short' });

const DURUM_METNI: Record<BiletDurumu, string> = {
  Valid: 'Geçerli',
  Used: 'Kullanıldı',
  Cancelled: 'İptal',
  Refunded: 'İade',
};

interface BiletGrubu {
  oturumId: string;
  baslik: string;
  mekan: string;
  salon: string;
  baslangicUtc: string;
  biletler: Bilet[];
}

/**
 * Bir oturumun butun biletlerini tek baslik altinda toplar.
 *
 * <b>Neden gruplaniyor:</b> ayni etkinlige uc bilet alan kullanici, ayni
 * etkinlik adini, ayni mekani ve ayni tarihi uc kez alt alta goruyordu —
 * her biri tam boy kart hâlinde. On bilet alan biri on dev kart gorurdu.
 * Degisen tek sey koltuk numarasiyken tekrar eden her satir, degiseni
 * bulmayi zorlastiriyor.
 *
 * <para>
 * Sunucunun siralamasi KORUNUYOR: gruplar ilk gorulduklari sirada
 * duruyor. Burada yeniden siralansaydi "en ustteki bilet" ifadesi ekranla
 * API arasinda farkli seyleri gosterirdi.
 * </para>
 */
function grupla(biletler: Bilet[]): BiletGrubu[] {
  const gruplar = new Map<string, BiletGrubu>();

  for (const bilet of biletler) {
    const mevcut = gruplar.get(bilet.eventSessionId);

    if (mevcut) {
      mevcut.biletler.push(bilet);
      continue;
    }

    gruplar.set(bilet.eventSessionId, {
      oturumId: bilet.eventSessionId,
      baslik: bilet.eventTitle,
      mekan: bilet.venueName,
      salon: bilet.hallName,
      baslangicUtc: bilet.eventStartsAtUtc,
      biletler: [bilet],
    });
  }

  return [...gruplar.values()];
}

/**
 * Kullanicinin biletleri.
 *
 * Gecmis etkinliklerin biletleri de listede kaliyor: silinmis bir bilet,
 * "gittigimi nasil kanitlarim" sorusunu dogurur.
 */
export function BiletlerimPage() {
  const { data, isPending, isError, error } = useQuery<Bilet[]>({
    queryKey: ['tickets'],
    queryFn: () => biletlerimGetir(),
  });

  const simdi = Date.now();
  const gecmisMi = (bilet: Bilet) => new Date(bilet.eventStartsAtUtc).getTime() < simdi;

  const yaklasan = grupla((data ?? []).filter((bilet) => !gecmisMi(bilet)));
  const gecmis = grupla((data ?? []).filter(gecmisMi));

  return (
    <main className="min-h-screen px-container-margin-mobile py-stack-lg md:px-container-margin-desktop">
      <div className="mx-auto max-w-2xl">
        <h1 className="font-headline text-headline-md text-on-surface">Biletlerim</h1>
        <p className="mt-1 font-body text-body-sm text-on-surface-variant">
          Girişte QR kodu okutman yeterli. Bilete dokununca kodu ve indirme seçenekleri açılır.
        </p>

        {isPending && (
          <div className="mt-stack-md animate-pulse space-y-stack-sm" aria-hidden="true">
            <div className="h-28 rounded-lg bg-surface-variant/40" />
            <div className="h-28 rounded-lg bg-surface-variant/40" />
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

        <Bolum baslik="Yaklaşan" gruplar={yaklasan} />
        <Bolum baslik="Geçmiş" gruplar={gecmis} sonuk />
      </div>
    </main>
  );
}

function Bolum({
  baslik,
  gruplar,
  sonuk = false,
}: {
  baslik: string;
  gruplar: BiletGrubu[];
  sonuk?: boolean;
}) {
  if (gruplar.length === 0) return null;

  return (
    <section className="mt-stack-lg">
      <h2 className="font-body text-label-caps uppercase tracking-widest text-on-surface-variant">
        {baslik}
      </h2>

      <ul className="mt-stack-sm space-y-stack-sm">
        {gruplar.map((grup) => (
          // Gecmis biletler soluk: listede duruyorlar ama kullanicinin
          // birazdan kullanacagi biletle karistirilmamalari gerekiyor.
          <li key={grup.oturumId} className={sonuk ? 'opacity-60' : undefined}>
            <GrupKarti grup={grup} />
          </li>
        ))}
      </ul>
    </section>
  );
}

function GrupKarti({ grup }: { grup: BiletGrubu }) {
  const baslangic = new Date(grup.baslangicUtc);

  return (
    <article className="overflow-hidden rounded-lg border border-outline-variant/40 bg-surface-variant/20">
      <header className="border-b border-outline-variant/30 px-stack-sm py-stack-sm">
        <div className="flex flex-wrap items-start justify-between gap-base">
          <h3 className="min-w-0 font-headline text-title-lg text-on-surface">{grup.baslik}</h3>

          <span className="shrink-0 rounded-full border border-outline-variant px-base py-[2px] font-body text-[11px] text-on-surface-variant">
            {grup.biletler.length} bilet
          </span>
        </div>

        <p className="mt-base font-body text-body-sm text-on-surface-variant">
          {gunBicimi.format(baslangic)} · {saatBicimi.format(baslangic)}
        </p>
        <p className="font-body text-body-sm text-on-surface-variant">
          {grup.mekan} · {grup.salon}
        </p>
      </header>

      <ul>
        {grup.biletler.map((bilet) => (
          <BiletSatiri
            key={bilet.id}
            bilet={bilet}
            // Tek biletli grupta acilir kapanir yapmanin anlami yok:
            // secilecek bir sey olmadigi hâlde bir tiklama daha
            // istemek olurdu.
            bastanAcik={grup.biletler.length === 1}
          />
        ))}
      </ul>
    </article>
  );
}

function BiletSatiri({ bilet, bastanAcik }: { bilet: Bilet; bastanAcik: boolean }) {
  const [acik, setAcik] = useState(bastanAcik);
  const bolgeId = `bilet-${bilet.id}`;

  const kullanildi = bilet.status === 'Used';
  const gecersiz = bilet.status === 'Cancelled' || bilet.status === 'Refunded';

  return (
    <li className="border-t border-outline-variant/20 first:border-t-0">
      <button
        type="button"
        onClick={() => setAcik((onceki) => !onceki)}
        aria-expanded={acik}
        aria-controls={bolgeId}
        className="flex w-full items-center gap-stack-sm px-stack-sm py-stack-sm text-left transition-colors hover:bg-surface-container-high"
      >
        <SagOkIkonu
          className={`h-4 w-4 shrink-0 text-on-surface-variant transition-transform ${
            acik ? 'rotate-90' : ''
          }`}
        />

        <span className="min-w-0 flex-1">
          <span className="block font-body text-body-md text-on-surface">
            {bilet.seatLabel}
            <span className="text-on-surface-variant"> · {bilet.ticketTypeName}</span>
          </span>

          {/* Bilet numarasi kapaliyken de gorunuyor: kapida gorevliye
              okumak ya da destege soylemek icin QR'i acmaya gerek yok. */}
          <span className="block font-mono text-[11px] tracking-wide text-on-surface-variant/70">
            {bilet.ticketNumber}
          </span>
        </span>

        <DurumRozeti durum={bilet.status} kullanildi={kullanildi} gecersiz={gecersiz} />
      </button>

      {acik && (
        <div id={bolgeId} className="px-stack-sm pb-stack-sm">
          <BiletKarti bilet={bilet} />
        </div>
      )}
    </li>
  );
}

function DurumRozeti({
  durum,
  kullanildi,
  gecersiz,
}: {
  durum: BiletDurumu;
  kullanildi: boolean;
  gecersiz: boolean;
}) {
  // Gecerli bilet icin rozet YOK: listedeki biletlerin cogu gecerli ve
  // her satira "Gecerli" yazmak, asil dikkat cekmesi gereken iki durumu
  // (kullanilmis, iptal) gorunmez kilardi.
  if (!kullanildi && !gecersiz) return null;

  return (
    <span
      className={`shrink-0 rounded-full border px-base py-[2px] font-body text-[11px] ${
        gecersiz
          ? 'border-error/50 text-error'
          : 'border-tertiary/50 text-tertiary'
      }`}
    >
      {DURUM_METNI[durum]}
    </span>
  );
}
