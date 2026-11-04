import type { ReactNode } from 'react';

/**
 * Durum kutusu — hata, uyarı, başarı ve nötr bildirimler.
 *
 * Aynı kutu otuza yakın yerde elle yazılmıştı ve her kopyada aynı sekiz
 * Tailwind sınıfı tekrar ediyordu; renk ailesini değiştirmek otuz dosyayı
 * birden aramak anlamına geliyordu. Kutunun iskeleti artık burada, dışarıdan
 * yalnızca varyantı geliyor.
 *
 * <b>Ton adları neden tam sınıf olarak yazılı:</b> Tailwind sınıf adlarını
 * kaynakta düz metin olarak arıyor. `border-${ton}/40` yazılsaydı sınıf
 * üretim derlemesinde budanır ve kutunun rengi yalnızca canlıda kaybolurdu —
 * geliştirmede çalışıp sadece dağıtımda bozulan türden bir hata. Aynı gerekçe
 * `Sayfa` bileşenindeki genişlik tablosunda da var.
 */
const TONLAR = {
  hata: 'border-error/40 bg-error-container/20 text-error',
  dikkat: 'border-tertiary/40 bg-tertiary-container/10 text-tertiary',
  basari: 'border-primary/40 bg-primary-container/15 text-primary',
  notr: 'border-outline-variant/60 bg-surface-variant/20 text-on-surface-variant',
} as const;

/** Dikey dolgu: dar 8px, geniş 12px. İkisi de kullanımda. */
const DOLGULAR = {
  dar: 'py-base',
  genis: 'py-stack-sm',
} as const;

const BOYUTLAR = {
  sm: 'text-body-sm',
  md: 'text-body-md',
} as const;

interface UyariProps {
  children: ReactNode;
  ton?: keyof typeof TONLAR;
  dolgu?: keyof typeof DOLGULAR;
  boyut?: keyof typeof BOYUTLAR;
  /** Metnin solunda duracak ikon; verilirse kutu flex'e geçer. */
  ikon?: ReactNode;
  /** İkon hizası — tek satırlık kutularda 'orta', sarılabilir metinde 'bas'. */
  hizala?: 'bas' | 'orta';
  /**
   * Ekran okuyucu duyurusu.
   *
   * Bir eylemin sonucunda beliren kutularda `alert` veriliyor; sayfa
   * açıldığında zaten orada duran bilgi kutularında verilmiyor — her kutuyu
   * `alert` yapmak ekran okuyucuyu sayfa açılışında art arda okumaya zorlar.
   */
  role?: 'alert' | 'status';
  /** Yalnızca dış boşluk için (mt-*, mb-*); iç dolgu prop'larla veriliyor. */
  className?: string;
}

export function Uyari({
  children,
  ton = 'hata',
  dolgu = 'genis',
  boyut = 'sm',
  ikon,
  hizala = 'bas',
  role,
  className = '',
}: UyariProps) {
  const hiza = ikon ? `flex ${hizala === 'orta' ? 'items-center' : 'items-start'} gap-base` : '';

  return (
    <div
      role={role}
      className={`${className} ${hiza} rounded-md border px-stack-sm ${DOLGULAR[dolgu]} font-body ${BOYUTLAR[boyut]} ${TONLAR[ton]}`}
    >
      {ikon}
      {children}
    </div>
  );
}
