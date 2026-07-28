import type { ButtonHTMLAttributes, ReactNode } from 'react';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode;
  gorunum?: 'dolu' | 'cizgili' | 'sade';
  yukleniyor?: boolean;
}

const gorunumler = {
  dolu: 'bg-primary text-on-primary hover:shadow-glow-primary',
  cizgili: 'border border-outline text-on-surface hover:bg-surface-container-high',
  sade: 'text-primary hover:bg-surface-container',
} as const;

export function Button({
  children,
  gorunum = 'dolu',
  yukleniyor = false,
  disabled,
  className = '',
  ...props
}: ButtonProps) {
  return (
    <button
      {...props}
      // Yuklenirken de kapatilir; cift gonderim rezervasyon akisinda
      // iki ayri kayit olusturabilir.
      disabled={disabled || yukleniyor}
      aria-busy={yukleniyor || undefined}
      className={`
        inline-flex items-center justify-center gap-base
        rounded-full px-stack-md py-stack-sm
        font-body text-body-md font-semibold
        transition-all duration-200
        disabled:opacity-50 disabled:cursor-not-allowed disabled:shadow-none
        ${gorunumler[gorunum]}
        ${className}
      `}
    >
      {yukleniyor && (
        <span
          aria-hidden="true"
          className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
        />
      )}
      {children}
    </button>
  );
}
