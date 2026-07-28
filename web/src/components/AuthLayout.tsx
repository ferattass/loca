import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

interface AuthLayoutProps {
  baslik: string;
  aciklama: string;
  children: ReactNode;
  altBaglantiMetni: string;
  altBaglantiEtiketi: string;
  altBaglantiYolu: string;
}

/** Giris ve kayit ekranlarinin ortak cercevesi. */
export function AuthLayout({
  baslik,
  aciklama,
  children,
  altBaglantiMetni,
  altBaglantiEtiketi,
  altBaglantiYolu,
}: AuthLayoutProps) {
  return (
    <main className="min-h-screen flex items-center justify-center px-container-margin-mobile py-stack-lg">
      <div className="w-full max-w-md">
        <div className="text-center mb-stack-md">
          <Link
            to="/"
            className="font-display text-display-lg-mobile text-primary drop-shadow-[0_0_18px_rgba(208,188,255,0.35)]"
          >
            LOCA
          </Link>
          <p className="font-body text-label-caps text-on-surface-variant uppercase mt-base">
            Etkinlik ve Koltuk Rezervasyonu
          </p>
        </div>

        <div className="glass-high rounded-xl p-stack-md">
          <h1 className="font-headline text-headline-md text-on-surface">{baslik}</h1>
          <p className="font-body text-body-sm text-on-surface-variant mt-base mb-stack-md">
            {aciklama}
          </p>

          {children}
        </div>

        <p className="text-center font-body text-body-sm text-on-surface-variant mt-stack-sm">
          {altBaglantiMetni}{' '}
          <Link to={altBaglantiYolu} className="text-primary font-semibold hover:underline">
            {altBaglantiEtiketi}
          </Link>
        </p>
      </div>
    </main>
  );
}
