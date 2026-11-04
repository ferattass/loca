/**
 * Yükleme iskeleti.
 *
 * On yediye yakın ekranda aynı üç karar tekrar ediyordu: `animate-pulse`,
 * `aria-hidden` ve gri çubuk rengi.
 *
 * <b>İskelet ekran okuyucudan gizli.</b> Okunacak bir şey yok — boş kutular
 * "grup, grup, grup" diye okunur ve kullanıcı sayfanın yüklendiğini değil
 * bozulduğunu sanar. Bekleyişin duyurulması gerekiyorsa metin ayrı bir
 * `role="status"` alanında gidiyor; kopyaların bir kısmında bu duyuru vardı,
 * bir kısmında yoktu.
 */
interface IskeletProps {
  /**
   * Çubukların tam sınıf zinciri — yükseklik, köşe ve renk birlikte.
   *
   * Renk de dışarıdan: kopyalarda iki ton kullanılıyor (`/40` gövde,
   * `/60` başlık satırı) ve varsayılan bir ton dayatmak, üstüne yazan
   * çağrılarda Tailwind sınıf sırası belirsizliğine düşerdi.
   */
  cubuk: string | readonly string[];
  /** `cubuk` tek metinse kaç kopya çizileceği. */
  adet?: number;
  /** Çubuk düzeni: "space-y-base" veya "grid gap-stack-sm sm:grid-cols-2". */
  duzen?: string;
  /** Ekran okuyucuya okunacak bekleme metni. */
  duyuru?: string;
  className?: string;
}

export function Iskelet({ cubuk, adet = 1, duzen = '', duyuru, className = '' }: IskeletProps) {
  const cubuklar = typeof cubuk === 'string' ? Array.from({ length: adet }, () => cubuk) : cubuk;

  return (
    <>
      <div className={`${className} animate-pulse ${duzen}`} aria-hidden="true">
        {cubuklar.map((sinif, sira) => (
          <div key={sira} className={sinif} />
        ))}
      </div>

      {duyuru && (
        <span className="sr-only" role="status">
          {duyuru}
        </span>
      )}
    </>
  );
}
