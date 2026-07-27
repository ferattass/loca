import { SeatStatePreview } from './components/SeatStatePreview';

/**
 * Gun 2 iskelet dogrulama sayfasi.
 *
 * Amaci tasarim sisteminin koda dogru baglandigini gostermek:
 * renk token'lari, tipografi olcegi ve bes koltuk durumu.
 * Gercek sayfalar Gun 3'ten itibaren src/pages altinda yazilacak,
 * bu dosya o zaman router'a devredecek.
 */
export default function App() {
  return (
    <div className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <header className="mb-stack-lg">
        <p className="font-body text-label-caps text-primary uppercase">
          Etkinlik ve Koltuk Rezervasyon Sistemi
        </p>
        <h1 className="font-display text-display-lg-mobile md:text-display-lg text-on-surface mt-base">
          LOCA
        </h1>
        <p className="font-body text-body-md text-on-surface-variant mt-stack-sm max-w-xl">
          Iskelet kuruldu. Tasarim sistemi token'lari Tailwind yapilandirmasina
          baglandi; asagidaki renkler ve koltuk durumlari config'ten geliyor.
        </p>
      </header>

      <section className="mb-stack-lg">
        <h2 className="font-headline text-headline-md text-on-surface mb-stack-sm">
          Koltuk durumlari
        </h2>
        <SeatStatePreview />
      </section>

      <section>
        <h2 className="font-headline text-headline-md text-on-surface mb-stack-sm">
          Yuzey katmanlari
        </h2>
        <div className="flex flex-wrap gap-base">
          {[
            ['surface-container-lowest', 'bg-surface-container-lowest'],
            ['surface-container-low', 'bg-surface-container-low'],
            ['surface-container', 'bg-surface-container'],
            ['surface-container-high', 'bg-surface-container-high'],
            ['surface-container-highest', 'bg-surface-container-highest'],
          ].map(([label, cls]) => (
            <div
              key={label}
              className={`${cls} rounded-lg px-stack-sm py-base border border-outline-variant/30`}
            >
              <span className="font-body text-body-sm text-on-surface-variant">
                {label}
              </span>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
