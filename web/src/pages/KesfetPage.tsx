import { useQuery } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';

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

  const etkinlikSorgu = useQuery({
    queryKey: ['discover-events'],
    queryFn: () => etkinlikleriGetir({ sayfaBoyutu: 24 }),
  });

  const yaklasanlar = yaklasanlariAyikla(etkinlikSorgu.data?.items ?? []);
  const oneCikanlar = yaklasanlar.slice(0, 3);
  const mekanOzet = mekanlaraGoreOzetle(yaklasanlar).slice(0, 5);
  const sehir = enYogunSehir(yaklasanlar);

  const etkinlikHata = etkinlikSorgu.isError
    ? hataMesaji(etkinlikSorgu.error, 'Etkinlikler yüklenemedi.')
    : null;

  const kategoriHata = kategoriSorgu.isError
    ? hataMesaji(kategoriSorgu.error, 'Kategoriler yüklenemedi.')
    : null;

  const oneCikanlaraKaydir = () => {
    document.getElementById(ONE_CIKANLAR_ID)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  return (
    <main className="flex flex-col">
      <HeroBolumu onKaydir={oneCikanlaraKaydir} />

      <div className="mx-auto flex w-full max-w-7xl flex-col gap-stack-lg px-container-margin-mobile py-stack-lg md:px-container-margin-desktop">
        <KategorilerBolumu
          kategoriler={kategoriSorgu.data ?? []}
          yukleniyor={kategoriSorgu.isPending}
          hata={kategoriHata}
        />

        <OneCikanlarBolumu
          etkinlikler={oneCikanlar}
          yukleniyor={etkinlikSorgu.isPending}
          hata={etkinlikHata}
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

      <div className="relative mx-auto flex max-w-7xl flex-col gap-stack-sm px-container-margin-mobile py-stack-lg md:px-container-margin-desktop md:py-24">
        <span className="w-fit rounded-full border border-primary/40 bg-primary-container/20 px-stack-sm py-1 font-body text-label-caps text-primary">
          CANLI ETKİNLİKLER YAYINDA
        </span>

        <h1 className="font-display text-display-lg-mobile text-on-surface md:text-display-lg">
          Loca ile
          <br />
          <span className="text-primary">Eğlenceyi Keşfet</span>
        </h1>

        <p className="max-w-xl font-body text-body-md text-on-surface-variant">
          Şehrin en özel konserlerinden teknoloji zirvelerine kadar tüm etkinlikler tek yerde.
          Biletini hemen al, anını kaçırma.
        </p>

        <div className="mt-stack-sm flex flex-wrap gap-stack-sm">
          <Button type="button" onClick={onKaydir}>
            HEMEN KEŞFET
          </Button>
          {/* Sunucudaki GET /events tarih filtresi desteklemiyor; "bugunun
              etkinlikleri" ayri bir sorgu degil, ayni listeye kaydiriyor. */}
          <Button type="button" gorunum="cizgili" onClick={onKaydir}>
            BUGÜNÜN ETKİNLİKLERİ
          </Button>
        </div>
      </div>
    </section>
  );
}

interface KategorilerBolumuProps {
  kategoriler: EtkinlikKategorisi[];
  yukleniyor: boolean;
  hata: string | null;
}

function KategorilerBolumu({ kategoriler, yukleniyor, hata }: KategorilerBolumuProps) {
  return (
    <section aria-labelledby="kategoriler-baslik" className="flex flex-col gap-stack-sm">
      <div className="flex items-center justify-between gap-stack-sm">
        <h2 id="kategoriler-baslik" className="font-headline text-headline-md text-on-surface">
          Kategoriler
        </h2>

        {/* Kategoriye gore filtreleme ekrani yok; bu yuzden tiklanabilir
            degil, yalnizca tasarimdaki basligin gorsel karsiligi. */}
        <span className="font-body text-label-caps text-on-surface-variant">TÜMÜNÜ GÖR</span>
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
        <div className="flex flex-wrap gap-stack-md" aria-hidden="true">
          {[0, 1, 2, 3].map((anahtar) => (
            <div key={anahtar} className="flex flex-col items-center gap-base">
              <div className="h-16 w-16 animate-pulse rounded-full bg-surface-variant/40" />
              <div className="h-3 w-12 animate-pulse rounded bg-surface-variant/40" />
            </div>
          ))}
        </div>
      )}

      {!yukleniyor && !hata && kategoriler.length === 0 && (
        <p className="font-body text-body-sm text-on-surface-variant">
          Henüz tanımlı kategori yok.
        </p>
      )}

      {!yukleniyor && kategoriler.length > 0 && (
        <div className="flex flex-wrap gap-stack-md">
          {kategoriler.map((kategori) => (
            <div key={kategori.id} className="flex flex-col items-center gap-base">
              <span
                aria-hidden="true"
                className="grid h-16 w-16 place-items-center rounded-full bg-surface-container-high text-primary"
              >
                {kategoriIkonu(kategori.slug)}
              </span>
              <span className="font-body text-body-sm text-on-surface">{kategori.name}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

interface OneCikanlarBolumuProps {
  etkinlikler: EtkinlikOzeti[];
  yukleniyor: boolean;
  hata: string | null;
}

function OneCikanlarBolumu({ etkinlikler, yukleniyor, hata }: OneCikanlarBolumuProps) {
  return (
    <section
      id={ONE_CIKANLAR_ID}
      aria-labelledby="one-cikanlar-baslik"
      className="scroll-mt-24 flex flex-col gap-stack-sm"
    >
      <div>
        <h2 id="one-cikanlar-baslik" className="font-headline text-headline-md text-on-surface">
          Öne Çıkanlar
        </h2>
        {/* Sunucuda bir "trend" sinyali yok; siralama tarihe gore —
            gercekte olcebildigimiz tek kriter bu. */}
        <p className="font-body text-body-sm text-on-surface-variant">En yakın tarihli etkinlikler</p>
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
        <div className="grid gap-stack-md md:grid-cols-2 lg:grid-cols-3" aria-hidden="true">
          {[0, 1, 2].map((anahtar) => (
            <div key={anahtar} className="h-80 animate-pulse rounded-lg bg-surface-variant/40" />
          ))}
        </div>
      )}

      {!yukleniyor && !hata && etkinlikler.length === 0 && (
        <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-stack-md font-body text-body-sm text-on-surface-variant">
          Şu anda yaklaşan bir etkinlik yok.
        </p>
      )}

      {!yukleniyor && etkinlikler.length > 0 && (
        <div className="grid gap-stack-md md:grid-cols-2 lg:grid-cols-3">
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
function kategoriIkonu(slug: string) {
  const anahtar = slug.toLowerCase();

  if (anahtar.includes('muzik') || anahtar.includes('konser')) return <MuzikIkonu />;
  if (anahtar.includes('tiyatro') || anahtar.includes('sahne')) return <TiyatroIkonu />;
  if (anahtar.includes('gece') || anahtar.includes('parti') || anahtar.includes('club'))
    return <GeceHayatiIkonu />;
  if (anahtar.includes('egitim') || anahtar.includes('seminer') || anahtar.includes('workshop'))
    return <EgitimIkonu />;

  return <GenelIkonu />;
}

function MuzikIkonu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.5">
      <circle cx="6" cy="18" r="2.5" />
      <circle cx="16" cy="16" r="2.5" />
      <path d="M8.5 18V6.5L18.5 4v11.5" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function TiyatroIkonu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.5">
      <circle cx="9" cy="10" r="5" />
      <circle cx="15" cy="14" r="5" />
      <path d="M6.5 9.5c1 1.2 3 1.2 4 0" strokeLinecap="round" />
      <path d="M13 15.5c1-1.2 3-1.2 4 0" strokeLinecap="round" />
    </svg>
  );
}

function GeceHayatiIkonu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M4 5h16l-8 9-8-9Z" strokeLinejoin="round" />
      <path d="M12 14v6M8.5 20h7" strokeLinecap="round" />
    </svg>
  );
}

function EgitimIkonu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M12 5 2 9.5 12 14l10-4.5L12 5Z" strokeLinejoin="round" />
      <path d="M6 11.5V17c0 1.1 2.7 2.5 6 2.5s6-1.4 6-2.5v-5.5" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M22 9.5V15" strokeLinecap="round" />
    </svg>
  );
}

function GenelIkonu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path
        d="M4 8a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v2a2 2 0 0 0 0 4v2a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-2a2 2 0 0 0 0-4V8Z"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function MekanIkonu() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth="1.5">
      <path d="M5 21V5a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v16" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M13 21V9a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v12" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M8 8h1M8 12h1M8 16h1" strokeLinecap="round" />
    </svg>
  );
}
