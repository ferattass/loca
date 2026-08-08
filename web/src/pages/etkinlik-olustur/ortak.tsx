/**
 * Etkinlik oluşturma sihirbazının adımları arasında paylaşılan sözleşme ve
 * yerleşim parçası.
 *
 * Adımlar ayrı dosyalarda ama hepsi hatayı aynı şekilde yukarı bildiriyor:
 * ham hatayı geçiriyorlar, metne çevirme kabukta yapılıyor. Her adım kendi
 * mesajını üretseydi alan bazlı doğrulama hatalarına erişilemezdi.
 *
 * Açılır liste burada değil `components/ui/Secim` içinde — sihirbaz dışında
 * iki yönetim ekranı daha aynı bileşeni kullanıyor.
 */

/** Cocuk bilesenlerin hata bildirme sozlesmesi. `null` temizler. */
export type HataBildir = (hata: unknown, varsayilan?: string) => void;

export function Alan({ children }: { children: React.ReactNode }) {
  return <div className="grid gap-stack-sm md:grid-cols-2">{children}</div>;
}
