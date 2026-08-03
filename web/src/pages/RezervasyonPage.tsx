import { useCallback, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';

import { hataMesaji } from '../api/client';
import {
  rezervasyonGetir,
  rezervasyonIptalEt,
  rezervasyonUzat,
  type Rezervasyon,
} from '../api/reservations';
import { sureBicimle, useGeriSayim } from '../hooks/useGeriSayim';

/** Son bir dakikada sayac kirmiziya doner. */
const UYARI_ESIGI_SANIYE = 60;

const paraBicimi = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
});

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

const DURUM_METNI: Record<Rezervasyon['status'], string> = {
  Pending: 'Odeme bekleniyor',
  Confirmed: 'Tamamlandi',
  Cancelled: 'Iptal edildi',
  Expired: 'Suresi doldu',
};

/**
 * Rezervasyon ozeti, geri sayim ve islemler.
 *
 * Geri sayim sunucudan gelen `remainingSeconds` degerinden baslar.
 * `expiresAtUtc` ile istemci saati karsilastirilsaydi, saati geri alinmis bir
 * makinede sayac hic bitmez ve kullanici suresi coktan dolmus bir
 * rezervasyonu odemeye calisirdi.
 */
export function RezervasyonPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [islemHatasi, setIslemHatasi] = useState<string | null>(null);

  const { data, isPending, isError, error } = useQuery<Rezervasyon>({
    queryKey: ['reservation', id],
    queryFn: () => rezervasyonGetir(id),
    enabled: id.length > 0,
  });

  // Sayac bittiginde sunucuya bir kez daha soruluyor: gercek durum burada
  // degil sunucuda. Sayacin sifirlanmasi "suresi doldu" demek degil,
  // "sunucuya tekrar sor" demek.
  const sureDoldu = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ['reservation', id] });
  }, [queryClient, id]);

  const kalan = useGeriSayim(
    data?.status === 'Pending' ? data.remainingSeconds : undefined,
    sureDoldu,
  );

  const uzat = useMutation({
    mutationFn: () => rezervasyonUzat(id),
    onSuccess: (yeni) => {
      setIslemHatasi(null);
      queryClient.setQueryData(['reservation', id], yeni);
    },
    onError: (hata) => setIslemHatasi(hataMesaji(hata, 'Sure uzatilamadi.')),
  });

  const iptal = useMutation({
    mutationFn: () => rezervasyonIptalEt(id),
    onSuccess: async () => {
      setIslemHatasi(null);
      await queryClient.invalidateQueries({ queryKey: ['reservation', id] });

      if (data) {
        // Koltuklar serbest kaldi; plan yenilenmeli.
        await queryClient.invalidateQueries({
          queryKey: ['seat-availability', data.eventSessionId],
        });
      }
    },
    onError: (hata) => setIslemHatasi(hataMesaji(hata, 'Rezervasyon iptal edilemedi.')),
  });

  if (isPending) {
    return (
      <Sayfa>
        <div className="animate-pulse space-y-stack-sm" aria-hidden="true">
          <div className="h-6 w-56 rounded bg-surface-variant/60" />
          <div className="h-40 rounded-lg bg-surface-variant/40" />
        </div>
        <span className="sr-only" role="status">
          Rezervasyon yukleniyor
        </span>
      </Sayfa>
    );
  }

  if (isError) {
    return (
      <Sayfa>
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hataMesaji(error, 'Rezervasyon yuklenemedi.')}
        </p>
      </Sayfa>
    );
  }

  const bekliyor = data.status === 'Pending' && kalan > 0;
  const azKaldi = kalan > 0 && kalan <= UYARI_ESIGI_SANIYE;

  return (
    <Sayfa>
      <header className="mb-stack-md">
        <p className="font-body text-label-caps uppercase tracking-widest text-primary">
          {data.venueName} · {data.hallName}
        </p>
        <h1 className="font-headline text-headline-md text-on-surface">{data.eventTitle}</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          {tarihBicimi.format(new Date(data.sessionStartsAtUtc))}
        </p>
      </header>

      {bekliyor ? (
        <section
          aria-label="Kalan sure"
          className={`mb-stack-md rounded-lg border p-stack-sm ${
            azKaldi
              ? 'border-error/50 bg-error-container/20'
              : 'border-outline-variant/40 bg-surface-variant/30'
          }`}
        >
          <p className="font-body text-body-sm text-on-surface-variant">
            Koltuklarin senin icin tutuluyor. Kalan sure:
          </p>
          {/*
            aria-live="polite": sayac her saniye degisiyor ama ekran okuyucu
            her degisikligi okumamali — okusaydi kullanici baska hicbir sey
            duyamazdi. Bu yuzden sayac gorseldir, sesli uyari yalnizca son
            dakikada bir kez verilir.
          */}
          <p
            className={`font-headline text-headline-lg tabular-nums ${
              azKaldi ? 'text-error' : 'text-on-surface'
            }`}
            aria-hidden="true"
          >
            {sureBicimle(kalan)}
          </p>
          <p role="status" aria-live="polite" className="sr-only">
            {azKaldi ? 'Bir dakikadan az sure kaldi.' : ''}
          </p>

          {data.extensionUsed && (
            <p className="font-body text-[11px] text-on-surface-variant">
              Uzatma hakkini kullandin.
            </p>
          )}
        </section>
      ) : (
        <p
          role="status"
          className="mb-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/30 px-stack-sm py-stack-sm font-body text-body-md text-on-surface"
        >
          {data.status === 'Pending' ? DURUM_METNI.Expired : DURUM_METNI[data.status]}
          {data.status === 'Pending' && ' — koltuklar serbest birakildi.'}
        </p>
      )}

      <section
        aria-label="Koltuklar"
        className="mb-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-sm"
      >
        <ul className="space-y-base">
          {data.seats.map((koltuk) => (
            <li
              key={koltuk.eventSeatId}
              className="flex items-center justify-between font-body text-body-sm text-on-surface"
            >
              <span>
                {koltuk.sectionName} {koltuk.rowLabel}-{koltuk.seatNumber}
                <span className="ml-base text-on-surface-variant">({koltuk.ticketTypeName})</span>
              </span>
              <span>{paraBicimi.format(koltuk.price)}</span>
            </li>
          ))}
        </ul>

        <p className="mt-stack-sm border-t border-outline-variant/30 pt-stack-sm font-body text-body-md text-on-surface">
          Toplam: <strong className="font-semibold">{paraBicimi.format(data.totalAmount)}</strong>
        </p>
      </section>

      {islemHatasi && (
        <p
          role="alert"
          className="mb-stack-sm rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
        >
          {islemHatasi}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-stack-sm">
        {bekliyor && (
          <>
            <button
              type="button"
              onClick={() => navigate(`/odeme/${data.id}`)}
              className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary"
            >
              Ödemeye geç
            </button>

            <button
              type="button"
              onClick={() => uzat.mutate()}
              disabled={uzat.isPending || data.extensionUsed}
              className="rounded-md border border-outline-variant px-stack-sm py-base font-body text-body-sm text-on-surface disabled:opacity-40"
            >
              {data.extensionUsed ? 'Uzatma hakki kullanildi' : 'Sureyi uzat (+5 dk)'}
            </button>

            <button
              type="button"
              onClick={() => iptal.mutate()}
              disabled={iptal.isPending}
              className="font-body text-body-sm text-error underline underline-offset-2 disabled:opacity-50"
            >
              {iptal.isPending ? 'Iptal ediliyor' : 'Rezervasyonu iptal et'}
            </button>
          </>
        )}

        {!bekliyor && (
          <button
            type="button"
            onClick={() => navigate(`/oturumlar/${data.eventSessionId}/koltuklar`)}
            className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary"
          >
            Yeniden koltuk sec
          </button>
        )}

        <Link
          to="/rezervasyonlarim"
          className="font-body text-body-sm text-primary underline underline-offset-2"
        >
          Rezervasyonlarim
        </Link>
      </div>
    </Sayfa>
  );
}

function Sayfa({ children }: { children: React.ReactNode }) {
  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-3xl">{children}</div>
    </main>
  );
}
