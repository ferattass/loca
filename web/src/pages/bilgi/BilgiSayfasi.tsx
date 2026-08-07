import { Link } from 'react-router-dom';

interface BilgiSayfasiProps {
  baslik: string;
  ozet: string;
  children: React.ReactNode;
}

/**
 * Bilgi sayfalarinin ortak kabugu.
 *
 * Uc sayfa (yardim, iletisim, kullanim kosullari) ayni iskeleti paylasiyor:
 * dar bir metin sutunu, ustte baslik, altta digerlerine gecis. Ucune ayri
 * ayri yazilsaydi baslik boslugu ve sutun genisligi zamanla birbirinden
 * ayrilirdi.
 *
 * <para>
 * Sutun <c>max-w-3xl</c>: duz metin satiri cok uzun olunca goz satir basina
 * donerken kayboluyor. Sitenin geri kalani <c>max-w-7xl</c> ama orada
 * icerik kart, burada paragraf.
 * </para>
 */
export function BilgiSayfasi({ baslik, ozet, children }: BilgiSayfasiProps) {
  return (
    <div className="mx-auto max-w-3xl px-container-margin-mobile py-stack-lg md:px-container-margin-desktop">
      <header className="mb-stack-lg border-b border-outline-variant/40 pb-stack-md">
        <h1 className="font-display text-display-lg-mobile text-on-surface">{baslik}</h1>
        <p className="mt-stack-sm font-body text-body-md text-on-surface-variant">{ozet}</p>
      </header>

      <div className="space-y-stack-lg">{children}</div>

      <nav
        aria-label="Diğer bilgi sayfaları"
        className="mt-stack-lg flex flex-wrap gap-stack-sm border-t border-outline-variant/40 pt-stack-md"
      >
        <BilgiBaglantisi yol="/hakkimizda">Hakkımızda</BilgiBaglantisi>
        <BilgiBaglantisi yol="/yardim">Yardım merkezi</BilgiBaglantisi>
        <BilgiBaglantisi yol="/iletisim">İletişim</BilgiBaglantisi>
        <BilgiBaglantisi yol="/kullanim-kosullari">Kullanım koşulları</BilgiBaglantisi>
        <BilgiBaglantisi yol="/gizlilik">Gizlilik</BilgiBaglantisi>
      </nav>
    </div>
  );
}

function BilgiBaglantisi({ yol, children }: { yol: string; children: React.ReactNode }) {
  return (
    <Link
      to={yol}
      className="rounded-full border border-outline-variant px-stack-sm py-1 font-body text-body-sm text-on-surface-variant transition-colors hover:border-outline hover:text-on-surface"
    >
      {children}
    </Link>
  );
}

/** Baslikli metin blogu. */
export function BilgiBolumu({
  baslik,
  children,
}: {
  baslik: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-stack-sm">
      <h2 className="font-headline text-title-lg text-on-surface">{baslik}</h2>
      <div className="space-y-stack-sm font-body text-body-md leading-relaxed text-on-surface-variant">
        {children}
      </div>
    </section>
  );
}
