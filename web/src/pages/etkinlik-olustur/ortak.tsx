/**
 * Etkinlik oluşturma sihirbazının adımları arasında paylaşılan sözleşme ve
 * form parçaları.
 *
 * Adımlar ayrı dosyalarda ama aynı hata bildirme sözleşmesini ve aynı iki
 * form öğesini kullanıyor; ortaklaştırılmasaydı her adım kendi `select`
 * biçimini yazar ve alanlar zamanla birbirinden ayrışırdı.
 */

/** Cocuk bilesenlerin hata bildirme sozlesmesi. `null` temizler. */
export type HataBildir = (hata: unknown, varsayilan?: string) => void;

export function Alan({ children }: { children: React.ReactNode }) {
  return <div className="grid gap-stack-sm md:grid-cols-2">{children}</div>;
}

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
