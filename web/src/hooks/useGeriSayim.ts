import { useEffect, useRef, useState } from 'react';

/**
 * Sunucudan gelen saniyeden baslayarak geri sayar.
 *
 * <b>Baslangic degeri sunucudan gelir, istemci saatinden HESAPLANMAZ.</b>
 * `expiresAtUtc` ile `Date.now()` karsilastirilsaydi saati geri alinmis bir
 * makinede sayac hic bitmez, ileri alinmis bir makinede daha ilk saniyede
 * biterdi. Istemci yalnizca gecen sureyi sayiyor.
 *
 * Gecen sure `performance.now()` uzerinden olculuyor: kullanici sekmeyi
 * arka plana attiginda tarayici zamanlayiciyi seyrelttigi icin "her tik bir
 * saniye" varsayimi kayar. Bu sayac gercek gecen sureyi okuyor, tik saymiyor.
 */
export function useGeriSayim(
  baslangicSaniye: number | undefined,
  bittiginde?: () => void,
): number {
  const [kalan, setKalan] = useState(baslangicSaniye ?? 0);

  // Geri cagirma referans olarak tutuluyor; her cizimde yeni bir fonksiyon
  // gelse bile zamanlayici yeniden kurulmasin.
  const bittigindeRef = useRef(bittiginde);
  bittigindeRef.current = bittiginde;

  useEffect(() => {
    if (baslangicSaniye === undefined) return;

    setKalan(baslangicSaniye);

    if (baslangicSaniye <= 0) {
      bittigindeRef.current?.();
      return;
    }

    const basladi = performance.now();
    let bildirildi = false;

    const zamanlayici = window.setInterval(() => {
      const gecen = (performance.now() - basladi) / 1000;
      const yeni = Math.max(0, Math.ceil(baslangicSaniye - gecen));

      setKalan(yeni);

      if (yeni === 0 && !bildirildi) {
        bildirildi = true;
        window.clearInterval(zamanlayici);
        bittigindeRef.current?.();
      }
    }, 250);

    return () => window.clearInterval(zamanlayici);
  }, [baslangicSaniye]);

  return kalan;
}

/** 125 saniyeyi "2:05" olarak yazar. */
export function sureBicimle(saniye: number): string {
  const dakika = Math.floor(saniye / 60);
  const kalan = saniye % 60;

  return `${dakika}:${kalan.toString().padStart(2, '0')}`;
}
