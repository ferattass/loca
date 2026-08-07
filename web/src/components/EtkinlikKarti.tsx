import { useState } from 'react';
import { Link } from 'react-router-dom';

import { dosyaAdresi } from '../api/client';
import type { EtkinlikOzeti } from '../api/eventCatalog';

// Ay adi Intl'den Turkce geliyor ama kucuk harfle ("eylul" degil "Eylul").
// Tasarimdaki buyuk harfli etiket icin CSS text-transform KULLANILMIYOR:
// tarayici locale bilmeden uppercase yaparsa Turkce "i" harfi "I"ya doner
// (nokta kaybolur). toLocaleUpperCase('tr-TR') bunu doğru yapiyor.
const tarihBicimi = new Intl.DateTimeFormat('tr-TR', { day: '2-digit', month: 'long' });
const gunBicimi = new Intl.DateTimeFormat('tr-TR', { weekday: 'short' });
const saatBicimi = new Intl.DateTimeFormat('tr-TR', { hour: '2-digit', minute: '2-digit' });

const paraBicimi = new Intl.NumberFormat('tr-TR', {
  style: 'currency',
  currency: 'TRY',
  maximumFractionDigits: 0,
});

interface EtkinlikKartiProps {
  etkinlik: EtkinlikOzeti;
}

/**
 * Kesfet listesindeki etkinlik karti.
 *
 * Afis gorseli artik GERCEKTEN cizilebiliyor: `posterFileId` sunucudan
 * geliyordu ama onu servis eden bir uc yoktu, bu yuzden kart dekoratif bir
 * degrade gosteriyordu. Uc yazildi. Afisi olmayan etkinlikte ayni degrade
 * yer tutucu duruyor.
 *
 * Gorsel yuklenemezse (dosya silinmis, adres bozuk) sessizce yer tutucuya
 * dusuyor: kirik gorsel ikonu, bos bir degradeden daha kotu gorunur.
 *
 * Favori butonu Link'in DISINDA, ayri bir kardes eleman: <button>'i <a>
 * icine koymak gecersiz HTML olurdu (ic ice etkilesimli eleman). Kart
 * govdesi tek buyuk Link, kalp onun uzerine mutlak konumla biniyor.
 */
export function EtkinlikKarti({ etkinlik }: EtkinlikKartiProps) {
  const tarih = new Date(etkinlik.eventDateUtc);
  const tarihEtiketi = tarihBicimi.format(tarih).toLocaleUpperCase('tr-TR');
  const gunEtiketi = gunBicimi.format(tarih).toLocaleUpperCase('tr-TR');
  const saatEtiketi = saatBicimi.format(tarih);
  const [gorselBozuk, setGorselBozuk] = useState(false);
  const afis = gorselBozuk ? null : dosyaAdresi(etkinlik.posterFileId);

  return (
    <article className="group relative flex flex-col overflow-hidden rounded-lg glass border border-transparent transition-colors hover:border-primary/50">
      <Link to={`/etkinlikler/${etkinlik.id}`} className="flex flex-1 flex-col">
        <div className="relative aspect-[3/4] w-full overflow-hidden">
          <div
            aria-hidden="true"
            className="absolute inset-0 bg-gradient-to-br from-primary-container/60 via-surface-container-high to-secondary-container/30 transition-transform duration-300 group-hover:scale-105"
          />

          {afis && (
            <img
              src={afis}
              // Afis DEKORATIF sayiliyor: etkinligin adi hemen altinda
              // yaziyor ve gorseli ayrica tarif etmek ekran okuyucuda ayni
              // bilgiyi iki kez okuturdu.
              alt=""
              loading="lazy"
              onError={() => setGorselBozuk(true)}
              className="absolute inset-0 h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
            />
          )}
        </div>

        <div className="flex flex-1 flex-col gap-1 p-stack-sm">
          {/* Tarih SAAT ile birlikte: bir biletleme sitesinde "hangi gun"
              kadar "saat kacta" da karar veriyor ve kullanici bunu
              ogrenmek icin detaya girmek zorunda kalmamali. */}
          <p className="font-body text-label-caps text-primary">
            {tarihEtiketi} · {gunEtiketi} · {saatEtiketi}
          </p>

          <h3 className="font-headline text-title-lg text-on-surface line-clamp-2">
            {etkinlik.title}
          </h3>

          <p className="flex items-center gap-1 font-body text-body-sm text-on-surface-variant">
            <KonumIkonu />
            <span className="truncate">
              {etkinlik.venueName}, {etkinlik.cityName}
            </span>
          </p>

          {/* Fiyat sunucudan geliyor (aktif bilet turlerinin en dususu).
              Kartta gosterilmesi, kullanicinin sekiz karti tek tek acip
              fiyata bakmasini onluyor. */}
          <div className="mt-auto flex items-center justify-between gap-base pt-base">
            <span className="font-body text-body-sm text-on-surface-variant">
              {etkinlik.minPrice !== null ? (
                <>
                  <span className="text-on-surface-variant/70">başlangıç </span>
                  <strong className="font-semibold text-on-surface">
                    {paraBicimi.format(etkinlik.minPrice)}
                  </strong>
                </>
              ) : (
                'Bilet bilgisi yok'
              )}
            </span>

            <span className="shrink-0 rounded-md bg-surface-container-high px-stack-sm py-1 font-body text-body-sm font-semibold text-on-surface transition-colors group-hover:bg-primary group-hover:text-on-primary">
              BİLET AL
            </span>
          </div>
        </div>
      </Link>

      <FavoriButonu baslik={etkinlik.title} />
    </article>
  );
}

/**
 * Favori butonu.
 *
 * Sunucuda favori ucu yok. Sahte bir istek atip basariliymis gibi
 * davranmak yerine tiklamada ozelligin henuz gelmedigini soyleyen kisa bir
 * bilgi gosterilir. Buton yine de gorsel olarak basilabilir durumda
 * (sartname geregi aria-pressed tasiyor); bu durum yalnizca oturum
 * icinde tutulur, sayfa yenilendiginde sifirlanir — hicbir yere
 * kaydedilmedigini ima etmiyoruz.
 */
function FavoriButonu({ baslik }: { baslik: string }) {
  const [secili, setSecili] = useState(false);
  const [bilgiGorunur, setBilgiGorunur] = useState(false);

  const tikla = () => {
    setSecili((onceki) => !onceki);
    setBilgiGorunur(true);
    window.setTimeout(() => setBilgiGorunur(false), 2000);
  };

  return (
    <div className="absolute right-2 top-2 z-10 flex flex-col items-end gap-1">
      <button
        type="button"
        onClick={tikla}
        aria-pressed={secili}
        aria-label={secili ? `${baslik} favorilerden çıkar` : `${baslik} favorilere ekle`}
        className="flex h-8 w-8 items-center justify-center rounded-full bg-surface-container-lowest/70 text-on-surface backdrop-blur-glass transition-colors hover:text-primary"
      >
        <KalpIkonu dolu={secili} />
      </button>

      {bilgiGorunur && (
        <span
          role="status"
          className="rounded-md bg-surface-container-highest px-stack-sm py-1 font-body text-[11px] text-on-surface shadow-lg"
        >
          Yakında
        </span>
      )}
    </div>
  );
}

function KonumIkonu() {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true" className="h-4 w-4 shrink-0 fill-current">
      <path d="M10 2a6 6 0 0 0-6 6c0 4.5 6 10 6 10s6-5.5 6-10a6 6 0 0 0-6-6Zm0 8.25A2.25 2.25 0 1 1 10 5.75a2.25 2.25 0 0 1 0 4.5Z" />
    </svg>
  );
}

function KalpIkonu({ dolu }: { dolu: boolean }) {
  return (
    <svg
      viewBox="0 0 20 20"
      aria-hidden="true"
      className="h-4 w-4"
      fill={dolu ? 'currentColor' : 'none'}
      stroke="currentColor"
      strokeWidth={dolu ? 0 : 1.5}
    >
      <path d="M10 17.25s-6.5-4.06-8.5-8.06C.36 6.36 1.9 3.5 4.7 3.1c1.62-.23 3.2.5 4.1 1.9.9-1.4 2.48-2.13 4.1-1.9 2.8.4 4.34 3.26 3.2 6.09-2 4-8.5 8.06-8.5 8.06Z" />
    </svg>
  );
}
