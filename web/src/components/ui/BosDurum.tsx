import type { ReactNode } from 'react';

/**
 * Boş liste kutusu — "henüz kayıt yok" durumu.
 *
 * Boş liste yerine hiçbir şey çizmemek, kullanıcıya listenin yüklenmediğini
 * mi yoksa gerçekten boş mu olduğunu söylemiyor; kutu bu ayrımı taşıyor.
 * Sekiz ekranda aynı çerçeve ve aynı sönük renk elle yazılmıştı.
 */
const BOYUTLAR = {
  sm: 'px-stack-sm py-stack-md text-body-sm',
  md: 'px-stack-md py-stack-md text-body-md',
} as const;

interface BosDurumProps {
  children: ReactNode;
  boyut?: keyof typeof BOYUTLAR;
  /** Metnin solunda duracak ikon; verilirse kutu flex'e geçer. */
  ikon?: ReactNode;
  /** Yalnızca dış boşluk için (mt-*, mb-*). */
  className?: string;
}

export function BosDurum({ children, boyut = 'md', ikon, className = '' }: BosDurumProps) {
  return (
    <p
      className={`${className} ${ikon ? 'flex items-center gap-base' : ''} rounded-lg border border-outline-variant/40 bg-surface-variant/20 ${BOYUTLAR[boyut]} font-body text-on-surface-variant`}
    >
      {ikon}
      {children}
    </p>
  );
}
