/**
 * Loca logosu.
 *
 * Kaynak gorseller BEYAZ ZEMINLI ve yazisi LACIVERT geldi. Site koyu
 * temada oldugu icin iki varyant uretildi: yazisi acik renk olan
 * (koyu zemin) ve orijinal lacivert olan (acik zemin — bilet karti,
 * yazdirilan belge). Ayni dosyayi her yerde kullanmak, ikisinden birinde
 * logonun okunmamasi demekti.
 *
 * <para>
 * Zemin seffaf: PNG'ler alfa kanaliyla uretildi, kenar yumusakligi
 * korundu. Beyaz zeminli hâli baslikta beyaz bir kutu gibi gorunurdu.
 * </para>
 *
 * <para>
 * <b>Neden PNG, SVG degil:</b> isaretteki degrade ve yumusak gecisler
 * SVG'ye elle cevrilseydi orijinalinden farkli bir sey cizilmis olurdu.
 * Boyutlar zaten sinirli (en buyugu 720 piksel genislikte) ve
 * <c>width</c>/<c>height</c> verildigi icin yerlesim kaymasi olmuyor.
 * </para>
 */

interface LogoProps {
  /** `isaret` yalnizca simge, `yatay` simge + yazi, `dikey` alt alta. */
  bicim?: 'isaret' | 'yatay' | 'dikey';
  /** Acik zeminde (bilet karti, belge) lacivert yazili varyant. */
  acikZemin?: boolean;
  className?: string;
}

const DOSYALAR = {
  isaret: { acik: '/logo-isaret.png', koyu: '/logo-isaret.png', oran: 1 },
  yatay: { acik: '/logo-yatay.png', koyu: '/logo-yatay-koyu.png', oran: 720 / 186 },
  dikey: { acik: '/logo-dikey.png', koyu: '/logo-dikey-koyu.png', oran: 560 / 535 },
} as const;

export function Logo({ bicim = 'yatay', acikZemin = false, className = 'h-8' }: LogoProps) {
  const secim = DOSYALAR[bicim];

  return (
    <img
      src={acikZemin ? secim.koyu : secim.acik}
      // Logo bir bilgi tasimiyor; yaninda zaten site adi gecmiyorsa
      // "Loca" diyor, geciyorsa tekrar olurdu. Cagiran taraf karar
      // veremedigi icin burada tek bir dogru metin var.
      alt="Loca"
      className={className}
      // Oran veriliyor: gorsel yuklenene kadar yer kaplasin ve sayfa
      // yuklendiginde asagi kaymasin.
      style={{ aspectRatio: secim.oran }}
    />
  );
}
