import { Link } from 'react-router-dom';

import { Logo } from './ui/Logo';

/**
 * Alt bilgi.
 *
 * Tasarimdaki dort sutunun ucu (Explore / Support / Settings) karsiligi
 * olmayan sayfalara isaret ediyordu; olmayan sayfaya baglanti vermek
 * kullaniciyi bos ekrana goturecegi icin o baglantilar bir sure duz metin
 * olarak durdu. Bes bilgi sayfasi (hakkimizda, yardim, iletisim, kullanim
 * kosullari, gizlilik) ve Mekanlar yazildi — alt bilgide artik tiklanmayan
 * oge yok.
 *
 * <para>
 * "Kurumsal" ve "Bilgi" AYRI sutunlar: ilki sirketle ilgili ("kimsiniz,
 * nerelerdesiniz"), ikincisi kurallarla. Tek sutunda toplansaydi bes madde
 * alt alta sıralanır ve hangisinin ne oldugu ancak okunarak anlasilirdi.
 * </para>
 */
export function SiteAltbilgisi() {
  return (
    <footer className="mt-stack-lg border-t border-outline-variant/40 bg-surface-container-lowest">
      <div className="mx-auto grid max-w-7xl gap-stack-md px-container-margin-mobile py-stack-lg sm:grid-cols-2 md:grid-cols-5 md:px-container-margin-desktop">
        <div>
          <div className="mb-stack-sm">
            <Logo bicim="yatay" className="h-8 w-auto" />
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

        <FooterSutunu baslik="Kurumsal">
          <FooterBaglantisi yol="/hakkimizda">Hakkımızda</FooterBaglantisi>
          <FooterBaglantisi yol="/iletisim">İletişim</FooterBaglantisi>
          <FooterBaglantisi yol="/mekanlar">Mekânlar</FooterBaglantisi>
        </FooterSutunu>

        <FooterSutunu baslik="Bilgi">
          <FooterBaglantisi yol="/yardim">Yardım merkezi</FooterBaglantisi>
          <FooterBaglantisi yol="/kullanim-kosullari">Kullanım koşulları</FooterBaglantisi>
          <FooterBaglantisi yol="/gizlilik">Gizlilik</FooterBaglantisi>
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
