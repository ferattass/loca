import { memo, useId, useMemo } from 'react';

import type { SeatStatus } from './SeatStatePreview';

export interface KoltukVerisi {
  id: string;
  rowLabel: string;
  seatNumber: number;
  label: string;
  positionX: number;
  positionY: number;
  isActive: boolean;
}

export interface BolumVerisi {
  id: string;
  name: string;
  displayOrder: number;
  seats: KoltukVerisi[];
}

interface SeatMapProps {
  bolumler: BolumVerisi[];
  seciliIdler?: ReadonlySet<string>;
  /**
   * Sunucudan gelen koltuk durumlari (koltuk kimligi → durum).
   *
   * Verildiginde `isActive` yerine BU esas alinir: yonetim ekraninda koltugun
   * satisa acik olup olmadigi, rezervasyon ekraninda ise o oturumdaki gercek
   * durumu (kilitli, satilmis) onemli. Iki ekran ayni bileseni kullaniyor
   * ama farkli soruya cevap veriyor.
   */
  sunucuDurumlari?: ReadonlyMap<string, SeatStatus>;
  /** Verilmezse plan salt okunur cizilir. */
  onKoltukSec?: (koltukId: string) => void;
}

/** Koltuk karesinin kenar uzunlugu (SVG birimi). */
const KOLTUK = 22;

/** Bolum basliginin altinda birakilan bosluk. */
const BASLIK_YUKSEKLIGI = 34;

/**
 * Sahne bandinin yuksekligi ve altindaki bosluk.
 *
 * Sahne yalnizca bir yazi degil, cizilen bir seklin kendisi: seyirci plana
 * baktiginda "on taraf neresi" sorusunu okumadan anlamali. Salon plani ters
 * okunursa kullanici sahneye en uzak koltugu en yakin sanip satin alir.
 */
const SAHNE_YUKSEKLIGI = 34;
const SAHNE_ALT_BOSLUK = 26;

/**
 * Sahne onunun asagi dogru sistigi mesafe.
 *
 * Duz bir dikdortgen salon planindan cok bir baslik cubuguna benziyordu.
 * Gercek sahne onleri seyirciye dogru cikinti yapar; hafif bir kavis, plani
 * "sahne buradan basliyor" diye okutuyor.
 */
const SAHNE_KAVIS = 9;

/** Sira etiketleri icin solda ayrilan sutun genisligi. */
const SIRA_SUTUNU = 26;

/**
 * Koltuk dolgu renkleri.
 *
 * Disari aciliyor cunku plan altindaki renk aciklamasi da ayni degerleri
 * kullaniyor. Iki yerde ayri ayri yazilsaydi biri degistirildiginde
 * aciklama yanlis rengi anlatir ve kullanici plani yanlis okurdu.
 */
export const DOLGU: Record<SeatStatus, string> = {
  Available: '#3b3742',
  Selected: '#d0bcff',
  Locked: '#ffb869',
  Sold: '#494454',
  Disabled: '#211e27',
};

const KENAR: Record<SeatStatus, string> = {
  Available: '#958ea0',
  Selected: '#d0bcff',
  Locked: '#ffb869',
  Sold: '#494454',
  Disabled: '#494454',
};

/** Ekran okuyucu ve ipucu metni icin durum adlari. */
const DURUM_ADI: Record<SeatStatus, string> = {
  Available: '',
  Selected: 'secili',
  Locked: 'baskasi tutuyor',
  Sold: 'satilmis',
  Disabled: 'devre disi',
};

/**
 * Tek bir koltuk.
 *
 * React.memo ile sarili: alti yuz koltuklu bir planda tek koltuk secildiginde
 * digerlerinin yeniden cizilmesi gozle gorulur bir takilmaya yol aciyor.
 * Karsilastirma props uzerinden yapildigi icin yalnizca durumu degisen
 * koltuk yeniden ciziliyor.
 */
const Koltuk = memo(function Koltuk({
  koltuk,
  durum,
  x,
  y,
  secilebilir,
  onSec,
}: {
  koltuk: KoltukVerisi;
  durum: SeatStatus;
  x: number;
  y: number;
  secilebilir: boolean;
  onSec?: (koltukId: string) => void;
}) {
  const secili = durum === 'Selected';

  const tikla = () => {
    if (secilebilir) onSec?.(koltuk.id);
  };

  return (
    <g
      // Koltuk bir onay kutusu gibi davraniyor: secilir, secimi kalkar.
      // Fare kullanamayan kullanici da sekme ve bosluk tusuyla
      // ayni islemi yapabilmeli.
      role={secilebilir ? 'checkbox' : 'img'}
      aria-checked={secilebilir ? secili : undefined}
      // Durum etikete yaziliyor: renk tek ayirt edici olamaz. Ekran okuyucu
      // kullanan biri "A-12" duyup koltugun satilmis oldugunu bilemezdi.
      aria-label={`${koltuk.label}${DURUM_ADI[durum] ? `, ${DURUM_ADI[durum]}` : ''}`}
      aria-disabled={!secilebilir || undefined}
      tabIndex={secilebilir ? 0 : -1}
      onClick={tikla}
      onKeyDown={(olay) => {
        if (olay.key === ' ' || olay.key === 'Enter') {
          olay.preventDefault();
          tikla();
        }
      }}
      className={secilebilir ? 'cursor-pointer outline-none focus-visible:opacity-80' : ''}
    >
      <rect
        x={x}
        y={y}
        width={KOLTUK}
        height={KOLTUK}
        rx={4}
        fill={DOLGU[durum]}
        stroke={KENAR[durum]}
        strokeWidth={secili ? 2 : 1}
      />
      <title>
        {koltuk.label}
        {DURUM_ADI[durum] ? ` - ${DURUM_ADI[durum]}` : ''}
      </title>
    </g>
  );
});

/**
 * Oturma planinin gorsel cizimi.
 *
 * Koltuklarin yerlesimi sunucudan gelen PositionX/PositionY degerlerine gore.
 * Konumlar planin uretimi sirasinda hesaplandigi icin arayuz yalnizca ciziyor;
 * yerlesim mantigi burada tekrarlanmiyor.
 *
 * Butun plan TEK bir SVG icinde. Her koltuk ayri bir HTML ogesi olsaydi
 * alti yuz koltuk alti yuz DOM dugumu ve o kadar da stil hesabi demek olurdu.
 */
export function SeatMap({
  bolumler,
  seciliIdler,
  sunucuDurumlari,
  onKoltukSec,
}: SeatMapProps) {
  const secilebilir = onKoltukSec !== undefined;

  // Bolum yerlesimi ve tuval olculeri yalnizca plan degistiginde hesaplaniyor;
  // her secimde yeniden hesaplansaydi secim gecikmeli hissedilirdi.
  const { yerlesim, genislik, yukseklik } = useMemo(() => {
    const sirali = [...bolumler].sort(
      (a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name, 'tr'),
    );

    // Yerlesim sahnenin ALTINDAN basliyor; sifirdan baslasaydi ilk bolumun
    // basligi sahne bandinin uzerine binerdi.
    let ofsetY = SAHNE_YUKSEKLIGI + SAHNE_ALT_BOSLUK;
    let enGenis = 0;

    const hesaplanan = sirali.map((bolum) => {
      const xler = bolum.seats.map((k) => k.positionX);
      const yler = bolum.seats.map((k) => k.positionY);

      // Sinira 0 EKLENMIYOR.
      // Onceki hâl Math.min(...konumlar, 0) yaziyordu; bu, konumlari sifirdan
      // buyuk baslayan bir bolumu her zaman sifirdan basliyor sayiyordu.
      // Uc bolumlu bir planda ikinci bolum originY=400 ile uretilince
      // bolumun ustunde 400 birimlik bos alan olusuyor ve plan ekrana
      // sigmiyordu. Bolumler ust uste degil ALT ALTA diziliyor; her birinin
      // kendi baslangici kendi koltuklarindan okunmali.
      const enKucukX = xler.length > 0 ? Math.min(...xler) : 0;
      const enKucukY = yler.length > 0 ? Math.min(...yler) : 0;
      const enBuyukX = xler.length > 0 ? Math.max(...xler) : 0;
      const enBuyukY = yler.length > 0 ? Math.max(...yler) : 0;

      const bolumGenisligi = enBuyukX - enKucukX + KOLTUK + SIRA_SUTUNU;
      const bolumYuksekligi = enBuyukY - enKucukY + KOLTUK;

      // Her siranin etiketi, o siradaki koltuklarin soluna yazilir.
      // Etiket olmadan kullanici hangi sirada oldugunu ancak sayarak bulur.
      const siraEtiketleri = new Map<string, number>();
      for (const koltuk of bolum.seats) {
        const mevcut = siraEtiketleri.get(koltuk.rowLabel);
        if (mevcut === undefined || koltuk.positionY < mevcut) {
          siraEtiketleri.set(koltuk.rowLabel, koltuk.positionY);
        }
      }

      const sonuc = {
        bolum,
        ustY: ofsetY,
        kaydirX: SIRA_SUTUNU - enKucukX,
        kaydirY: ofsetY + BASLIK_YUKSEKLIGI - enKucukY,
        siralar: [...siraEtiketleri.entries()].map(([etiket, y]) => ({ etiket, y })),
      };

      enGenis = Math.max(enGenis, bolumGenisligi);
      ofsetY += BASLIK_YUKSEKLIGI + bolumYuksekligi + 24;

      return sonuc;
    });

    return { yerlesim: hesaplanan, genislik: enGenis, yukseklik: Math.max(ofsetY, KOLTUK) };
  }, [bolumler]);

  const toplamKoltuk = bolumler.reduce((toplam, bolum) => toplam + bolum.seats.length, 0);

  // Koltuklar kategoriye ayrilmis mi. Tek bolumluk plan "ayrilmamis" demek.
  const cokBolumlu = bolumler.length > 1;

  // Gradient kimlikleri bilesen basina uretiliyor: ayni sayfada iki plan
  // cizilirse sabit bir kimlik ikisinde de ayni olur ve tarayici ilk
  // tanimlananı kullanir.
  const sahneDolguId = useId();
  const sahneIsikId = useId();

  const sahneSol = SIRA_SUTUNU;
  const sahneGenisligi = Math.max(genislik - SIRA_SUTUNU, KOLTUK);

  if (toplamKoltuk === 0) {
    return (
      <p
        role="status"
        className="rounded-md border border-outline-variant/40 bg-surface-variant/30 px-stack-sm py-stack-sm font-body text-body-sm text-on-surface-variant"
      >
        Bu planda henuz koltuk uretilmemis.
      </p>
    );
  }

  return (
    <div className="overflow-x-auto">
      <svg
        viewBox={`0 0 ${genislik} ${yukseklik}`}
        className="mx-auto block h-auto w-full max-h-[70vh]"
        role="group"
        aria-label={`Oturma plani, ${toplamKoltuk} koltuk`}
      >
        {/*
          SAHNE.
          Yalnizca "SAHNE" yazisi vardi; plan ters okunmaya acikti. Simdi
          cizilen bir sahne onu: koltuklar bunun karsisinda diziliyor ve
          seyirci hangi tarafin on oldugunu okumadan goruyor.

          Uc parca: (1) seyirciye dogru hafif cikinti yapan govde,
          (2) ustteki aydinlik kenar cizgisi, (3) koltuklara dusen isik.
          Isik salt sus degil: bakisi sahneden koltuklara dogru cekiyor,
          yani planin okunma yonunu belirliyor.
        */}
        <g aria-hidden="true">
          <defs>
            <linearGradient id={sahneDolguId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#453d59" />
              <stop offset="100%" stopColor="#221e2c" />
            </linearGradient>

            <linearGradient id={sahneIsikId} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#d0bcff" stopOpacity="0.22" />
              <stop offset="100%" stopColor="#d0bcff" stopOpacity="0" />
            </linearGradient>
          </defs>

          {/* Sahnenin koltuklara vuran isigi */}
          <rect
            x={sahneSol}
            y={SAHNE_YUKSEKLIGI}
            width={sahneGenisligi}
            height={SAHNE_ALT_BOSLUK}
            fill={`url(#${sahneIsikId})`}
          />

          <path
            d={
              `M ${sahneSol} 4 ` +
              `L ${sahneSol + sahneGenisligi} 4 ` +
              `L ${sahneSol + sahneGenisligi} ${SAHNE_YUKSEKLIGI} ` +
              `Q ${sahneSol + sahneGenisligi / 2} ${SAHNE_YUKSEKLIGI + SAHNE_KAVIS} ` +
              `${sahneSol} ${SAHNE_YUKSEKLIGI} Z`
            }
            fill={`url(#${sahneDolguId})`}
            stroke="#6d6579"
            strokeWidth={1}
          />

          {/* Aydinlik ust kenar: sahne isigi altta degil ustte yanar. */}
          <line
            x1={sahneSol + 2}
            y1={4.5}
            x2={sahneSol + sahneGenisligi - 2}
            y2={4.5}
            stroke="#d0bcff"
            strokeOpacity={0.55}
            strokeWidth={1.5}
            strokeLinecap="round"
          />

          <text
            x={sahneSol + sahneGenisligi / 2}
            y={SAHNE_YUKSEKLIGI / 2 + 6}
            textAnchor="middle"
            fontSize={11}
            letterSpacing={5}
            fill="#e8e0e8"
            fontWeight={700}
          >
            SAHNE
          </text>
        </g>

        {yerlesim.map(({ bolum, ustY, kaydirX, kaydirY, siralar }) => (
          <g key={bolum.id}>
            {/*
              Bolum basligi yalnizca BIRDEN FAZLA bolum varsa yaziliyor.
              Koltuklari kategoriye ayrilmamis bir salonda tek bolumun adini
              ("Genel", "Salon") yazmak kullaniciya secmesi gereken bir ayrim
              varmis izlenimi verirdi; oysa ayrim yok.
            */}
            {cokBolumlu && (
              <>
                <rect
                  x={0}
                  y={ustY + 14}
                  width={3}
                  height={16}
                  rx={1.5}
                  fill="#d0bcff"
                  aria-hidden="true"
                />
                <text x={10} y={ustY + 27} fontSize={13} fill="#e8e0e8" fontWeight={600}>
                  {bolum.name}
                </text>
              </>
            )}

            {bolum.seats.length === 0 && (
              <text x={0} y={ustY + 46} fontSize={11} fill="#958ea0">
                Bu bolumde henuz koltuk uretilmemis.
              </text>
            )}

            {siralar.map(({ etiket, y }) => (
              <text
                key={etiket}
                x={SIRA_SUTUNU - 8}
                y={y + kaydirY + KOLTUK / 2 + 4}
                textAnchor="end"
                fontSize={10}
                fill="#958ea0"
                aria-hidden="true"
              >
                {etiket}
              </text>
            ))}

            {bolum.seats.map((koltuk) => {
              const sunucuDurumu = sunucuDurumlari?.get(koltuk.id);

              // Bosta olmayan bir koltuk secilemez. Kontrol yalnizca gorsel:
              // gercek engel sunucuda, koltugun durumu transaction icinde
              // yeniden dogrulaniyor.
              const bosta = sunucuDurumu ? sunucuDurumu === 'Available' : koltuk.isActive;

              const durum: SeatStatus = seciliIdler?.has(koltuk.id)
                ? 'Selected'
                : (sunucuDurumu ?? (koltuk.isActive ? 'Available' : 'Disabled'));

              return (
                <Koltuk
                  key={koltuk.id}
                  koltuk={koltuk}
                  durum={durum}
                  x={koltuk.positionX + kaydirX}
                  y={koltuk.positionY + kaydirY}
                  secilebilir={secilebilir && bosta}
                  onSec={onKoltukSec}
                />
              );
            })}
          </g>
        ))}
      </svg>
    </div>
  );
}
