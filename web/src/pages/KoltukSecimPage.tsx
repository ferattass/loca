import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
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
import { SecimOzetiPaneli, type SecimOzetiKoltugu } from '../components/SecimOzetiPaneli';
import { DOLGU, SeatMap, type BolumVerisi } from '../components/SeatMap';
import { SolOkIkonu } from '../components/ui/Ikon';
import type { SeatStatus } from '../components/SeatStatePreview';
import { sureBicimle } from '../hooks/useGeriSayim';
import { uzunTarihSaatBicimi } from '../lib/bicim';
import { Sayfa } from '../components/ui/Sayfa';

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

/** Plan kac saniyede bir tazeleniyor; ust bilgideki gostergeyle de esler. */
const YENILEME_ARALIGI_SANIYE = 15;

/**
 * Koltuk secimi ve rezervasyon acma ekrani.
 *
 * Secim yalnizca arayuzde tutulur; koltuklar ancak "Odemeye gec" denince
 * sunucuda kilitlenir. Secildigi anda kilitlenseydi, plana bakip vazgecen
 * her kullanici koltuklari on dakika bloke ederdi.
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

  const { data, dataUpdatedAt, isPending, isError, error } = useQuery<KoltukMusaitligi>({
    queryKey: ['seat-availability', id],
    queryFn: () => koltukMusaitligiGetir(id),
    enabled: id.length > 0,
    // Baskasinin aldigi koltuk ekranda bos gorunmeye devam etmesin.
    refetchInterval: YENILEME_ARALIGI_SANIYE * 1_000,
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

  const secilenler = useMemo<SecimOzetiKoltugu[]>(
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
        setUyari('Seçtiğin koltuklardan bazıları az önce alındı. Plan yenilendi, tekrar seç.');
        await queryClient.invalidateQueries({ queryKey: ['seat-availability', id] });
        return;
      }

      // Kalan hatalarda anahtar KORUNUYOR: istek sunucuya ulasmis ama cevap
      // donmemis olabilir. Ayni anahtarla tekrar denendiginde sunucu ikinci
      // bir rezervasyon acmaz, ilkinin sonucunu doner.
      setUyari(hataMesaji(hata, 'Rezervasyon açılamadı.'));
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
        setUyari(`Bir oturumda en fazla ${EN_FAZLA_KOLTUK} koltuk seçebilirsin.`);
        return oncekiler;
      }

      yeni.add(koltukId);
      return yeni;
    });
  }, []);

  const temizle = useCallback(() => {
    setSeciliIdler(new Set());
    anahtarRef.current = null;
  }, []);

  if (isPending) {
    return (
      <Sayfa genislik="6xl">
        <div className="animate-pulse space-y-stack-sm" aria-hidden="true">
          <div className="h-6 w-64 rounded bg-surface-variant/60" />
          <div className="h-96 rounded-lg bg-surface-variant/40" />
        </div>
        <span className="sr-only" role="status">
          Koltuk planı yükleniyor
        </span>
      </Sayfa>
    );
  }

  if (isError) {
    return (
      <Sayfa genislik="6xl">
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hataMesaji(error, 'Koltuk planı yüklenemedi.')}
        </p>
      </Sayfa>
    );
  }

  return (
    <Sayfa genislik="6xl">
      <BaslikBlogu musaitlik={data} dataUpdatedAt={dataUpdatedAt} />

      {uyari && (
        <p
          role="alert"
          className="mb-stack-sm rounded-md border border-tertiary/40 bg-tertiary-container/20 px-stack-sm py-base font-body text-body-sm text-tertiary"
        >
          {uyari}
        </p>
      )}

      <div className="grid grid-cols-1 gap-stack-lg lg:grid-cols-[minmax(0,1fr)_360px] lg:items-start">
        <div className="min-w-0">
          {acikBolum === null ? (
            <div className="glass rounded-lg p-stack-sm md:p-stack-md">
              <BolumHaritasi
                bolumler={bolumOzetleri}
                seciliBolumId={null}
                onBolumSec={(bolumId) => {
                  setUyari(null);
                  setAcikBolumId(bolumId);
                }}
                seciliKoltukSayilari={bolumBazliSecim}
              />
            </div>
          ) : (
            <>
              <button
                type="button"
                onClick={() => setAcikBolumId(null)}
                className="mb-stack-sm font-body text-body-sm text-primary underline underline-offset-2"
              >
                <SolOkIkonu className="h-4 w-4" />
                Tüm bölümler
              </button>

              <Aciklama />

              {/*
                Yalnizca ACIK BOLUMUN koltuklari ciziliyor. Tamami cizilseydi hem
                ekran yine kalabaliklasir hem de alti yuz koltuklu bir salonda
                gereksiz yere alti yuz SVG ogesi olusurdu.
              */}
              <div className="rounded-lg border border-outline-variant/40 bg-surface-container-low/40 p-stack-sm md:p-stack-md">
                <SeatMap
                  bolumler={[acikBolum]}
                  seciliIdler={seciliIdler}
                  sunucuDurumlari={durumlar}
                  onKoltukSec={koltukSec}
                />
              </div>

              <div className="mt-stack-sm">
                <p className="font-body text-label-caps uppercase tracking-widest text-on-surface-variant">
                  Mevcut görünüm
                </p>
                <p className="font-headline text-body-md font-semibold text-on-surface">
                  {acikBolum.name}
                </p>
              </div>
            </>
          )}
        </div>

        <aside className="lg:sticky lg:top-stack-md">
          <SecimOzetiPaneli
            koltuklar={secilenler}
            toplam={onizlemeToplam}
            yukleniyor={olustur.isPending}
            onOdemeyeGec={() => olustur.mutate()}
            onTemizle={temizle}
          />
        </aside>
      </div>
    </Sayfa>
  );
}

/**
 * Etkinlik baslik blogu.
 *
 * <b>Sag tarafta tasarimdaki geri sayim kartinin YERINE ne var.</b> Figma
 * referansi burada "SURE DOLUYOR / 9:56" gibi bir kilit sayaci gosteriyor;
 * ama bu ekranda henuz kilitlenmis bir rezervasyon YOK — kilit ancak
 * "Odemeye gec" tiklaninca acilir (yukaridaki JSDoc'ta da anlatiliyor).
 * Sahte bir geri sayim koymak "suren azaliyor, acele et" izlenimi verip
 * kullaniciyi yanlis yonlendirirdi. Onun yerine GERCEK bir sure gosteriliyor:
 * planin otomatik yenilenmesine (react-query'nin refetchInterval'i) kalan
 * sure. Ayni gorsel agirlik, dogru bilgi.
 */

/**
 * Etkinlik bilgisi koltuk musaitligi yanitindan geliyor.
 *
 * Once sayfa yalnizca "Koltuk secimi" yaziyordu; kullanici hangi oturum icin
 * koltuk sectigini goremiyor ve yanlis gune bilet alabiliyordu. Baslik ayri
 * bir istekle cekilseydi plan ile baslik farkli anlarda gelirdi.
 */
function BaslikBlogu({
  musaitlik,
  dataUpdatedAt,
}: {
  musaitlik: KoltukMusaitligi;
  dataUpdatedAt: number;
}) {
  return (
    <header className="glass mb-stack-md flex flex-col gap-stack-md rounded-lg p-stack-md lg:flex-row lg:items-center lg:justify-between">
      <div>
        <span className="inline-flex items-center gap-base rounded-full border border-outline-variant/60 bg-surface-container-high/60 px-stack-sm py-[2px] font-body text-label-caps uppercase tracking-widest text-on-surface-variant">
          <span
            aria-hidden="true"
            className="h-1.5 w-1.5 rounded-full bg-secondary animate-pulse-urgent"
          />
          {musaitlik.venueName} · {musaitlik.hallName}
        </span>

        <h1 className="mt-stack-sm font-display text-display-lg-mobile text-on-surface md:text-display-lg">
          {musaitlik.eventTitle}
        </h1>

        <p className="mt-base font-body text-body-md text-primary">
          {uzunTarihSaatBicimi.format(new Date(musaitlik.startsAtUtc))}
        </p>

        <p className="mt-base max-w-xl font-body text-body-sm text-on-surface-variant">
          En fazla {EN_FAZLA_KOLTUK} koltuk seçebilirsin. Koltuklar yalnızca arayüzde tutulur;
          ödemeye geçtiğinde sunucuda kısa süreliğine kilitlenir.
        </p>
      </div>

      <YenilemeKarti dataUpdatedAt={dataUpdatedAt} />
    </header>
  );
}

/**
 * Planin bir sonraki otomatik yenilemesine kalan sure.
 *
 * Baslangic noktasi react-query'nin `dataUpdatedAt` degeri — yani planin
 * SUNUCUDAN son geldigi an. Yerel bir sayac sifirdan baslatilsaydi, sekme
 * arka plandayken atlanan yenilemelerde gosterge gercek durumdan kopardi.
 *
 * Butunuyle `aria-hidden`: ekran okuyucu icin bu bir uyari degil, sadece
 * gorsel bir suslemedir. Saniyede birkaç kez guncellenen bir metni "canli
 * bolge" yapsaydik ekran okuyucu her tikte anons eder, kullaniciyi yorardi.
 * Koltuklarin gercek durumu zaten her koltugun kendi aria-label'inda var.
 */
function YenilemeKarti({ dataUpdatedAt }: { dataUpdatedAt: number }) {
  const [simdi, setSimdi] = useState(() => Date.now());

  useEffect(() => {
    const zamanlayici = window.setInterval(() => setSimdi(Date.now()), 250);
    return () => window.clearInterval(zamanlayici);
  }, []);

  const gecenSaniye = Math.max(0, (simdi - dataUpdatedAt) / 1000);
  const kalanSaniye = Math.max(0, Math.ceil(YENILEME_ARALIGI_SANIYE - gecenSaniye));
  const kalanYuzde = Math.min(100, Math.max(0, (kalanSaniye / YENILEME_ARALIGI_SANIYE) * 100));

  return (
    <div aria-hidden="true" className="glass flex items-center gap-stack-sm rounded-lg px-stack-md py-stack-sm">
      <div>
        <p className="font-body text-label-caps uppercase tracking-widest text-secondary">
          Otomatik yenileme
        </p>
        <p className="tabular font-display text-display-lg-mobile text-on-surface">
          {sureBicimle(kalanSaniye)}
        </p>
      </div>

      <div className="flex h-10 w-1.5 flex-col-reverse overflow-hidden rounded-full bg-surface-container-low md:h-12">
        <div
          className="w-full rounded-full bg-secondary transition-[height] duration-200"
          style={{ height: `${kalanYuzde}%` }}
        />
      </div>
    </div>
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
    { durum: 'Sold', metin: 'Satılmış' },
    { durum: 'Available', metin: 'Boş' },
    { durum: 'Selected', metin: 'Seçili' },
    { durum: 'Locked', metin: 'Tutuluyor' },
    { durum: 'Disabled', metin: 'Devre dışı' },
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

