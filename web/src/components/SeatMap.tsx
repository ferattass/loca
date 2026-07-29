import { memo, useMemo } from 'react';

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
  /** Verilmezse plan salt okunur cizilir. */
  onKoltukSec?: (koltukId: string) => void;
}

/** Koltuk karesinin kenar uzunlugu (SVG birimi). */
const KOLTUK = 22;

/** Bolum basliginin altinda birakilan bosluk. */
const BASLIK_YUKSEKLIGI = 34;

/** Sira etiketleri icin solda ayrilan sutun genisligi. */
const SIRA_SUTUNU = 26;

const DOLGU: Record<SeatStatus, string> = {
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
      aria-label={`${koltuk.label}${koltuk.isActive ? '' : ' (devre disi)'}`}
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
        {koltuk.isActive ? '' : ' - devre disi'}
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
export function SeatMap({ bolumler, seciliIdler, onKoltukSec }: SeatMapProps) {
  const secilebilir = onKoltukSec !== undefined;

  // Bolum yerlesimi ve tuval olculeri yalnizca plan degistiginde hesaplaniyor;
  // her secimde yeniden hesaplansaydi secim gecikmeli hissedilirdi.
  const { yerlesim, genislik, yukseklik } = useMemo(() => {
    const sirali = [...bolumler].sort(
      (a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name, 'tr'),
    );

    let ofsetY = 0;
    let enGenis = 0;

    const hesaplanan = sirali.map((bolum) => {
      const enKucukX = Math.min(...bolum.seats.map((k) => k.positionX), 0);
      const enKucukY = Math.min(...bolum.seats.map((k) => k.positionY), 0);
      const enBuyukX = Math.max(...bolum.seats.map((k) => k.positionX), 0);
      const enBuyukY = Math.max(...bolum.seats.map((k) => k.positionY), 0);

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
        {/* Sahne yonu: seyircinin plani nasil okuyacagini belirler. */}
        <text x={genislik / 2} y={12} textAnchor="middle" fontSize={11} fill="#958ea0">
          SAHNE
        </text>

        {yerlesim.map(({ bolum, ustY, kaydirX, kaydirY, siralar }) => (
          <g key={bolum.id}>
            <text x={0} y={ustY + 26} fontSize={13} fill="#e8e0e8" fontWeight={600}>
              {bolum.name}
            </text>

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
              const durum: SeatStatus = !koltuk.isActive
                ? 'Disabled'
                : seciliIdler?.has(koltuk.id)
                  ? 'Selected'
                  : 'Available';

              return (
                <Koltuk
                  key={koltuk.id}
                  koltuk={koltuk}
                  durum={durum}
                  x={koltuk.positionX + kaydirX}
                  y={koltuk.positionY + kaydirY}
                  secilebilir={secilebilir && koltuk.isActive}
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
