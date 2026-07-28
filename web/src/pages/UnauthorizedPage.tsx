import { Link } from 'react-router-dom';

export function UnauthorizedPage() {
  return (
    <main className="min-h-screen flex items-center justify-center px-container-margin-mobile">
      <div className="text-center max-w-md">
        <p className="font-display text-display-lg text-tertiary">403</p>

        <h1 className="font-headline text-headline-md text-on-surface mt-stack-sm">
          Bu sayfaya erisim yetkin yok
        </h1>

        <p className="font-body text-body-md text-on-surface-variant mt-base">
          Hesabin bu islem icin gereken role sahip degil. Yanlislik oldugunu
          dusunuyorsan yonetici ile iletisime gec.
        </p>

        <Link
          to="/"
          className="inline-block mt-stack-md font-body text-body-md text-primary hover:underline"
        >
          Ana sayfaya don
        </Link>
      </div>
    </main>
  );
}
