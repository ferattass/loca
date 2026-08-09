import { useCallback, useRef, useState } from 'react';

import { Logo } from './ui/Logo';
import { QRCodeSVG } from 'qrcode.react';

import { biletiGorselIndir, biletiPdfIndir } from '../lib/biletIndir';
import type { Bilet } from '../api/tickets';
import { BelgeIkonu, GorselIkonu } from './ui/Ikon';
import { paraKurussuz, saatBicimi } from '../lib/bicim';

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long' });
const yilBicimi = new Intl.DateTimeFormat('tr-TR', { year: 'numeric' });

/**
 * Gecerli olmayan biletin ust seridi.
 *
 * Ilk tasarimda bu bilgi kartin ortasina capraz bir damga olarak
 * basiliyordu; damga koltuk ve saat satirlarinin uzerine geliyordu ve
 * biletin en cok bakilan iki bilgisi okunmuyordu. Serit ustte, kendi
 * satirinda.
 */
const DURUM_SERIDI: Record<Bilet['status'], string | null> = {
  Valid: null,
  Used: 'Bu bilet girişte kullanıldı',
  Cancelled: 'Bu bilet iptal edildi',
  Refunded: 'Bu biletin bedeli iade edildi',
};

interface BiletKartiProps {
  bilet: Bilet;
}

/**
 * Tek bir bilet.
 *
 * <b>Gercek bir bilet gibi ciziliyor:</b> govde + perforasyon + koçan.
 * Duz bir liste satiri da ayni bilgiyi tasirdi ama kullanici bunu kapida
 * gorevliye uzatiyor; tanidik bicim, hangi tarafin okutulacagini
 * soylemeden anlatiyor.
 *
 * <para>
 * Kart KOYU DEGIL acik renkli. Uygulamanin geri kalani koyu tema ama bu
 * dugum oldugu gibi PNG ve PDF'e cevriliyor; koyu bir bilet yazicidan
 * kapkara cikar ve QR'in cevresindeki kontrast kaybolurdu.
 * </para>
 */
export function BiletKarti({ bilet }: BiletKartiProps) {
  const kartRef = useRef<HTMLDivElement>(null);
  const [indiriliyor, setIndiriliyor] = useState<'pdf' | 'png' | null>(null);
  const [hata, setHata] = useState<string | null>(null);

  const indir = useCallback(
    async (bicim: 'pdf' | 'png') => {
      const dugum = kartRef.current;
      if (!dugum) return;

      setIndiriliyor(bicim);
      setHata(null);

      try {
        await (bicim === 'pdf'
          ? biletiPdfIndir(dugum, bilet.ticketNumber)
          : biletiGorselIndir(dugum, bilet.ticketNumber));
      } catch {
        // Sebep kullaniciya tasinmiyor: buradaki hatalar tarayici
        // ic detaylari (tuval kirlenmesi, bellek) ve kullanicinin
        // yapabilecegi bir sey yok. Ekranda bilet zaten duruyor.
        setHata('Dosya oluşturulamadı, tekrar dener misin?');
      } finally {
        setIndiriliyor(null);
      }
    },
    [bilet.ticketNumber],
  );

  const baslangic = new Date(bilet.eventStartsAtUtc);
  const serit = DURUM_SERIDI[bilet.status];

  return (
    <div>
      <div ref={kartRef} className="overflow-hidden rounded-lg">
        {serit && (
          <p className="bg-[#8c1d18] px-stack-md py-base font-body text-body-sm font-semibold text-white">
            {serit}
          </p>
        )}

        <div
          className={`relative flex flex-col bg-gradient-to-br from-white to-[#efe9f7] text-[#1c1a22] shadow-[0_18px_40px_-18px_rgba(0,0,0,0.9)] sm:flex-row ${
            // Gecersiz bilet renksiz: yiginin icinde gozle ayirt edilmesi
            // seridi okumaya gerek kalmadan mumkun olmali.
            serit ? 'grayscale' : ''
          }`}
        >
        {/* Sol kenardaki renk seridi: yigin halinde duran biletlerde
            kartin nerede basladigini gozle ayirt ettiriyor. */}
        <div
          aria-hidden="true"
          // Serit logonun degradesini tasiyor: kartin ustunde zaten
          // Loca logosu var ve ikisi ayni geciste olmali.
          className="h-2 w-full bg-gradient-to-r from-[#3b1b92] via-[#aa2484] to-[#fd2d6e] sm:h-auto sm:w-2 sm:bg-gradient-to-b"
        />

        <div className="min-w-0 flex-1 p-stack-md">
          <div className="flex items-start justify-between gap-stack-sm">
            {/* Bilet karti ACIK zeminli — yazisi lacivert olan varyant
                kullaniliyor. Koyu tema varyanti burada okunmazdi. Kart
                ayrica PDF ve gorsel olarak indiriliyor; oradaki zemin de
                beyaz. */}
            <Logo bicim="yatay" acikZemin className="h-6 w-auto" />
            <span className="shrink-0 rounded-full bg-[#6d3bd7]/10 px-stack-sm py-[3px] font-body text-[11px] font-semibold uppercase tracking-wider text-[#5516be]">
              {bilet.ticketTypeName}
            </span>
          </div>

          <h3 className="mt-stack-sm font-headline text-headline-md leading-tight text-[#171520]">
            {bilet.eventTitle}
          </h3>
          <p className="mt-1 font-body text-body-sm text-[#5c5668]">
            {bilet.venueName} · {bilet.hallName}
          </p>

          <dl className="mt-stack-md grid grid-cols-3 gap-stack-sm border-t border-dashed border-[#cfc7dc] pt-stack-sm">
            <Alan baslik="Tarih">
              {tarihBicimi.format(baslangic)}
              <span className="ml-1 font-normal text-[#5c5668]">
                {yilBicimi.format(baslangic)}
              </span>
            </Alan>
            <Alan baslik="Saat">{saatBicimi.format(baslangic)}</Alan>
            <Alan baslik="Koltuk">{bilet.seatLabel}</Alan>
          </dl>
        </div>

        {/*
          Perforasyon. Centikler sayfa zeminiyle AYNI renkte; kagit
          gercekten oradan kesilmis gibi duruyor. Indirilen dosyanin zemini
          de ayni renge sabitlendigi icin (bkz. lib/biletIndir) ekranda ne
          gorunuyorsa dosyada da o gorunuyor.
        */}
        <div className="relative shrink-0 border-t border-dashed border-[#cfc7dc] sm:border-l sm:border-t-0">
          <span
            aria-hidden="true"
            className="absolute -left-[9px] -top-[9px] h-[18px] w-[18px] rounded-full bg-background sm:-top-[9px] sm:left-[-9px]"
          />
          <span
            aria-hidden="true"
            className="absolute -right-[9px] -top-[9px] h-[18px] w-[18px] rounded-full bg-background sm:-bottom-[9px] sm:left-[-9px] sm:right-auto sm:top-auto"
          />

          <div className="flex items-center gap-stack-md p-stack-md sm:w-[186px] sm:flex-col sm:gap-stack-sm">
            <div className="shrink-0 rounded-md bg-white p-2 ring-1 ring-[#e2dcec]">
              <QRCodeSVG
                value={bilet.qrCode}
                size={124}
                // Hata duzeltme seviyesi ortada: daha yuksegi deseni
                // gereksiz siklastirip telefon ekranindan okumayi
                // zorlastiriyor.
                level="M"
                marginSize={0}
                title={`${bilet.ticketNumber} numarali biletin QR kodu`}
              />
            </div>

            <div className="min-w-0 sm:text-center">
              <p className="font-mono text-[13px] font-semibold tracking-[0.08em] text-[#1c1a22]">
                {bilet.ticketNumber}
              </p>
              <p className="mt-1 font-body text-[11px] uppercase tracking-wider text-[#7a7488]">
                Girişte okutulacak
              </p>
              <p className="mt-stack-sm font-body text-body-sm font-semibold text-[#1c1a22]">
                {paraKurussuz(bilet.price, bilet.currency)}
              </p>
            </div>
          </div>
        </div>

        </div>
      </div>

      {/* Indirme dugmeleri kartin DISINDA: kart oldugu gibi goruntuye
          cevriliyor, icindeki bir dugme indirilen bilette de gorunurdu. */}
      <div className="mt-stack-sm flex flex-wrap items-center gap-base">
        <IndirmeDugmesi
          onClick={() => void indir('pdf')}
          bekliyor={indiriliyor === 'pdf'}
          etiket="PDF indir"
        >
          <BelgeIkonu className="h-4 w-4" />
        </IndirmeDugmesi>

        <IndirmeDugmesi
          onClick={() => void indir('png')}
          bekliyor={indiriliyor === 'png'}
          etiket="Görsel indir"
        >
          <GorselIkonu className="h-4 w-4" />
        </IndirmeDugmesi>
      </div>

      {hata && (
        <p role="alert" className="mt-base font-body text-body-sm text-error">
          {hata}
        </p>
      )}
    </div>
  );
}

function Alan({ baslik, children }: { baslik: string; children: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="font-body text-[10px] font-bold uppercase tracking-[0.14em] text-[#7a7488]">
        {baslik}
      </dt>
      <dd className="mt-[2px] truncate font-headline text-body-md font-bold text-[#1c1a22]">
        {children}
      </dd>
    </div>
  );
}

function IndirmeDugmesi({
  onClick,
  bekliyor,
  etiket,
  children,
}: {
  onClick: () => void;
  bekliyor: boolean;
  etiket: string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={bekliyor}
      className="inline-flex items-center gap-base rounded-full border border-outline-variant px-stack-sm py-base font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high disabled:opacity-60"
    >
      {children}
      {bekliyor ? 'Hazırlanıyor' : etiket}
    </button>
  );
}
