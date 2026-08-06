import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

import { Logo } from './ui/Logo';

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
        {/* Giris ekraninda dikey logo: alt yazisi ("ETKINLIK · BILET ·
            KOLTUK") zaten logonun parcasi, ayrica bir aciklama satiri
            yazmak ayni seyi iki kez soylemek olurdu. */}
        <div className="mb-stack-md flex justify-center">
          <Link to="/" aria-label="Loca ana sayfa">
            <Logo bicim="dikey" className="h-40 w-auto" />
          </Link>
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
