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

export function IndirIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M12 3v12m0 0 4-4m-4 4-4-4M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-2" />
    </Sarmal>
  );
}

export function BelgeIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
      <path d="M14 3v5h5" />
    </Sarmal>
  );
}

export function GorselIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <rect x="3" y="4" width="18" height="16" rx="2" />
      <circle cx="8.5" cy="9.5" r="1.5" />
      <path d="m21 16-5-5L5 20" />
    </Sarmal>
  );
}

/** Kamerayla QR okutma. Kose parantezleri okuyucu cercevesini anlatiyor. */
export function OkutmaIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M4 8V6a2 2 0 0 1 2-2h2M16 4h2a2 2 0 0 1 2 2v2M20 16v2a2 2 0 0 1-2 2h-2M8 20H6a2 2 0 0 1-2-2v-2" />
      <path d="M4 12h16" />
    </Sarmal>
  );
}

export function BiletIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M3 9V7a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v2a2 2 0 0 0 0 6v2a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-2a2 2 0 0 0 0-6z" />
      <path d="M14 6v2m0 3v2m0 3v2" strokeDasharray="1 3" />
    </Sarmal>
  );
}

export function UyariIkonu(props: IkonProps) {
  return (
    <Sarmal {...props}>
      <path d="M12 9v4m0 4h.01" />
      <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0" />
    </Sarmal>
  );
}
