import { useQuery } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { useSearchParams } from 'react-router-dom';

import {
  KesfetSuzgeci,
  zamanAraligi,
  type ZamanSecimi,
} from '../components/KesfetSuzgeci';

import { hataMesaji } from '../api/client';
import {
  enYogunSehir,
  etkinlikleriGetir,
  mekanlaraGoreOzetle,
  yaklasanlariAyikla,
} from '../api/eventCatalog';
import type { EtkinlikOzeti, MekanOzetSatiri } from '../api/eventCatalog';
import { kategorileriGetir } from '../api/events';
import type { EtkinlikKategorisi } from '../api/events';
import { EtkinlikKarti } from '../components/EtkinlikKarti';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';

const ONE_CIKANLAR_ID = 'one-cikanlar';

/**
 * Kesfet (ana sayfa) ekrani.
 *
 * Tasarim: docs/gorseller/01-kesfet.png. Orijinal tasarimdaki "Sana Ozel
 * Firsatlar" (indirim kodlari) ve alt bilgi (footer) burada YOK: gorev
 * tanimindaki bes bolume dahil degiller, sunucuda karsiliklari da yok —
 * indirim kodu uydurmak yanlis olurdu. Alt bilgi zaten SiteKabugu
 * uzerinden geliyor.
 */
export function KesfetPage() {
  const kategoriSorgu = useQuery<EtkinlikKategorisi[]>({
    queryKey: ['event-categories'],
    queryFn: kategorileriGetir,
  });

  // Secili kategori. null = tumu.
  const [kategoriId, setKategoriId] = useState<string | null>(null);

  /**
   * Suzgecler adres cubugunda tasiniyor (`?ne_zaman=bugun&ara=konser`).
   *
   * Bilesenin kendi durumunda tutulsalardi ust menudeki "Bugün" sekmesi
   * ayni sayfaya gidip hicbir sey degistiremez, kullanici suzulmus
   * listeyi paylasamaz ve geri tusu calismazdi.
   */
  const [aramaParametreleri, setAramaParametreleri] = useSearchParams();

  const zaman = (aramaParametreleri.get('ne_zaman') ?? 'hepsi') as ZamanSecimi;
  const arananMetin = aramaParametreleri.get('ara') ?? '';
  const aralik = zamanAraligi(zaman);

  const parametreYaz = (anahtar: string, deger: string | null) => {
    const yeni = new URLSearchParams(aramaParametreleri);

    if (deger === null || deger === '' || deger === 'hepsi') {
      yeni.delete(anahtar);
    } else {
      yeni.set(anahtar, deger);
    }

    setAramaParametreleri(yeni, { replace: true });
  };

  const etkinlikSorgu = useQuery({
    // Butun suzgecler onbellek anahtarinda: biri degistiginde react-query
    // yeni istek atiyor ve onceki sonucu geri geldiginde uzerine yazmiyor.
    queryKey: ['discover-events', kategoriId, zaman, arananMetin],
    queryFn: () =>
      etkinlikleriGetir({
        sayfaBoyutu: 24,
        kategoriId: kategoriId ?? undefined,
        arama: arananMetin || undefined,
        baslangicUtc: aralik.bas,
        bitisUtc: aralik.bit,
      }),
  });

  const yaklasanlar = yaklasanlariAyikla(etkinlikSorgu.data?.items ?? []);

  // Herhangi bir suzgec varken kirpma yapilmiyor: kullanici artik "one
  // cikanlara" degil arattigi seyin tamamina bakiyor. Suzgecsiz ana
  // sayfada da liste artik kirpilmiyor — uc kart, alti etkinligi olan
  // bir katalogu bos gosteriyordu.
  const oneCikanlar = yaklasanlar;

  const mekanOzet = mekanlaraGoreOzetle(yaklasanlar).slice(0, 5);
  const sehir = enYogunSehir(yaklasanlar);

  const seciliKategori = kategoriSorgu.data?.find((kategori) => kategori.id === kategoriId) ?? null;

  const etkinlikHata = etkinlikSorgu.isError
    ? hataMesaji(etkinlikSorgu.error, 'Etkinlikler yüklenemedi.')
    : null;

  const kategoriHata = kategoriSorgu.isError
    ? hataMesaji(kategoriSorgu.error, 'Kategoriler yüklenemedi.')
    : null;

  const baslikMetni = arananMetin
    ? `"${arananMetin}" sonuçları`
    : seciliKategori
      ? seciliKategori.name
      : zaman === 'bugun'
        ? 'Bugün'
        : zaman === 'yarin'
          ? 'Yarın'
          : zaman === 'hafta'
            ? 'Bu hafta'
            : 'Yaklaşan etkinlikler';

  const altBaslikMetni = seciliKategori
    ? `${seciliKategori.name} kategorisindeki yaklaşan etkinlikler`
    : 'En yakın tarihten başlayarak';

  const oneCikanlaraKaydir = () => {
    document.getElementById(ONE_CIKANLAR_ID)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  return (
    <main className="flex flex-col">
      <HeroBolumu onKaydir={oneCikanlaraKaydir} />

      <div className="mx-auto flex w-full max-w-7xl flex-col gap-stack-md px-container-margin-mobile pb-stack-lg md:px-container-margin-desktop">
        <KesfetSuzgeci
          kategoriler={kategoriSorgu.data ?? []}
          seciliKategori={kategoriId}
          onKategori={setKategoriId}
          zaman={zaman}
          onZaman={(deger) => parametreYaz('ne_zaman', deger)}
          arama={arananMetin}
          onArama={(deger) => parametreYaz('ara', deger)}
          sonucSayisi={etkinlikSorgu.data ? yaklasanlar.length : null}
        />

        {kategoriHata && (
          <p role="alert" className="font-body text-body-sm text-error">
            {kategoriHata}
          </p>
        )}

        <OneCikanlarBolumu
          baslik={baslikMetni}
          altBaslik={altBaslikMetni}
          etkinlikler={oneCikanlar}
          yukleniyor={etkinlikSorgu.isPending}
          hata={etkinlikHata}
          bosMetin={
            arananMetin
              ? `"${arananMetin}" için sonuç bulunamadı.`
              : zaman !== 'hepsi'
                ? 'Seçilen tarihte etkinlik yok.'
                : seciliKategori
                  ? 'Bu kategoride yaklaşan etkinlik yok.'
                  : 'Şu anda yaklaşan etkinlik yok.'
          }
        />

        <EtrafindaBolumu
          mekanlar={mekanOzet}
          sehir={sehir}
          yukleniyor={etkinlikSorgu.isPending}
          hata={etkinlikHata}
        />

        <BultenBolumu />
      </div>
    </main>
  );
}

/**
 * Kahraman bolumu.
 *
 * <b>Ekranin tamamini kaplamiyor artik.</b> Onceden dikeyde yirmi dort
 * birim dolgu vardi ve ilk ekranda tek bir etkinlik gorunmuyordu — bir
 * biletleme sitesinde ilk gorulmesi gereken sey bilettir. Yukseklik
 * yariya indi, suzgec cubugu ve ilk kart sirasi ilk ekrana girdi.
 */
function HeroBolumu({ onKaydir }: { onKaydir: () => void }) {
  return (
    <section className="relative overflow-hidden">
      {/* Gercek bir sahne fotografi yok; "canli etkinlik" hissi icin
          tasarim sistemindeki neon renklerden bir degrade kullanildi. */}
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-br from-surface-container-lowest via-primary-container/25 to-secondary-container/20"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-t from-background via-background/70 to-transparent"
      />

      <div className="relative mx-auto flex max-w-7xl flex-col gap-stack-sm px-container-margin-mobile py-stack-md md:px-container-margin-desktop md:py-stack-lg">
        <span className="w-fit rounded-full border border-primary/40 bg-primary-container/20 px-stack-sm py-1 font-body text-label-caps text-primary">
          CANLI ETKİNLİKLER YAYINDA
        </span>

        <h1 className="font-display text-display-lg-mobile text-on-surface">
          Loca ile <span className="text-primary">Eğlenceyi Keşfet</span>
        </h1>

        <p className="max-w-xl font-body text-body-md text-on-surface-variant">
          Şehrin en özel konserlerinden teknoloji zirvelerine kadar tüm etkinlikler tek yerde.
          Biletini hemen al, anını kaçırma.
        </p>

        <div className="mt-base flex flex-wrap gap-stack-sm">
          <Button type="button" onClick={onKaydir}>
            HEMEN KEŞFET
          </Button>
        </div>
      </div>
    </section>
  );
}

interface OneCikanlarBolumuProps {
  baslik: string;
  altBaslik: string;
  bosMetin: string;
  etkinlikler: EtkinlikOzeti[];
  yukleniyor: boolean;
  hata: string | null;
}

function OneCikanlarBolumu({
  baslik,
  altBaslik,
  bosMetin,
  etkinlikler,
  yukleniyor,
  hata,
}: OneCikanlarBolumuProps) {
  return (
    <section
      id={ONE_CIKANLAR_ID}
      aria-labelledby="one-cikanlar-baslik"
      className="scroll-mt-24 flex flex-col gap-stack-sm"
    >
      <div>
        {/* Baslik disaridan: kategori seciliyken bolum artik "one
            cikanlar" degil o kategorinin listesi ve ayni basligi
            birakmak yanlis bilgi olurdu. */}
        <h2 id="one-cikanlar-baslik" className="font-headline text-headline-md text-on-surface">
          {baslik}
        </h2>
        {/* Sunucuda bir "trend" sinyali yok; siralama tarihe gore —
            gercekte olcebildigimiz tek kriter bu. */}
        <p className="font-body text-body-sm text-on-surface-variant">{altBaslik}</p>
      </div>

      {hata && (
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hata}
        </p>
      )}

      {yukleniyor && (
        <div
          className="grid grid-cols-2 gap-stack-sm sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5"
          aria-hidden="true"
        >
          {[0, 1, 2].map((anahtar) => (
            <div key={anahtar} className="h-80 animate-pulse rounded-lg bg-surface-variant/40" />
          ))}
        </div>
      )}

      {!yukleniyor && !hata && etkinlikler.length === 0 && (
        <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-stack-md font-body text-body-sm text-on-surface-variant">
          {bosMetin}
        </p>
      )}

      {!yukleniyor && etkinlikler.length > 0 && (
        <div className="grid grid-cols-2 gap-stack-sm sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
          {etkinlikler.map((etkinlik) => (
            <EtkinlikKarti key={etkinlik.id} etkinlik={etkinlik} />
          ))}
        </div>
      )}
    </section>
  );
}

interface EtrafindaBolumuProps {
  mekanlar: MekanOzetSatiri[];
  sehir: string | null;
  yukleniyor: boolean;
  hata: string | null;
}

function EtrafindaBolumu({ mekanlar, sehir, yukleniyor, hata }: EtrafindaBolumuProps) {
  return (
    <section aria-labelledby="etrafinda-baslik" className="flex flex-col gap-stack-sm">
      <div>
        <h2 id="etrafinda-baslik" className="font-headline text-headline-md text-on-surface">
          Etrafında Neler Oluyor?
        </h2>
        <p className="max-w-xl font-body text-body-sm text-on-surface-variant">
          {sehir
            ? `Şu anda ${sehir} içindeki mekanları ve yaklaşan etkinlikleri keşfet.`
            : 'Yaklaşan etkinliklerin gerçekleştiği mekanları keşfet.'}{' '}
          {/* Konum verisi yok; "km mesafe" gosterilmiyor — siralama
              yalnizca yaklasan etkinlik sayisina gore. */}
          Liste, en çok yaklaşan etkinliğe sahip mekanlara göre sıralanır.
        </p>
      </div>

      {hata && (
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hata}
        </p>
      )}

      {yukleniyor && (
        <ul className="flex flex-col gap-base" aria-hidden="true">
          {[0, 1, 2].map((anahtar) => (
            <li key={anahtar} className="h-14 animate-pulse rounded-lg bg-surface-variant/40" />
          ))}
        </ul>
      )}

      {!yukleniyor && !hata && mekanlar.length === 0 && (
        <p className="font-body text-body-sm text-on-surface-variant">
          Şu anda listelenecek mekan yok.
        </p>
      )}

      {!yukleniyor && mekanlar.length > 0 && (
        <ul className="flex flex-col gap-base">
          {mekanlar.map((mekan) => (
            <li
              key={mekan.venueName}
              className="flex items-center gap-stack-sm rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-stack-sm"
            >
              <span
                aria-hidden="true"
                className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-surface-container-high text-secondary"
              >
                <MekanIkonu />
              </span>
              <div>
                <p className="font-body text-body-md text-on-surface">{mekan.venueName}</p>
                <p className="font-body text-body-sm text-on-surface-variant">
                  {mekan.yaklasanEtkinlikSayisi} yaklaşan etkinlik
                </p>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function BultenBolumu() {
  const [eposta, setEposta] = useState('');
  const [mesaj, setMesaj] = useState<string | null>(null);

  // Bulten ucu sunucuda yok. Sahte bir istek atip basariliymis gibi
  // davranmak yerine durum acikca bildiriliyor; e-posta hicbir yere
  // gonderilmiyor, yalnizca form alaninda kaliyor.
  const gonder = (olay: FormEvent<HTMLFormElement>) => {
    olay.preventDefault();
    setMesaj('Bülten kaydı yakında açılacak.');
  };

  return (
    <section
      aria-labelledby="bulten-baslik"
      className="flex flex-col items-center gap-stack-sm rounded-xl glass p-stack-lg text-center"
    >
      <h2 id="bulten-baslik" className="font-headline text-headline-md text-on-surface">
        Gelişmelerden Haberdar Ol
      </h2>
      <p className="max-w-md font-body text-body-sm text-on-surface-variant">
        Favori sanatçıların ve şehrindeki yeni etkinlikler ilk sana ulaşsın.
      </p>

      <form onSubmit={gonder} className="flex w-full max-w-md flex-col gap-stack-sm sm:flex-row sm:items-start">
        <div className="flex-1 text-left">
          <TextField
            etiket="E-posta adresin"
            type="email"
            required
            value={eposta}
            onChange={(olay) => setEposta(olay.target.value)}
            placeholder="ornek@eposta.com"
          />
        </div>
        <Button type="submit">ABONE OL</Button>
      </form>

      {mesaj && (
        <p role="status" className="font-body text-body-sm text-primary">
          {mesaj}
        </p>
      )}
    </section>
  );
}

/** Kategori ikonlari slug'a gore eslenir; taninmayan slug genel bir bilet ikonu alir. */
function MekanIkonu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M5 21V5a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v16" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M13 21V9a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v12" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M8 8h1M8 12h1M8 16h1" strokeLinecap="round" />
    </svg>
  );
}
