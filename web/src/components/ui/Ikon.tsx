interface IkonProps {
  className?: string;
  /** Metnin yaninda anlam tasimayan sus ikonlarda true birakilir. */
  gizli?: boolean;
  /** Tek basina anlam tasiyorsa ekran okuyucuya okunacak metin. */
  etiket?: string;
}

/**
 * Arayuzde kullanilan ikonlar.
 *
 * <b>Emoji yerine SVG.</b> Emoji her isletim sisteminde farkli ciziliyor
 * (Windows'ta duz, macOS'ta renkli, Android'de bambaska), boyutu yaziyla
 * birlikte kaymiyor ve rengi CSS'ten degistirilemiyor — koyu temada
 * beklenmedik renk lekeleri birakiyor. SVG ikonlar <c>currentColor</c>
 * kullandigi icin metnin rengini aliyor ve her yerde ayni gorunuyor.
 *
 * <para>
 * Dis ikon kutuphanesi eklenmedi: kullanilan ikon sayisi az, tek bir paket
 * icin ek bagimlilik ve paket boyutu tasimanin karsiligi yok.
 * </para>
 */
function Sarmal({
  gizli = true,
  etiket,
  className = 'h-4 w-4',
  children,
}: IkonProps & { children: React.ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      // Etiket verilmisse ikon tek basina anlam tasiyor demektir; ekran
      // okuyucudan gizlenmemeli.
      aria-hidden={etiket ? undefined : gizli}
      role={etiket ? 'img' : undefined}
      aria-label={etiket}
    >
      {children}
    </svg>
  );
}

export function OnayIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M20 6 9 17l-5-5" />
    </Sarmal>
  );
}

export function CarpiIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M18 6 6 18M6 6l12 12" />
    </Sarmal>
  );
}

export function SolOkIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M19 12H5m0 0 7 7m-7-7 7-7" />
    </Sarmal>
  );
}

export function SagOkIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M5 12h14m0 0-7-7m7 7-7 7" />
    </Sarmal>
  );
}
