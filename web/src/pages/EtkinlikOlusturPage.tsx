import { useCallback, useState } from 'react';
import { Link } from 'react-router-dom';

import { dogrulamaHatalari, hataMesaji } from '../api/client';
import { OnayIkonu } from '../components/ui/Ikon';
import { AfisAdimi } from './etkinlik-olustur/AfisAdimi';
import { BelgeAdimi } from './etkinlik-olustur/BelgeAdimi';
import { BiletTuruAdimi } from './etkinlik-olustur/BiletTuruAdimi';
import { EtkinlikAdimi } from './etkinlik-olustur/EtkinlikAdimi';
import { OnayAdimi } from './etkinlik-olustur/OnayAdimi';
import { OturumAdimi } from './etkinlik-olustur/OturumAdimi';

/**
 * Etkinlik oluşturma sihirbazı.
 *
 * Beş adım: etkinlik → oturum → bilet türü → afiş → onaya gönder.
 *
 * <b>Etkinlik ilk adımın sonunda gerçekten oluşturuluyor</b>, tek seferde
 * hepsi birden değil. Sebep sunucunun kendi kuralları: oturum ve bilet türü
 * uçları var olan bir etkinliğe bağlanıyor, afiş bir etkinliğe iliştiriliyor
 * ve onaya gönderme yayın ön koşullarını (oturum + aktif bilet türü + afiş)
 * kontrol ediyor. Yarıda bırakılan iş taslak olarak kalıyor — Draft zaten
 * domainde gerçek bir durum, yapay bir yarım kayıt değil.
 */
export function EtkinlikOlusturPage() {
  const [adim, setAdim] = useState(1);
  const [gonderildi, setGonderildi] = useState(false);
  const [etkinlikId, setEtkinlikId] = useState<string | null>(null);
  const [salonId, setSalonId] = useState('');
  const [oturumlar, setOturumlar] = useState<Array<{ id: string; baslangic: string }>>([]);
  const [biletTurleri, setBiletTurleri] = useState<Array<{ id: string; ad: string; fiyat: number }>>([]);
  const [planId, setPlanId] = useState('');
  const [afisId, setAfisId] = useState<string | null>(null);
  const [belgeSayisi, setBelgeSayisi] = useState(0);
  const [hata, setHata] = useState<string | null>(null);
  const [hatalar, setHatalar] = useState<string[]>([]);

  // Cocuk bilesenler ham hatayi geciriyor, metne cevirme burada yapiliyor.
  // Once her bileşen kendi mesajini uretiyordu; o zaman ALAN BAZLI dogrulama
  // hatalarina erisilemiyordu, cunku metne cevrilmis hatanin icinde liste
  // kalmiyor. Kullanici da dort alan birden hataliyken yalnizca birini
  // goruyordu.
  const bildirHata = useCallback((gelen: unknown, varsayilan?: string) => {
    if (gelen === null) {
      setHata(null);
      setHatalar([]);
      return;
    }

    setHata(hataMesaji(gelen, varsayilan));
    setHatalar(dogrulamaHatalari(gelen));
  }, []);

  const adimlar = ['Etkinlik', 'Oturumlar', 'Bilet türleri', 'Afiş', 'Belgeler', 'Onay'];

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-3xl">
        <h1 className="mb-stack-sm font-headline text-headline-md text-on-surface">
          Yeni etkinlik
        </h1>

        <ol className="mb-stack-md flex flex-wrap gap-stack-sm" aria-label="Adımlar">
          {adimlar.map((ad, sira) => {
            const numara = sira + 1;
            const durum = numara < adim ? 'tamam' : numara === adim ? 'aktif' : 'bekliyor';

            return (
              <li
                key={ad}
                aria-current={durum === 'aktif' ? 'step' : undefined}
                className={`font-body text-body-sm ${
                  durum === 'aktif'
                    ? 'text-primary font-semibold'
                    : durum === 'tamam'
                      ? 'text-on-surface-variant'
                      : 'text-on-surface-variant/50'
                }`}
              >
                {numara}. {ad}
                {durum === 'tamam' && (
                  <OnayIkonu className="ml-base inline-block h-3.5 w-3.5 align-[-2px]" />
                )}
              </li>
            );
          })}
        </ol>

        {hata && (
          <div
            role="alert"
            className="mb-stack-sm rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
          >
            <p>{hata}</p>

            {/*
              Sunucu alan bazli hata dondurduğunde hepsi listeleniyor.
              Tek satir gosterilseydi kullanici hatalari tek tek keşfetmek
              icin formu birkac kez gondermek zorunda kalirdi.
            */}
            {hatalar.length > 1 && (
              <ul className="mt-base list-disc space-y-[2px] pl-stack-md">
                {hatalar.map((satir) => (
                  <li key={satir}>{satir}</li>
                ))}
              </ul>
            )}
          </div>
        )}

        {adim === 1 && (
          <EtkinlikAdimi
            onHata={bildirHata}
            onTamam={(id, secilenSalon) => {
              setEtkinlikId(id);
              setSalonId(secilenSalon);
              bildirHata(null);
              setAdim(2);
            }}
          />
        )}

        {adim === 2 && etkinlikId && (
          <OturumAdimi
            etkinlikId={etkinlikId}
            salonId={salonId}
            oturumlar={oturumlar}
            onHata={bildirHata}
            onEklendi={(kayit, secilenPlan) => {
              setOturumlar((eskiler) => [...eskiler, kayit]);
              setPlanId(secilenPlan);
              bildirHata(null);
            }}
            onDevam={() => {
              bildirHata(null);
              setAdim(3);
            }}
          />
        )}

        {adim === 3 && etkinlikId && (
          <BiletTuruAdimi
            etkinlikId={etkinlikId}
            planId={planId}
            turler={biletTurleri}
            onHata={bildirHata}
            onEklendi={(kayit) => {
              setBiletTurleri((eskiler) => [...eskiler, kayit]);
              bildirHata(null);
            }}
            onDevam={() => {
              bildirHata(null);
              setAdim(4);
            }}
          />
        )}

        {adim === 4 && etkinlikId && (
          <AfisAdimi
            etkinlikId={etkinlikId}
            afisId={afisId}
            onHata={bildirHata}
            onYuklendi={(id) => {
              setAfisId(id);
              bildirHata(null);
            }}
            onDevam={() => {
              bildirHata(null);
              setAdim(5);
            }}
          />
        )}

        {adim === 5 && etkinlikId && (
          <BelgeAdimi
            etkinlikId={etkinlikId}
            onHata={bildirHata}
            onDegisti={setBelgeSayisi}
            onDevam={() => {
              bildirHata(null);
              setAdim(6);
            }}
          />
        )}

        {adim === 6 && etkinlikId && !gonderildi && (
          <OnayAdimi
            etkinlikId={etkinlikId}
            oturumSayisi={oturumlar.length}
            turSayisi={biletTurleri.length}
            afisVar={afisId !== null}
            belgeSayisi={belgeSayisi}
            onHata={bildirHata}
            onGonderildi={() => {
              bildirHata(null);
              setGonderildi(true);
            }}
          />
        )}

        {gonderildi && (
          <div
            role="status"
            className="space-y-stack-sm rounded-lg border border-primary/40 bg-surface-variant/20 p-stack-md"
          >
            <p className="font-body text-body-md text-on-surface">
              Etkinlik onaya gönderildi. Yönetici yayına aldığında satılabilir koltuklar
              üretilir ve biletler satışa açılır.
            </p>
            <Link
              to="/"
              className="font-body text-body-sm text-primary underline underline-offset-2"
            >
              Ana sayfaya dön
            </Link>
          </div>
        )}
      </div>
    </main>
  );
}
