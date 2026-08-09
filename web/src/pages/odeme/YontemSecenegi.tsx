
/**
 * Odeme yontemi secenegi.
 *
 * Gorunuse ragmen gercek bir <c>radio</c>: klavye ile ok tuslariyla
 * gezilebilmesi ve ekran okuyucunun "iki secenekten biri" demesi buna
 * bagli. Div uzerine onClick konsaydi fare disinda hicbir sey calismazdi.
 */
export function YontemSecenegi({
  secili,
  onSelect,
  baslik,
  aciklama,
}: {
  secili: boolean;
  onSelect: () => void;
  baslik: string;
  aciklama: string;
}) {
  return (
    <label
      className={`flex cursor-pointer items-start gap-stack-sm rounded-md border px-stack-sm py-stack-sm transition-colors ${
        secili
          ? 'border-primary bg-primary-container/15'
          : 'border-outline-variant hover:border-outline'
      }`}
    >
      <input
        type="radio"
        name="odeme-yontemi"
        checked={secili}
        onChange={onSelect}
        className="mt-1 h-4 w-4 accent-primary"
      />
      <span className="font-body text-body-sm text-on-surface">
        {baslik}
        <span className="mt-[2px] block text-body-sm text-on-surface-variant/80">
          {aciklama}
        </span>
      </span>
    </label>
  );
}
