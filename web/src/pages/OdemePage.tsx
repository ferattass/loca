import { useCallback, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from 'react-router-dom';

import { hataKodu, hataMesaji } from '../api/client';
import {
  odemeBaslat,
  odemeGetir,
  odemeTamamla,
  type OdemeDetayi,
  type OdemeTamamlamaSonucu,
} from '../api/payments';
import { rezervasyonGetir, type Rezervasyon } from '../api/reservations';
import { biletlerimGetir, type Bilet } from '../api/tickets';
import { BiletKarti } from '../components/BiletKarti';
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

/** Odeme akisinin baglandigi rezervasyon durumu icin kisa aciklama. */
const REZERVASYON_DURUM_METNI: Record<Rezervasyon['status'], string> = {
  Pending: 'Odeme bekleniyor',
  Confirmed: 'Bu rezervasyon zaten ödendi.',
  Cancelled: 'Bu rezervasyon iptal edilmiş.',
  Expired: 'Süresi doldu, koltuklar serbest bırakıldı.',
};

/**
 * Sunucunun makine okunur odeme hata kodlari icin kullanici mesaji.
 *
 * Karar mesaj metnine degil koda gore veriliyor ki sunucudaki metin
 * degistiginde arayuz mantigi kirilmasin.
 */
const ODEME_HATA_METNI: Record<string, string> = {
  'Payment.AlreadyPaid': 'Bu rezervasyon zaten ödendi.',
  'Payment.AlreadyPending': 'Bu rezervasyon için zaten bekleyen bir ödeme var.',
  'Payment.ReservationNotActive': 'Süresi doldu, koltuklar bırakıldı.',
  'Payment.ProviderRejected': 'Ödeme sağlayıcı işlemi reddetti.',
  'Payment.NotOwner': 'Bu rezervasyon sana ait değil.',
  'Payment.SeatsNoLongerHeld': 'Koltukların süresi doldu, tekrar seç.',
};

/** Bu kodlarda koltuklar sunucu tarafinda zaten serbest birakilmistir. */
const KOLTUK_SERBEST_KODLARI = new Set([
  'Payment.ReservationNotActive',
  'Payment.SeatsNoLongerHeld',
]);

function odemeHatasiniAcikla(hata: unknown, varsayilan: string): { mesaj: string; kod?: string } {
  const kod = hataKodu(hata);
  const mesaj = (kod && ODEME_HATA_METNI[kod]) || hataMesaji(hata, varsayilan);
  return { mesaj, kod };
}

/**
 * Odeme baslatma, tamamlama ve bilet gosterme akisi.
 *
 * Rezervasyonun kendisi `RezervasyonPage` ile ayni ucla cekiliyor; burasi
 * ustune odeme adimini ekliyor. Odeme kaydinin kimligi yalnizca bu sayfanin
 * durumunda tutuluyor — sayfa yenilenirse akis bastan baslar, cunku
 * rezervasyon uzerinden mevcut bir odemeyi bulan bir uc yok.
 *
 * <para>
 * Biletler odeme cevabindan DEGIL kendi ucundan okunuyor. Cevaptaki liste
 * yalnizca o cagriya ozel; sayfa yenilendiginde kaybolurdu ve kullanici
 * parasini odedigi bileti bir daha goremezdi.
 * </para>
 */
export function OdemePage() {
  const { rezervasyonId = '' } = useParams();
  const queryClient = useQueryClient();

  const [islemHatasi, setIslemHatasi] = useState<string | null>(null);
  const [koltuklarSerbest, setKoltuklarSerbest] = useState(false);
  const [odemeId, setOdemeId] = useState<string | null>(null);
  const [tamamlamaSonucu, setTamamlamaSonucu] = useState<OdemeTamamlamaSonucu | null>(null);

  /**
   * Bu odeme denemesinin idempotency anahtari.
   *
   * Rezervasyon bu sayfada degismiyor, dolayisiyla anahtar KoltukSecimPage'in
   * aksine hic sifirlanmiyor: ag koptugunda veya "Odemeyi baslat"a iki kez
   * basildiginda sunucu ayni anahtari gorup ikinci bir odeme kaydi acmaz,
   * ilkinin sonucunu doner.
   */
  const anahtarRef = useRef<string | null>(null);

  const {
    data: rezervasyon,
    isPending,
    isError,
    error,
  } = useQuery<Rezervasyon>({
    queryKey: ['reservation', rezervasyonId],
    queryFn: () => rezervasyonGetir(rezervasyonId),
    enabled: rezervasyonId.length > 0,
  });

  // Sayac bittiginde sunucuya bir kez daha soruluyor: gercek durum burada
  // degil sunucuda. Sayacin sifirlanmasi "suresi doldu" demek degil,
  // "sunucuya tekrar sor" demek.
  const sureDoldu = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ['reservation', rezervasyonId] });
  }, [queryClient, rezervasyonId]);

  const kalan = useGeriSayim(
    rezervasyon?.status === 'Pending' ? rezervasyon.remainingSeconds : undefined,
    sureDoldu,
  );

  // Odeme kaydinin guncel hali. `baslat` basarili olur olmaz cache'e
  // yaziliyor; bu yuzden ilk goruntude tekrar bir GET beklenmiyor.
  const { data: odemeVerisi } = useQuery<OdemeDetayi>({
    queryKey: ['payment', odemeId],
    queryFn: () => odemeGetir(odemeId as string),
    enabled: odemeId !== null,
  });

  // Bilet yalnizca odeme tamamlandiginda var; rezervasyon Confirmed
  // olmadan sorulsaydi her acilista bos donen bir istek atilirdi.
  const { data: biletler } = useQuery<Bilet[]>({
    queryKey: ['tickets', rezervasyonId],
    queryFn: () => biletlerimGetir(rezervasyonId),
    enabled: rezervasyon?.status === 'Confirmed',
  });

  const baslat = useMutation({
    mutationFn: () => {
      anahtarRef.current ??= crypto.randomUUID();
      return odemeBaslat(rezervasyonId, anahtarRef.current);
    },
    onSuccess: (detay) => {
      setIslemHatasi(null);
      setOdemeId(detay.id);
      queryClient.setQueryData(['payment', detay.id], detay);

      if (detay.redirectUrl) {
        // Gercek saglayici: tamamlama orada olur, sunucuya webhook ile doner.
        window.location.href = detay.redirectUrl;
      }
    },
    onError: (hata) => {
      const { mesaj, kod } = odemeHatasiniAcikla(hata, 'Ödeme başlatılamadı.');
      setIslemHatasi(mesaj);
      if (kod && KOLTUK_SERBEST_KODLARI.has(kod)) setKoltuklarSerbest(true);
    },
  });

  const tamamla = useMutation({
    mutationFn: () => odemeTamamla(odemeId as string),
    onSuccess: async (sonuc) => {
      setIslemHatasi(null);
      setTamamlamaSonucu(sonuc);

      if (sonuc.status === 'Failed') {
        // Bu cevap govdesinde sebep yok; tam detay (failureReason) ayrica
        // sorulmali. Taklit saglayicida tamamlama senkron oldugu icin
        // bu sorgu guncel sonucu hemen doner.
        setKoltuklarSerbest(true);
        await queryClient.invalidateQueries({ queryKey: ['payment', odemeId] });
        return;
      }

      if (sonuc.status === 'Succeeded') {
        // Rezervasyon artik Confirmed; detay sayfasi ve liste guncellenmeli.
        // Bilet listesi de: "Biletlerim" onbellekte eski hâliyle durur ve
        // kullanici az once aldigi bileti orada goremezdi.
        await Promise.all([
          queryClient.invalidateQueries({ queryKey: ['reservation', rezervasyonId] }),
          queryClient.invalidateQueries({ queryKey: ['tickets'] }),
        ]);
      }
    },
    onError: (hata) => {
      const { mesaj, kod } = odemeHatasiniAcikla(hata, 'Ödeme tamamlanamadı.');
      setIslemHatasi(mesaj);
      if (kod && KOLTUK_SERBEST_KODLARI.has(kod)) setKoltuklarSerbest(true);
    },
  });

  if (isPending) {
    return (
      <Sayfa>
        <div className="animate-pulse space-y-stack-sm" aria-hidden="true">
          <div className="h-6 w-56 rounded bg-surface-variant/60" />
          <div className="h-40 rounded-lg bg-surface-variant/40" />
        </div>
        <span className="sr-only" role="status">
          Rezervasyon yükleniyor
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
          {hataMesaji(error, 'Rezervasyon yüklenemedi.')}
        </p>
      </Sayfa>
    );
  }

  if (!rezervasyon) return null;

  const bekliyor = rezervasyon.status === 'Pending' && kalan > 0;
  const azKaldi = kalan > 0 && kalan <= UYARI_ESIGI_SANIYE;
  const yenidenSecLink = `/oturumlar/${rezervasyon.eventSessionId}/koltuklar`;

  return (
    <Sayfa>
      <header className="mb-stack-md">
        <p className="font-body text-label-caps uppercase tracking-widest text-primary">
          {rezervasyon.venueName} · {rezervasyon.hallName}
        </p>
        <h1 className="font-headline text-headline-md text-on-surface">
          {rezervasyon.eventTitle}
        </h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          {tarihBicimi.format(new Date(rezervasyon.sessionStartsAtUtc))}
        </p>
      </header>

      {bekliyor ? (
        <section
          aria-label="Kalan süre"
          className={`mb-stack-md rounded-lg border p-stack-sm ${
            azKaldi
              ? 'border-error/50 bg-error-container/20'
              : 'border-outline-variant/40 bg-surface-variant/30'
          }`}
        >
          <p className="font-body text-body-sm text-on-surface-variant">
            Koltukların ödeme için tutulma süresi:
          </p>
          {/* aria-hidden: sayac gorseldir, sesli uyari asagida ayrica verilir. */}
          <p
            className={`font-headline text-headline-md tabular-nums ${
              azKaldi ? 'text-error' : 'text-on-surface'
            }`}
            aria-hidden="true"
          >
            {sureBicimle(kalan)}
          </p>
          <p role="status" aria-live="polite" className="sr-only">
            {azKaldi ? 'Bir dakikadan az süre kaldı.' : ''}
          </p>
        </section>
      ) : (
        /*
          Odenmis rezervasyonda "zaten odendi" uyarisi hic cikmiyor.
          Odemeyi az once yapan kullaniciya sacma geliyordu; sonradan geri
          donen kullanici icin de bilgi degeri yok, asagida zaten biletleri
          duruyor. Uyari yalnizca iptal ve suresi dolmus durumlar icin
          anlamli.
        */
        rezervasyon.status !== 'Confirmed' && (
          <p
            role="status"
            className="mb-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/30 px-stack-sm py-stack-sm font-body text-body-md text-on-surface"
          >
            {rezervasyon.status === 'Pending'
              ? REZERVASYON_DURUM_METNI.Expired
              : REZERVASYON_DURUM_METNI[rezervasyon.status]}
          </p>
        )
      )}

      <section
        aria-label="Koltuklar"
        className="mb-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-sm"
      >
        <ul className="space-y-base">
          {rezervasyon.seats.map((koltuk) => (
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
          Toplam:{' '}
          <strong className="font-semibold">{paraBicimi.format(rezervasyon.totalAmount)}</strong>
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

      {/* Odeme / bilet alani */}
      {rezervasyon.status === 'Confirmed' ? (
        <section aria-label="Biletler" className="space-y-stack-md">
          <p className="font-body text-body-md font-semibold text-on-surface">
            {tamamlamaSonucu?.status === 'Succeeded'
              ? 'Ödeme tamamlandı, biletlerin hazır.'
              : 'Biletlerin hazır.'}
          </p>

          {biletler?.map((bilet) => <BiletKarti key={bilet.id} bilet={bilet} />)}
        </section>
      ) : tamamlamaSonucu?.status === 'Failed' ? (
        <section
          aria-label="Ödeme başarısız"
          className="rounded-lg border border-error/40 bg-error-container/20 p-stack-sm"
        >
          <p className="font-body text-body-md text-error">
            {odemeVerisi?.failureReason ?? 'Ödeme sağlayıcı işlemi reddetti.'}
          </p>
          <p className="mt-base font-body text-body-sm text-on-surface-variant">
            Koltuklar serbest bırakıldı.
          </p>
        </section>
      ) : odemeId !== null ? (
        odemeVerisi?.redirectUrl ? (
          <section
            aria-label="Sağlayıcıya yönlendiriliyor"
            className="rounded-lg border border-outline-variant/40 bg-surface-variant/30 p-stack-sm"
          >
            <p className="mb-stack-sm font-body text-body-sm text-on-surface-variant">
              Ödeme sayfasına yönlendiriliyorsun. Yönlendirme başlamadıysa aşağıdaki bağlantıyı
              kullan.
            </p>
            <a
              href={odemeVerisi.redirectUrl}
              className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary"
            >
              Ödeme sayfasına git
            </a>
          </section>
        ) : (
          <button
            type="button"
            onClick={() => tamamla.mutate()}
            disabled={tamamla.isPending}
            className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary disabled:opacity-60"
          >
            {tamamla.isPending ? 'Tamamlanıyor' : 'Ödemeyi tamamla'}
          </button>
        )
      ) : bekliyor ? (
        <button
          type="button"
          onClick={() => baslat.mutate()}
          disabled={baslat.isPending}
          className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary disabled:opacity-60"
        >
          {baslat.isPending ? 'Başlatılıyor' : 'Ödemeyi başlat'}
        </button>
      ) : null}

      {(koltuklarSerbest ||
        rezervasyon.status === 'Cancelled' ||
        rezervasyon.status === 'Expired') && (
        <div className="mt-stack-sm">
          <Link
            to={yenidenSecLink}
            className="font-body text-body-sm text-primary underline underline-offset-2"
          >
            Koltukları yeniden seç
          </Link>
        </div>
      )}

      <div className="mt-stack-md">
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
