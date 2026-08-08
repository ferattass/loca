/**
 * Etiketli açılır liste.
 *
 * Üç ayrı ekranda birbirinin kopyası olarak duruyordu (etkinlik sihirbazı,
 * mekân yönetimi, oturma planı yönetimi). Kopyalar zaten ayrışmaya
 * başlamıştı: ikisi `devreDisi` destekliyor, biri desteklemiyordu — yani
 * mekân yönetiminde bir listeyi geçici olarak kilitlemek isteyen kod
 * yazamıyordu. Üstün küme burada, tek yerde.
 */
export function Secim({
  etiket,
  deger,
  onDegis,
  secenekler,
  bosMetin = 'Seçiniz',
  gerekli = true,
  devreDisi = false,
}: {
  etiket: string;
  deger: string;
  onDegis: (deger: string) => void;
  secenekler: Array<{ id: string; ad: string }>;
  bosMetin?: string;
  gerekli?: boolean;
  devreDisi?: boolean;
}) {
  return (
    <label className="flex flex-col gap-base">
      <span className="font-body text-body-sm text-on-surface-variant">{etiket}</span>
      <select
        value={deger}
        required={gerekli}
        disabled={devreDisi}
        onChange={(olay) => onDegis(olay.target.value)}
        className="w-full rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-stack-sm font-body text-body-md text-on-surface disabled:opacity-50"
      >
        <option value="">{bosMetin}</option>
        {secenekler.map((secenek) => (
          <option key={secenek.id} value={secenek.id}>
            {secenek.ad}
          </option>
        ))}
      </select>
    </label>
  );
}
