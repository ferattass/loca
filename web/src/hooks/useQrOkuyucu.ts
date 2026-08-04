import { useCallback, useEffect, useRef, useState } from 'react';
import QrScanner from 'qr-scanner';

/** Ayni kod bu sure icinde tekrar okunursa yok sayilir. */
const TEKRAR_ESIGI_MS = 2500;

interface Secenekler {
  /** Kamera acik mi. Kapatildiginda akis gercekten durdurulur. */
  acik: boolean;
  onKod: (kod: string) => void;
}

/**
 * Kameradan QR okur.
 *
 * <b>Ayni kodun tekrari bastiriliyor.</b> Okuyucu kareyi saniyede birkac
 * kez cozuyor; bastirilmasaydi bilet kameranin onunde durdugu surece
 * saniyede birkac okutma istegi giderdi. Ilki bileti kullanilmis
 * isaretler, hemen ardindan gelenler "bu bilet daha once kullanilmis"
 * doner ve gorevli yesil ekrani goremeden kirmiziya donerdi.
 *
 * <para>
 * Kamera izni verilmemis veya cihazda kamera yoksa hata mesaji donuyor;
 * ekran bu durumda elle kod girme alanina dusuyor. Kapida internet ve
 * izinlerle ugrasmak zorunda kalan gorevlinin elinde her zaman bir yol
 * olmali.
 * </para>
 */
export function useQrOkuyucu({ acik, onKod }: Secenekler) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [hata, setHata] = useState<string | null>(null);

  // Callback ref: okuyucu bir kez kuruluyor, onKod her renderda
  // degisebiliyor. Bagimlilik dizisine konsaydi her renderda kamera
  // kapanip yeniden acilirdi.
  const onKodRef = useRef(onKod);
  onKodRef.current = onKod;

  const sonKodRef = useRef<{ kod: string; an: number } | null>(null);

  const kodGeldi = useCallback((kod: string) => {
    const simdi = Date.now();
    const son = sonKodRef.current;

    if (son && son.kod === kod && simdi - son.an < TEKRAR_ESIGI_MS) return;

    sonKodRef.current = { kod, an: simdi };
    onKodRef.current(kod);
  }, []);

  useEffect(() => {
    const video = videoRef.current;
    if (!acik || !video) return;

    const okuyucu = new QrScanner(video, (sonuc) => kodGeldi(sonuc.data), {
      // Arka kamera: telefonda varsayilan on kamerayla bilete dogru
      // tutmak mumkun degil.
      preferredCamera: 'environment',
      highlightScanRegion: true,
      highlightCodeOutline: true,
      maxScansPerSecond: 5,
    });

    let iptal = false;

    okuyucu
      .start()
      .then(() => {
        if (!iptal) setHata(null);
      })
      .catch(() => {
        if (!iptal) setHata('Kameraya erişilemedi. Kodu elle girebilirsin.');
      });

    return () => {
      iptal = true;
      okuyucu.stop();
      // stop() akisi durduruyor ama kamerayi birakmiyor; destroy()
      // cagrilmazsa cihazin kamera isigi sayfadan cikildiktan sonra da
      // yanik kaliyor.
      okuyucu.destroy();
    };
  }, [acik, kodGeldi]);

  return { videoRef, hata };
}
