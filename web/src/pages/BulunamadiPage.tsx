import { Link, useLocation } from 'react-router-dom';


/**
 * Tanimsiz adres.
 *
 * <b>Onceden tanimsiz her adres sessizce ana sayfaya yonlendiriliyordu.</b>
 * Gorunuste zararsiz ama bir hatayi tamamen ortuyor: etkinlik karti
 * <c>/etkinlikler/:id</c> adresine bagliydi, o rota hic tanimli degildi ve
 * "Bilet al"a basan kullanici ana sayfaya donuyordu. Kirik bir baglanti,
 * calisan bir baglanti gibi davraniyordu — ne kullanici bir sey anliyordu
 * ne de gelistirici.
 *
 * <para>
 * Sayfanin dili urunun kendi dili: burada satilan sey koltuk, bu yuzden
 * bulunamayan adres "salonda olmayan koltuk" olarak anlatiliyor. Genel bir
 * "404 Not Found" ekrani her siteye ait olabilirdi, bu ekran yalnizca bu
 * urune ait.
 * </para>
 */
export function BulunamadiPage() {
  const konum = useLocation();

  return (
    <div
      // Baglanti tarayicisinin (tools/link-checker) tutundugu isaret.
      // Tek sayfali uygulamada sunucu tanimsiz adrese de index.html
      // donuyor, yani HTTP durumu her zaman 200; kirik bir rota ancak
      // bu ekranin cikmasindan anlasiliyor.
      data-sayfa="bulunamadi"
      className="relative overflow-hidden"
    >
      {/* Ana sayfadaki kahraman bolumuyle ayni isik: sayfa bir hata
          ekrani olsa da sitenin disina dusmus gibi durmamali. */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-x-0 top-0 h-64 bg-gradient-to-b from-primary-container/15 to-transparent"
      />

      <div className="relative mx-auto flex max-w-3xl flex-col items-center gap-stack-md px-container-margin-mobile py-stack-lg text-center md:py-24">
        <span className="w-fit rounded-full border border-primary/40 bg-primary-container/20 px-stack-sm py-1 font-body text-label-caps text-primary">
          SAYFA BULUNAMADI
        </span>

        <h1 className="font-display text-display-lg-mobile text-on-surface md:text-display-lg">
          Bu koltuk
          <br />
          <span className="text-primary">salonda yok</span>
        </h1>

        <p className="max-w-md font-body text-body-md text-on-surface-variant">
          Aradığın sayfa kaldırılmış ya da adres yanlış yazılmış olabilir. Sahne hâlâ ayakta —
          buradan devam edebilirsin.
        </p>

        <BosKoltukSirasi />

        {/* Button bileseni <button> uretiyor; Link'in icine konsaydi
            baglantinin icinde dugme olurdu — gecersiz HTML ve ekran
            okuyucuda iki ayri etkilesimli oge. Gidilecek bir adres oldugu
            icin dogru oge baglantinin kendisi, dugme gorunumu siniftan
            geliyor. */}
        <div className="flex flex-wrap justify-center gap-stack-sm">
          <Link
            to="/"
            className="inline-flex items-center justify-center rounded-full bg-primary px-stack-md py-stack-sm font-body text-body-md font-semibold text-on-primary transition-all duration-200 hover:shadow-glow-primary"
          >
            ETKİNLİKLERE DÖN
          </Link>

          <Link
            to="/biletlerim"
            className="inline-flex items-center justify-center rounded-full border border-outline px-stack-md py-stack-sm font-body text-body-md font-semibold text-on-surface transition-colors duration-200 hover:bg-surface-container-high"
          >
            BİLETLERİM
          </Link>
        </div>

        {/* Adres duruyor ama one cikmiyor: kullaniciya bir sey ifade
            etmiyor, buna karsilik "su sayfa acilmiyor" diyen birinden
            hangi adresi denedigini ogrenmenin en kisa yolu bu. */}
        <p className="max-w-full break-all font-body text-body-sm text-on-surface-variant/50">
          {konum.pathname}
        </p>
      </div>
    </div>
  );
}

/**
 * Ortasi bos bir koltuk sirasi.
 *
 * Emoji degil SVG: emoji her isletim sisteminde farkli ciziliyor ve rengi
 * CSS'ten degistirilemiyor. Buradaki koltuklar <c>currentColor</c>
 * kullandigi icin temanin rengini aliyor.
 *
 * Ikon dosyasina konmadi cunku ikon degil, yalnizca bu sayfaya ait bir
 * cizim; <c>Ikon.tsx</c> arayuzun her yerinde tekrar eden ogeler icin.
 */
function BosKoltukSirasi() {
  const koltuklar = [0, 1, 2, 3, 4];
  const eksik = 2;

  return (
    <svg
      viewBox="0 0 260 64"
      role="img"
      aria-label="Bir koltuğu eksik koltuk sırası"
      className="h-16 w-full max-w-xs text-on-surface-variant/35"
    >
      {koltuklar.map((sira) => {
        const x = sira * 52 + 6;
        const bosMu = sira === eksik;

        return (
          <g
            key={sira}
            // Eksik koltuk kesikli ve vurgulu; digerleri geride kalıyor
            // ki goz dogrudan bosluga gitsin.
            stroke="currentColor"
            strokeWidth={2}
            fill="none"
            className={bosMu ? 'text-primary/70' : undefined}
            strokeDasharray={bosMu ? '4 4' : undefined}
          >
            {/* Sirt */}
            <rect x={x} y={8} width={40} height={28} rx={8} />
            {/* Oturak */}
            <rect x={x - 2} y={38} width={44} height={12} rx={5} />
          </g>
        );
      })}
    </svg>
  );
}
