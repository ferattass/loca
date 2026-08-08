import { useCallback, useRef, useState } from 'react';
import { useMutation } from '@tanstack/react-query';

import { hataKodu, hataMesaji } from '../api/client';
import { biletOkut, type BiletOkutmaSonucu } from '../api/tickets';
import { useQrOkuyucu } from '../hooks/useQrOkuyucu';
import { CarpiIkonu, OkutmaIkonu, OnayIkonu, UyariIkonu } from '../components/ui/Ikon';
import { saatBicimi } from '../lib/bicim';


/** Gecmis listesinde tutulan okutma sayisi. */
const GECMIS_SINIRI = 12;

/**
 * Ret sebebinin gorevliye gosterilen metni.
 *
 * Metin sunucudan gelen `message` alanindan DEGIL buradan okunuyor.
 * Sunucudaki metinler kayit icin; ekranda gorunen her sey arayuzun
 * sorumlulugunda ve tam Turkce karakterlerle yazilmali. Sunucudaki bir
 * ifade degistiginde kapidaki ekran degismemeli.
 */
const RET_METNI: Record<string, string> = {
  Used: 'Bu bilet daha önce kullanılmış.',
  Cancelled: 'Bu bilet iptal edilmiş.',
  Refunded: 'Bu biletin bedeli iade edilmiş.',
};

function retMetni(sonuc: BiletOkutmaSonucu): string {
  return RET_METNI[sonuc.status] ?? 'Bu bilet geçerli değil.';
}

interface GecmisSatiri {
  anahtar: string;
  kabul: boolean;
  baslik: string;
  ayrinti: string;
  an: Date;
}

/**
 * Kapida bilet okutma ekrani.
 *
 * <b>Karar bir bakista okunmali.</b> Gorevli sirada bekleyen kalabaligin
 * onunde duruyor; sonuc bu yuzden tam ekran genisliginde tek bir renk
 * blogu olarak veriliyor, satir arasinda okunacak bir metin olarak degil.
 *
 * <para>
 * Sunucu reddi de basarili cevap donuyor (bkz. <c>/tickets/check-in</c>);
 * ekran karari <c>verdict</c> alanina gore veriyor, mesaj metnine gore
 * degil. Sunucudaki metin degistiginde ekranin rengi degismemeli.
 * </para>
 */
export function KapiOkutmaPage() {
  const [kameraAcik, setKameraAcik] = useState(true);
  const [sonuc, setSonuc] = useState<BiletOkutmaSonucu | null>(null);
  const [hataMetni, setHataMetni] = useState<string | null>(null);
  const [gecmis, setGecmis] = useState<GecmisSatiri[]>([]);
  const [elleKod, setElleKod] = useState('');

  // Istek surerken gelen yeni okumalar yok sayiliyor: kamera bileti
  // gormeye devam ettigi icin cevabi beklerken ikinci bir istek gider ve
  // sunucuda "zaten kullanildi" cevabini kendi istegimiz uretirdi.
  const bekliyorRef = useRef(false);

  const gecmiseEkle = useCallback((satir: GecmisSatiri) => {
    setGecmis((onceki) => [satir, ...onceki].slice(0, GECMIS_SINIRI));
  }, []);

  const okut = useMutation({
    mutationFn: biletOkut,
    onSuccess: (cevap) => {
      setHataMetni(null);
      setSonuc(cevap);
      gecmiseEkle({
        anahtar: `${cevap.ticketId}-${Date.now()}`,
        kabul: cevap.admitted,
        baslik: cevap.ticketNumber,
        ayrinti: cevap.admitted ? cevap.seatLabel : retMetni(cevap),
        an: new Date(),
      });
    },
    onError: (hata) => {
      const kod = hataKodu(hata);
      const mesaj =
        kod === 'Ticket.QrNotRecognised'
          ? 'Bu kod bir bilete ait değil.'
          : hataMesaji(hata, 'Okutma yapılamadı.');

      setSonuc(null);
      setHataMetni(mesaj);
      gecmiseEkle({
        anahtar: `hata-${Date.now()}`,
        kabul: false,
        baslik: 'Tanınmayan kod',
        ayrinti: mesaj,
        an: new Date(),
      });
    },
    onSettled: () => {
      bekliyorRef.current = false;
    },
  });

  const kodGeldi = useCallback(
    (kod: string) => {
      if (bekliyorRef.current) return;

      bekliyorRef.current = true;
      okut.mutate(kod);
    },
    [okut],
  );

  const { videoRef, hata: kameraHatasi } = useQrOkuyucu({ acik: kameraAcik, onKod: kodGeldi });

  const elleGonder = (olay: React.FormEvent) => {
    olay.preventDefault();
    const temiz = elleKod.trim();
    if (temiz.length === 0 || bekliyorRef.current) return;

    bekliyorRef.current = true;
    okut.mutate(temiz);
    setElleKod('');
  };

  return (
    <main className="min-h-screen px-container-margin-mobile py-stack-lg md:px-container-margin-desktop">
      <div className="mx-auto max-w-5xl">
        <header className="mb-stack-md flex flex-wrap items-end justify-between gap-stack-sm">
          <div>
            <p className="font-body text-label-caps uppercase tracking-widest text-primary">
              Giriş kontrolü
            </p>
            <h1 className="font-headline text-headline-md text-on-surface">Bilet okut</h1>
            <p className="mt-1 font-body text-body-sm text-on-surface-variant">
              QR kodu kameraya tut. Yalnızca kendi etkinliklerinin biletlerini okutabilirsin.
            </p>
          </div>

          <button
            type="button"
            onClick={() => setKameraAcik((acik) => !acik)}
            className="inline-flex items-center gap-base rounded-full border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
          >
            <OkutmaIkonu className="h-4 w-4" />
            {kameraAcik ? 'Kamerayı kapat' : 'Kamerayı aç'}
          </button>
        </header>

        <div className="grid gap-stack-md lg:grid-cols-[minmax(0,1fr)_320px]">
          <div>
            <div className="relative overflow-hidden rounded-lg border border-outline-variant/40 bg-surface-container-lowest">
              {/* Video her zaman DOM'da: kamera kapatilip acildiginda
                  elemani yeniden olusturmak Safari'de akisi bazen hic
                  baslatmiyor. */}
              <video
                ref={videoRef}
                muted
                playsInline
                className={`aspect-video w-full object-cover ${kameraAcik ? '' : 'hidden'}`}
              />

              {!kameraAcik && (
                <div className="flex aspect-video w-full items-center justify-center">
                  <p className="font-body text-body-sm text-on-surface-variant">Kamera kapalı</p>
                </div>
              )}
            </div>

            {kameraHatasi && (
              <p
                role="status"
                className="mt-stack-sm flex items-center gap-base rounded-md border border-tertiary/40 bg-tertiary-container/10 px-stack-sm py-base font-body text-body-sm text-tertiary"
              >
                <UyariIkonu className="h-4 w-4 shrink-0" />
                {kameraHatasi}
              </p>
            )}

            <SonucPaneli sonuc={sonuc} hata={hataMetni} bekliyor={okut.isPending} />

            <form onSubmit={elleGonder} className="mt-stack-md">
              <label
                htmlFor="elle-kod"
                className="font-body text-body-sm text-on-surface-variant"
              >
                Kod okunmuyorsa elle gir
              </label>
              <div className="mt-base flex gap-base">
                <input
                  id="elle-kod"
                  value={elleKod}
                  onChange={(olay) => setElleKod(olay.target.value)}
                  autoComplete="off"
                  spellCheck={false}
                  className="min-w-0 flex-1 rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-mono text-body-sm text-on-surface"
                  placeholder="Biletin altındaki kod"
                />
                <button
                  type="submit"
                  disabled={okut.isPending}
                  className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary disabled:opacity-60"
                >
                  Okut
                </button>
              </div>
            </form>
          </div>

          <aside>
            <h2 className="font-body text-label-caps uppercase tracking-widest text-on-surface-variant">
              Son okutmalar
            </h2>

            {gecmis.length === 0 ? (
              <p className="mt-stack-sm font-body text-body-sm text-on-surface-variant">
                Henüz okutma yok.
              </p>
            ) : (
              <ul className="mt-stack-sm space-y-base">
                {gecmis.map((satir) => (
                  <li
                    key={satir.anahtar}
                    className="flex items-start gap-base rounded-md border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-base"
                  >
                    <span
                      className={`mt-[2px] shrink-0 ${satir.kabul ? 'text-primary' : 'text-error'}`}
                    >
                      {satir.kabul ? (
                        <OnayIkonu className="h-4 w-4" etiket="Kabul" />
                      ) : (
                        <CarpiIkonu className="h-4 w-4" etiket="Ret" />
                      )}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-mono text-body-sm text-on-surface">
                        {satir.baslik}
                      </span>
                      <span className="block truncate font-body text-body-sm text-on-surface-variant">
                        {satir.ayrinti}
                      </span>
                    </span>
                    <span className="shrink-0 font-body text-[11px] tabular-nums text-on-surface-variant">
                      {saatBicimi.format(satir.an)}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </aside>
        </div>
      </div>
    </main>
  );
}

function SonucPaneli({
  sonuc,
  hata,
  bekliyor,
}: {
  sonuc: BiletOkutmaSonucu | null;
  hata: string | null;
  bekliyor: boolean;
}) {
  if (bekliyor) {
    return (
      <div
        role="status"
        className="mt-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-md py-stack-md font-body text-body-md text-on-surface-variant"
      >
        Kontrol ediliyor...
      </div>
    );
  }

  if (hata) {
    return (
      <Panel kabul={false} baslik="Giriş yok">
        <p className="font-body text-body-md text-on-surface">{hata}</p>
      </Panel>
    );
  }

  if (!sonuc) {
    return (
      <div className="mt-stack-md rounded-lg border border-dashed border-outline-variant/60 px-stack-md py-stack-md text-center font-body text-body-sm text-on-surface-variant">
        Bilet bekleniyor
      </div>
    );
  }

  return (
    <Panel kabul={sonuc.admitted} baslik={sonuc.admitted ? 'Geçebilir' : 'Giriş yok'}>
      <p className="font-headline text-title-lg text-on-surface">{sonuc.eventTitle}</p>
      <p className="font-body text-body-md text-on-surface">
        {sonuc.seatLabel} · {sonuc.ticketTypeName}
      </p>
      <p className="mt-base font-mono text-body-sm text-on-surface-variant">
        {sonuc.ticketNumber}
      </p>

      {!sonuc.admitted && (
        <p className="mt-stack-sm font-body text-body-md text-on-surface">
          {retMetni(sonuc)}
          {sonuc.checkedInAtUtc && (
            <span className="text-on-surface-variant">
              {' '}
              ({saatBicimi.format(new Date(sonuc.checkedInAtUtc))})
            </span>
          )}
        </p>
      )}
    </Panel>
  );
}

function Panel({
  kabul,
  baslik,
  children,
}: {
  kabul: boolean;
  baslik: string;
  children: React.ReactNode;
}) {
  return (
    <div
      role="status"
      aria-live="assertive"
      className={`mt-stack-md overflow-hidden rounded-lg border ${
        kabul ? 'border-primary/50 bg-primary-container/15' : 'border-error/50 bg-error-container/20'
      }`}
    >
      <div
        className={`flex items-center gap-stack-sm px-stack-md py-stack-sm ${
          kabul ? 'bg-primary/20 text-primary' : 'bg-error/20 text-error'
        }`}
      >
        {/* Ikon tek basina anlam tasimiyor: yanindaki baslik ayni seyi
            soyluyor. Karar yalnizca renkle verilseydi renk korlugu olan
            gorevli yesil ile kirmiziyi ayirt edemezdi. */}
        {kabul ? (
          <OnayIkonu className="h-6 w-6" />
        ) : (
          <CarpiIkonu className="h-6 w-6" />
        )}
        <span className="font-display text-headline-md uppercase tracking-wide">{baslik}</span>
      </div>

      <div className="space-y-1 px-stack-md py-stack-sm">{children}</div>
    </div>
  );
}
