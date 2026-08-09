/**
 * Sayfa gövdesi — kenar boşlukları ve ortalanmış içerik sütunu.
 *
 * Dört ekranda birbirinin kopyasıydı ve aralarındaki tek fark sütun
 * genişliğiydi. Kenar boşluğu değeri kopyalarda ayrı ayrı duruyordu; biri
 * güncellenip diğerleri unutulsa sayfalar birbirinden kayardı.
 *
 * <b>Genişlik neden serbest metin değil:</b> Tailwind sınıf adlarını
 * kaynakta düz metin olarak arıyor. `max-w-${genislik}` yazılsaydı sınıf
 * üretim derlemesinde budanır ve sütun genişliği sessizce kaybolurdu —
 * geliştirmede çalışıp yalnızca canlıda bozulan türden bir hata. Bu yüzden
 * tam sınıf adları tabloda düz yazılı.
 */
const GENISLIKLER = {
  '3xl': 'max-w-3xl',
  '5xl': 'max-w-5xl',
  '6xl': 'max-w-6xl',
} as const;

export function Sayfa({
  genislik = '3xl',
  children,
}: {
  genislik?: keyof typeof GENISLIKLER;
  children: React.ReactNode;
}) {
  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className={`mx-auto ${GENISLIKLER[genislik]}`}>{children}</div>
    </main>
  );
}
