import { Link } from 'react-router-dom';

/**
 * Alt bilgi.
 *
 * Tasarimdaki dort sutunun ucu (Explore / Support / Settings) HENUZ
 * karsiligi olmayan sayfalara isaret ediyordu; olmayan sayfaya baglanti
 * vermek kullaniciyi bos ekrana goturur. Var olan bolumler baglanti,
 * olmayanlar duz metin olarak duruyor ve sayfalari yazildikca baglaniyor.
 */
export function SiteAltbilgisi() {
  return (
    <footer className="mt-stack-lg border-t border-outline-variant/40 bg-surface-container-lowest">
      <div className="mx-auto grid max-w-7xl gap-stack-md px-container-margin-mobile py-stack-lg md:grid-cols-4 md:px-container-margin-desktop">
        <div>
          <div className="mb-stack-sm flex items-center gap-base">
            <span
              aria-hidden="true"
              className="grid h-7 w-7 place-items-center rounded-md bg-primary font-headline text-body-sm font-extrabold text-on-primary"
            >
              L
            </span>
            <span className="font-display text-title-lg tracking-wide text-on-surface">LOCA</span>
          </div>

          <p className="font-body text-body-sm text-on-surface-variant">
            Etkinlik keşfi ve koltuk rezervasyonu. Salonu gör, yerini seç, biletini al.
          </p>
        </div>

        <FooterSutunu baslik="Keşfet">
          <FooterBaglantisi yol="/">Etkinlikler</FooterBaglantisi>
          <FooterBaglantisi yol="/rezervasyonlarim">Rezervasyonlarım</FooterBaglantisi>
          <FooterBaglantisi yol="/biletlerim">Biletlerim</FooterBaglantisi>
        </FooterSutunu>

        <FooterSutunu baslik="Hesap">
          <FooterBaglantisi yol="/giris">Giriş yap</FooterBaglantisi>
          <FooterBaglantisi yol="/kayit">Kayıt ol</FooterBaglantisi>
          <FooterBaglantisi yol="/sifremi-unuttum">Şifremi unuttum</FooterBaglantisi>
        </FooterSutunu>

        <FooterSutunu baslik="Bilgi">
          {/* Bu sayfalar henuz yok; baglanti verilseydi bos ekrana giderdi. */}
          <li className="font-body text-body-sm text-on-surface-variant/50">Yardım merkezi</li>
          <li className="font-body text-body-sm text-on-surface-variant/50">İletişim</li>
          <li className="font-body text-body-sm text-on-surface-variant/50">Kullanım koşulları</li>
        </FooterSutunu>
      </div>

      <div className="border-t border-outline-variant/30 px-container-margin-mobile py-stack-sm text-center md:px-container-margin-desktop">
        <p className="font-body text-body-sm text-on-surface-variant/70">
          Loca — staj projesi. Tüm hakları saklıdır.
        </p>
      </div>
    </footer>
  );
}

function FooterSutunu({ baslik, children }: { baslik: string; children: React.ReactNode }) {
  return (
    <div>
      <h2 className="mb-stack-sm font-headline text-body-md font-semibold text-on-surface">
        {baslik}
      </h2>
      <ul className="flex flex-col gap-base">{children}</ul>
    </div>
  );
}

function FooterBaglantisi({ yol, children }: { yol: string; children: React.ReactNode }) {
  return (
    <li>
      <Link to={yol} className="font-body text-body-sm text-on-surface-variant hover:text-primary">
        {children}
      </Link>
    </li>
  );
}
