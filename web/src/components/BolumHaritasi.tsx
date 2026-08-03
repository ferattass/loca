export interface BolumOzeti {
  id: string;
  ad: string;
  siraNo: number;
  toplamKoltuk: number;
  musaitKoltuk: number;
  enDusukFiyat: number | null;
  paraBirimi: string;
}

interface BolumHaritasiProps {
  bolumler: BolumOzeti[];
  seciliBolumId: string | null;
  onBolumSec: (bolumId: string) => void;
  /** Kullanicinin bu oturumda halihazirda sectigi koltuklarin bolum dagilimi. */
  seciliKoltukSayilari?: ReadonlyMap<string, number>;
}

const paraBicimi = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
});

/**
 * Salonun bolum bolum genel gorunumu.
 *
 * <b>Neden iki seviyeli akis.</b> Iki yuz koltugu tek ekranda kare kare
 * gostermek, kullaniciyi once "nereye oturmak istiyorum" sorusunu
 * atlayip dogrudan tek tek koltuklara bakmaya zorluyor. Bilet satan
 * sitelerin tamami once BLOK secitiyor: kullanici salonun neresi oldugunu
 * ve fiyat araligini gorup karar veriyor, koltuk secimi ondan sonra
 * geliyor. Ayrica alti yuz koltuklu bir salonda tek seferde alti yuz SVG
 * ogesi cizmek gereksiz.
 *
 * Blok olculeri koltuk sayisiyla ORANTILI: yuz koltukluk bir bolum, on
 * koltukluk bolumden gorunur sekilde buyuk. Hepsi ayni boyda olsaydi plan
 * salonun gercek dagilimi hakkinda yanlis fikir verirdi.
 */
export function BolumHaritasi({
  bolumler,
  seciliBolumId,
  onBolumSec,
  seciliKoltukSayilari,
}: BolumHaritasiProps) {
  const sirali = [...bolumler].sort(
    (a, b) => a.siraNo - b.siraNo || a.ad.localeCompare(b.ad, 'tr'),
  );

  const enBuyuk = Math.max(...sirali.map((b) => b.toplamKoltuk), 1);

  return (
    // Harita sabit bir genislikte tutuluyor: genis ekranda serbest birakilsa
    // sahne ile bolumler birbirinden kopar ve plan salon gibi okunmaz.
    <div className="mx-auto w-full max-w-xl space-y-stack-sm">
      <Sahne />

      <ul className="flex flex-col items-center gap-stack-sm">
        {sirali.map((bolum) => {
          const doluluk =
            bolum.toplamKoltuk === 0
              ? 1
              : 1 - bolum.musaitKoltuk / bolum.toplamKoltuk;

          const tamamenDolu = bolum.musaitKoltuk === 0;
          const secili = bolum.id === seciliBolumId;
          const seciliKoltuk = seciliKoltukSayilari?.get(bolum.id) ?? 0;

          // Genislik koltuk sayisiyla orantili ama en az %35: cok kucuk bir
          // bolum tiklanamayacak kadar incelmesin.
          const genislikYuzde = Math.max(35, Math.round((bolum.toplamKoltuk / enBuyuk) * 100));

          return (
            <li key={bolum.id} className="w-full" style={{ maxWidth: `${genislikYuzde}%` }}>
              <button
                type="button"
                onClick={() => onBolumSec(bolum.id)}
                aria-pressed={secili}
                // Tamamen dolu bolum devre disi BIRAKILMIYOR: kullanici yine
                // de icini gorup satilmis koltuklari gorebilmeli. Yalnizca
                // gorsel olarak soluklastiriliyor.
                className={`
                  w-full rounded-lg border px-stack-sm py-stack-sm text-left transition-colors
                  ${
                    secili
                      ? 'border-primary bg-primary-container/30'
                      : tamamenDolu
                        ? 'border-outline-variant/40 bg-surface-variant/20'
                        : 'border-outline-variant bg-surface-variant/40 hover:border-primary/70'
                  }
                `}
              >
                <div className="flex flex-wrap items-baseline justify-between gap-base">
                  <span
                    className={`font-body text-body-md font-semibold ${
                      tamamenDolu ? 'text-on-surface-variant' : 'text-on-surface'
                    }`}
                  >
                    {bolum.ad}
                    {seciliKoltuk > 0 && (
                      <span className="ml-base font-normal text-primary">
                        · {seciliKoltuk} koltuk seçtin
                      </span>
                    )}
                  </span>

                  <span className="font-body text-body-sm text-on-surface-variant">
                    {bolum.enDusukFiyat !== null
                      ? `${paraBicimi.format(bolum.enDusukFiyat)}'den`
                      : ''}
                  </span>
                </div>

                {/*
                  Doluluk cubugu: sayiyi okumadan once "burasi ne kadar dolu"
                  sorusuna cevap veriyor. Renk tek ayirt edici degil, altinda
                  ayrica sayi yaziyor.
                */}
                <div
                  className="mt-base h-1.5 w-full overflow-hidden rounded-full bg-surface-container-low"
                  aria-hidden="true"
                >
                  <div
                    className={`h-full ${tamamenDolu ? 'bg-error/60' : 'bg-primary/70'}`}
                    style={{ width: `${Math.round(doluluk * 100)}%` }}
                  />
                </div>

                <p className="mt-base font-body text-body-sm text-on-surface-variant">
                  {tamamenDolu
                    ? 'Tükendi'
                    : `${bolum.musaitKoltuk} / ${bolum.toplamKoltuk} koltuk boş`}
                </p>
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

/**
 * Sahne blogu.
 *
 * Kalin ve dolu bir blok: plandaki en belirgin oge sahne olmali, cunku
 * kullanicinin ilk sordugu sey "sahne nerede". Ince bir cerceve veya
 * yalnizca yazi, koltuk bloklariyla ayni agirlikta gorunup bu soruyu
 * cevapsiz birakiyordu.
 */
export function Sahne() {
  return (
    <div className="w-full">
      {/*
        Sahne, koltuk bloklariyla ayni genislikte ve en yuksek kontrastli
        oge. Koyu zeminde en dikkat cekici sey acik bir blok oldugu icin
        sahne acik, bloklar koyu; boylece "on taraf neresi" sorusu plana
        bakildigi anda cevaplaniyor.
      */}
      <div className="rounded-lg bg-on-surface px-stack-md py-stack-sm text-center shadow-lg">
        <span className="font-headline text-body-md font-bold tracking-[0.4em] text-surface">
          SAHNE
        </span>
      </div>

      {/* Sahnenin koltuklara dusen isigi; bakisi asagi, koltuklara cekiyor. */}
      <div
        aria-hidden="true"
        className="mx-auto h-7 w-11/12 bg-gradient-to-b from-primary/20 to-transparent"
      />
    </div>
  );
}
