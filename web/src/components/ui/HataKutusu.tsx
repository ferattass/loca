import type { HataYapisi } from '../../lib/hataYapisi';
import { Uyari } from './Uyari';

/**
 * Özet + alan bazlı hata listesi taşıyan hata kutusu.
 *
 * Kutunun görünümü `Uyari`'dan geliyor; buradaki tek karar, listenin ne
 * zaman gösterileceği.
 */
export function HataKutusu({ hata }: { hata: HataYapisi | null }) {
  if (!hata) return null;

  return (
    <Uyari role="alert" dolgu="dar">
      <p>{hata.ozet}</p>
      {/* Tek mesaj zaten özette görünüyor; liste yalnızca birden fazla alan
          hatalı olduğunda ek bilgi taşır. */}
      {hata.liste.length > 1 && (
        <ul className="mt-base list-disc space-y-[2px] pl-stack-md">
          {hata.liste.map((mesaj) => (
            <li key={mesaj}>{mesaj}</li>
          ))}
        </ul>
      )}
    </Uyari>
  );
}
