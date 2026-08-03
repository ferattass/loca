import { useCallback, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';

import { hataKodu, hataMesaji } from '../api/client';
import {
  koltukMusaitligiGetir,
  type KoltukDurumu,
  type KoltukMusaitligi,
} from '../api/eventSessions';
import { rezervasyonOlustur } from '../api/reservations';
import { BolumHaritasi, type BolumOzeti } from '../components/BolumHaritasi';
import { DOLGU, SeatMap, type BolumVerisi } from '../components/SeatMap';
import type { SeatStatus } from '../components/SeatStatePreview';

/** Sunucu durumunun gorsel karsiligi. */
const GORSEL_DURUM: Record<KoltukDurumu, SeatStatus> = {
  Available: 'Available',
  // Rezerve koltuk odemesi surmekte olan koltuktur. Kullanici acisindan
  // kilitli koltuktan farki yok: ikisi de "baskasi isliyor". Ayri bir renk
  // eklemek bes durumluk tasarim sistemini altiya cikarirdi.
  Reserved: 'Locked',
  Locked: 'Locked',
  Sold: 'Sold',
  Disabled: 'Disabled',
};

/** Sartname: bir kullanici tek oturumda en fazla bu kadar bilet alabilir. */
const EN_FAZLA_KOLTUK = 6;

const paraBicimi = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 2,
});

/**
 * Koltuk secimi ve rezervasyon acma ekrani.
 *
 * Secim yalnizca arayuzde tutulur; koltuklar ancak "Koltuklari kilitle"
 * denince sunucuda kilitlenir. Secildigi anda kilitlenseydi, plana bakip
 * vazgecen her kullanici koltuklari on dakika bloke ederdi.
 */
export function KoltukSecimPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [seciliIdler, setSeciliIdler] = useState<ReadonlySet<string>>(new Set());

  /**
   * Koltuklari acilmis bolum. `null` ise salon genel gorunumu.
   *
   * Iki seviyeli akis: once "salonun neresi" sorusu, sonra koltuk secimi.
   * Iki yuz koltugu tek ekranda gostermek kullaniciyi bu ilk soruyu
   * atlamaya zorluyordu.
   */
  const [acikBolumId, setAcikBolumId] = useState<string | null>(null);
  const [uyari, setUyari] = useState<string | null>(null);

  /**
   * Bu denemenin idempotency anahtari.
   *
   * Ayni secimle yapilan tekrar denemelerde AYNI anahtar gonderilir: ag
   * koptugunda istek sunucuya ulasmis ama cevap donmemis olabilir. Her
   * denemede yeni anahtar uretilseydi kullanici ayni koltuklar icin iki
   * rezervasyon acardi. Secim degisince anahtar sifirlanir — artik farkli
   * bir istektir.
   */
  const anahtarRef = useRef<string | null>(null);

  const { data, isPending, isError, error } = useQuery<KoltukMusaitligi>({
    queryKey: ['seat-availability', id],
    queryFn: () => koltukMusaitligiGetir(id),
    enabled: id.length > 0,
    // Baskasinin aldigi koltuk ekranda bos gorunmeye devam etmesin.
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
  });

  const { bolumler, durumlar, fiyatlar } = useMemo(() => {
    const bolumListesi: BolumVerisi[] = [];
    const durumHaritasi = new Map<string, SeatStatus>();
    const fiyatHaritasi = new Map<string, { tutar: number; tur: string; etiket: string }>();

    for (const bolum of data?.sections ?? []) {
      bolumListesi.push({
        id: bolum.id,
        name: bolum.name,
        displayOrder: bolum.displayOrder,
        seats: bolum.seats.map((koltuk) => ({
          // SeatMap koltugu kendi kimligiyle ciziyor; rezervasyon uclari
          // EventSeat kimligini bekliyor. Ikisi ayni olsun diye burada
          // eventSeatId kullaniliyor.
          id: koltuk.eventSeatId,
          rowLabel: koltuk.rowLabel,
          seatNumber: koltuk.seatNumber,
          label: `${koltuk.rowLabel}-${koltuk.seatNumber}`,
          positionX: koltuk.positionX,
          positionY: koltuk.positionY,
          isActive: koltuk.status !== 'Disabled',
        })),
      });

      for (const koltuk of bolum.seats) {
        durumHaritasi.set(koltuk.eventSeatId, GORSEL_DURUM[koltuk.status]);
        fiyatHaritasi.set(koltuk.eventSeatId, {
          tutar: koltuk.price,
          tur: koltuk.ticketTypeName,
          etiket: `${bolum.name} ${koltuk.rowLabel}-${koltuk.seatNumber}`,
        });
      }
    }

    return { bolumler: bolumListesi, durumlar: durumHaritasi, fiyatlar: fiyatHaritasi };
  }, [data]);

  // Bolum kartlari icin ozet: kac koltuk bos, en dusuk fiyat ne.
  const bolumOzetleri = useMemo<BolumOzeti[]>(
    () =>
      (data?.sections ?? []).map((bolum) => {
        const musait = bolum.seats.filter((koltuk) => koltuk.status === 'Available');

        return {
          id: bolum.id,
          ad: bolum.name,
          siraNo: bolum.displayOrder,
          toplamKoltuk: bolum.seats.length,
          musaitKoltuk: musait.length,
          // Fiyat yalnizca SATILABILIR koltuklardan hesaplaniyor: tukenmis
          // ucuz bolumun fiyatini gostermek yaniltici olurdu.
          enDusukFiyat: musait.length > 0 ? Math.min(...musait.map((k) => k.price)) : null,
          paraBirimi: bolum.seats[0]?.currency ?? 'TRY',
        };
      }),
    [data],
  );

  // Hangi bolumde kac koltuk secildi. Kullanici bolumler arasi gezerken
  // secimini kaybetmiyor; kartta gorunuyor.
  const bolumBazliSecim = useMemo(() => {
    const sayac = new Map<string, number>();

    for (const bolum of data?.sections ?? []) {
      const adet = bolum.seats.filter((koltuk) => seciliIdler.has(koltuk.eventSeatId)).length;
      if (adet > 0) sayac.set(bolum.id, adet);
    }

    return sayac;
  }, [data, seciliIdler]);

  const acikBolum = bolumler.find((bolum) => bolum.id === acikBolumId) ?? null;

  const secilenler = useMemo(
    () => [...seciliIdler].map((koltukId) => ({ koltukId, ...fiyatlar.get(koltukId) })),
    [seciliIdler, fiyatlar],
  );

  // Ekranda gosterilen toplam yalnizca ONIZLEME. Odenecek tutar sunucunun
  // dondugu rezervasyondaki `totalAmount`; istemcinin hesabi baglayici degil.
  const onizlemeToplam = secilenler.reduce((toplam, koltuk) => toplam + (koltuk.tutar ?? 0), 0);

  const olustur = useMutation({
    mutationFn: () => {
      anahtarRef.current ??= crypto.randomUUID();
      return rezervasyonOlustur(id, [...seciliIdler], anahtarRef.current);
    },
    onSuccess: (rezervasyon) => {
      setSeciliIdler(new Set());
      anahtarRef.current = null;
      navigate(`/rezervasyonlar/${rezervasyon.id}`);
    },
    onError: async (hata) => {
      const kod = hataKodu(hata);

      // Karar MESAJA degil KODA gore veriliyor: mesaj metni degistiginde
      // arayuz mantigi kirilmasin.
      if (kod === 'Reservation.SeatNotAvailable' || kod === 'Reservation.SeatTakenConcurrently') {
        setSeciliIdler(new Set());
        anahtarRef.current = null;
        setUyari('Sectigin koltuklardan bazilari az once alindi. Plan yenilendi, tekrar sec.');
        await queryClient.invalidateQueries({ queryKey: ['seat-availability', id] });
        return;
      }

      // Kalan hatalarda anahtar KORUNUYOR: istek sunucuya ulasmis ama cevap
      // donmemis olabilir. Ayni anahtarla tekrar denendiginde sunucu ikinci
      // bir rezervasyon acmaz, ilkinin sonucunu doner.
      setUyari(hataMesaji(hata, 'Rezervasyon acilamadi.'));
    },
  });

  // Referansi sabit: her cizimde yeni fonksiyon uretilseydi React.memo ile
  // sarilan koltuklarin tamami yeniden cizilirdi.
  const koltukSec = useCallback((koltukId: string) => {
    setUyari(null);
    // Secim degisti; artik farkli bir istek.
    anahtarRef.current = null;

    setSeciliIdler((oncekiler) => {
      const yeni = new Set(oncekiler);

      if (yeni.has(koltukId)) {
        yeni.delete(koltukId);
        return yeni;
      }

      if (yeni.size >= EN_FAZLA_KOLTUK) {
        setUyari(`Bir oturumda en fazla ${EN_FAZLA_KOLTUK} koltuk secebilirsin.`);
        return oncekiler;
      }

      yeni.add(koltukId);
      return yeni;
    });
  }, []);

  if (isPending) {
    return (
      <Sayfa>
        <div className="animate-pulse space-y-stack-sm" aria-hidden="true">
          <div className="h-6 w-64 rounded bg-surface-variant/60" />
          <div className="h-96 rounded-lg bg-surface-variant/40" />
        </div>
        <span className="sr-only" role="status">
          Koltuk plani yukleniyor
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
          {hataMesaji(error, 'Koltuk plani yuklenemedi.')}
        </p>
      </Sayfa>
    );
  }

  return (
    <Sayfa>
      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-md text-on-surface">Koltuk secimi</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          En fazla {EN_FAZLA_KOLTUK} koltuk secebilirsin. Koltuklar kilitlendikten sonra
          odemeyi tamamlaman icin sinirli suren olur.
        </p>
      </header>

      {uyari && (
        <p
          role="alert"
          className="mb-stack-sm rounded-md border border-tertiary/40 bg-tertiary-container/20 px-stack-sm py-base font-body text-body-sm text-tertiary"
        >
          {uyari}
        </p>
      )}

      {acikBolum === null ? (
        <BolumHaritasi
          bolumler={bolumOzetleri}
          seciliBolumId={null}
          onBolumSec={(bolumId) => {
            setUyari(null);
            setAcikBolumId(bolumId);
          }}
          seciliKoltukSayilari={bolumBazliSecim}
        />
      ) : (
        <>
          <div className="mb-stack-sm flex flex-wrap items-center justify-between gap-stack-sm">
            <button
              type="button"
              onClick={() => setAcikBolumId(null)}
              className="font-body text-body-sm text-primary underline underline-offset-2"
            >
              ← Tüm bölümler
            </button>

            <p className="font-headline text-body-md font-semibold text-on-surface">
              {acikBolum.name}
            </p>
          </div>

          <Aciklama />

          {/*
            Yalnizca ACIK BOLUMUN koltuklari ciziliyor. Tamami cizilseydi hem
            ekran yine kalabaliklasir hem de alti yuz koltuklu bir salonda
            gereksiz yere alti yuz SVG ogesi olusurdu.
          */}
          <SeatMap
            bolumler={[acikBolum]}
            seciliIdler={seciliIdler}
            sunucuDurumlari={durumlar}
            onKoltukSec={koltukSec}
          />
        </>
      )}

      {seciliIdler.size > 0 && (
        <section
          aria-label="Secim ozeti"
          className="mt-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/30 p-stack-sm"
        >
          <ul className="mb-stack-sm space-y-base">
            {secilenler.map((koltuk) => (
              <li
                key={koltuk.koltukId}
                className="flex items-center justify-between font-body text-body-sm text-on-surface"
              >
                <span>
                  {koltuk.etiket}
                  <span className="ml-base text-on-surface-variant">({koltuk.tur})</span>
                </span>
                <span>{paraBicimi.format(koltuk.tutar ?? 0)}</span>
              </li>
            ))}
          </ul>

          <div className="flex flex-wrap items-center justify-between gap-stack-sm border-t border-outline-variant/30 pt-stack-sm">
            <p className="font-body text-body-md text-on-surface">
              {seciliIdler.size} koltuk ·{' '}
              <strong className="font-semibold">{paraBicimi.format(onizlemeToplam)}</strong>
            </p>

            <div className="flex items-center gap-stack-sm">
              <button
                type="button"
                onClick={() => {
                  setSeciliIdler(new Set());
                  anahtarRef.current = null;
                }}
                className="font-body text-body-sm text-primary underline underline-offset-2"
              >
                Secimi temizle
              </button>

              <button
                type="button"
                onClick={() => olustur.mutate()}
                disabled={olustur.isPending}
                className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary disabled:opacity-60"
              >
                {olustur.isPending ? 'Kilitleniyor' : 'Koltuklari kilitle'}
              </button>
            </div>
          </div>

          <p className="mt-base font-body text-[11px] text-on-surface-variant">
            Odenecek tutar sunucuda hesaplanir; buradaki toplam onizlemedir.
          </p>
        </section>
      )}
    </Sayfa>
  );
}

/**
 * Renklerin ne anlama geldigi.
 *
 * Renk tek ayirt edici olamaz: renk koru bir kullanici turuncu ile griyi
 * ayirt edemez. Koltuklarin kendi `aria-label`'i de durumu metin olarak
 * tasiyor; bu liste goren kullanici icin ek bir anahtar.
 */
function Aciklama() {
  const ogeler: Array<{ durum: SeatStatus; metin: string }> = [
    { durum: 'Available', metin: 'Bos' },
    { durum: 'Selected', metin: 'Sectiklerin' },
    { durum: 'Locked', metin: 'Baskasi tutuyor' },
    { durum: 'Sold', metin: 'Satilmis' },
    { durum: 'Disabled', metin: 'Devre disi' },
  ];

  // Renkler SeatMap'ten geliyor; burada tekrar yazilsaydi ikisi zamanla ayrisirdi.
  const renk = DOLGU;

  return (
    <ul className="mb-stack-sm flex flex-wrap gap-stack-sm">
      {ogeler.map(({ durum, metin }) => (
        <li
          key={durum}
          className="flex items-center gap-base font-body text-body-sm text-on-surface-variant"
        >
          <span
            aria-hidden="true"
            className="inline-block h-3 w-3 rounded-sm border border-outline-variant/60"
            style={{ backgroundColor: renk[durum] }}
          />
          {metin}
        </li>
      ))}
    </ul>
  );
}

function Sayfa({ children }: { children: React.ReactNode }) {
  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-5xl">{children}</div>
    </main>
  );
}
