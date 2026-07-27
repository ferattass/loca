/**
 * Bes koltuk durumunun gorsel karsiligi.
 *
 * Sartname dort durum sayiyor (bos, secili, gecici kilitli, satilmis);
 * Sprint 4'teki "koltuk devre dislari birakma" gereksinimi yuzunden
 * besinci durum (Disabled) eklendi.
 *
 * Renk tek ayirt edici degil: Locked ve Sold durumlarinda ayrica
 * simge/desen farki var, renk koru kullanicilar icin gerekli.
 */

export type SeatStatus =
  | 'Available'
  | 'Selected'
  | 'Locked'
  | 'Sold'
  | 'Disabled';

const STATES: Array<{ status: SeatStatus; label: string; aciklama: string }> = [
  { status: 'Available', label: 'Bos', aciklama: 'Secilebilir' },
  { status: 'Selected', label: 'Secili', aciklama: 'Senin sectigin' },
  { status: 'Locked', label: 'Gecici kilitli', aciklama: 'Baskasi isliyor' },
  { status: 'Sold', label: 'Satilmis', aciklama: 'Secilemez' },
  { status: 'Disabled', label: 'Devre disi', aciklama: 'Bozuk koltuk' },
];

/** Duruma gore koltuk hucresinin stilini dondurur. */
export function seatClasses(status: SeatStatus): string {
  switch (status) {
    case 'Available':
      return 'bg-seat-available border-outline-variant text-on-surface-variant hover:border-primary cursor-pointer';
    case 'Selected':
      return 'bg-seat-selected border-primary text-on-primary shadow-glow-primary cursor-pointer';
    case 'Locked':
      return 'bg-seat-locked border-tertiary text-on-tertiary cursor-not-allowed';
    case 'Sold':
      return 'bg-seat-sold border-outline-variant/50 text-on-surface-variant/40 cursor-not-allowed';
    case 'Disabled':
      return 'bg-seat-disabled border-outline-variant/30 text-on-surface-variant/25 cursor-not-allowed';
  }
}

export function SeatStatePreview() {
  return (
    <div className="glass rounded-lg p-stack-md">
      <div className="flex flex-wrap gap-stack-md">
        {STATES.map(({ status, label, aciklama }) => (
          <div key={status} className="flex flex-col items-center gap-base">
            <div
              className={`w-12 h-12 rounded flex items-center justify-center border-2 font-body text-body-sm font-semibold transition-all ${seatClasses(status)}`}
              aria-label={`${label} koltuk ornegi`}
            >
              A1
            </div>
            <div className="text-center">
              <p className="font-body text-body-sm text-on-surface">{label}</p>
              <p className="font-body text-[11px] text-on-surface-variant">
                {aciklama}
              </p>
            </div>
          </div>
        ))}
      </div>

      <p className="font-body text-body-sm text-on-surface-variant mt-stack-md border-t border-outline-variant/30 pt-stack-sm">
        Gercek koltuk plani Gun 4'te SVG olarak cizilecek. 600 koltukta
        takilmamasi icin her hucre <code className="text-secondary">React.memo</code>{' '}
        ile sarilacak, klavye erisimi icin{' '}
        <code className="text-secondary">role=&quot;checkbox&quot;</code> eklenecek.
      </p>
    </div>
  );
}
