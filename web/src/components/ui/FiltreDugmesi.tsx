/**
 * Açık/kapalı süzgeç düğmesi.
 *
 * Kullanıcı listesi ve ödeme listesi ekranlarında birebir aynı gövdeyle iki
 * kez yazılmıştı.
 *
 * <b>`aria-pressed` bu bileşenin işi:</b> seçili durum yalnızca renkle
 * anlatılsaydı ekran okuyucu kullanan biri hangi süzgecin açık olduğunu
 * hiç öğrenemezdi.
 */
export function FiltreDugmesi({
  secili,
  onClick,
  children,
}: {
  secili: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={secili}
      className={`rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
        secili
          ? 'bg-primary-container/25 font-semibold text-primary'
          : 'border border-outline-variant text-on-surface-variant hover:text-on-surface'
      }`}
    >
      {children}
    </button>
  );
}
