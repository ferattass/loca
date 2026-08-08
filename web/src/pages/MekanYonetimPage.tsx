import { useEffect, useRef, useState } from 'react';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { dogrulamaHatalari, hataMesaji } from '../api/client';
import { sehirleriGetir, type Sehir } from '../api/catalog';
import {
  mekanDetayiGetir,
  mekanGuncelle,
  mekanListesiGetir,
  mekanOlustur,
  mekanSil,
  salonEkle,
  salonSil,
  type MekanDetayi,
} from '../api/venues';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { Secim } from '../components/ui/Secim';

const SAYFA_BOYUTU = 10;

/**
 * Yonetici paneli: mekan ve salon yonetimi.
 *
 * Iki sutunlu duzen — solda filtreli mekan listesi ve yeni mekan formu,
 * sagda (mobilde altta) secili mekanin duzenleme formu ve salon listesi.
 * Silme islemleri (hem mekan hem salon) tek tiklamayla tetiklenmiyor;
 * oturma plani ekraninda oldugu gibi once onay isteniyor, cunku sunucu
 * tarafinda geri alinmasi mumkun degil.
 */
export function MekanYonetimPage() {
  const [sehirId, setSehirId] = useState('');
  const [aramaTaslak, setAramaTaslak] = useState('');
  const [arama, setArama] = useState('');
  const [sayfaNo, setSayfaNo] = useState(1);
  const [seciliMekanId, setSeciliMekanId] = useState<string | null>(null);

  const sehirler = useQuery({ queryKey: ['cities'], queryFn: sehirleriGetir });

  const mekanlar = useQuery({
    queryKey: ['mekan-listesi', sehirId, arama, sayfaNo],
    queryFn: () => mekanListesiGetir({ sehirId, arama, sayfaNo, sayfaBoyutu: SAYFA_BOYUTU }),
    // Sayfa degistiginde onceki liste ekranda kalir; aksi halde her sayfa
    // gecisinde bos bir iskelet gorunur ve liste "zipliyor" gibi hissettirir.
    placeholderData: keepPreviousData,
  });

  const toplamSayfa = Math.max(1, Math.ceil((mekanlar.data?.totalCount ?? 0) / SAYFA_BOYUTU));

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-6xl">
        <h1 className="mb-stack-md font-headline text-headline-md text-on-surface">
          Mekân ve salon yönetimi
        </h1>

        <div className="grid gap-stack-lg md:grid-cols-2">
          <section className="space-y-stack-sm">
            <div className="flex flex-wrap items-end gap-stack-sm">
              <Secim
                etiket="Şehir"
                deger={sehirId}
                gerekli={false}
                bosMetin="Tüm şehirler"
                onDegis={(deger) => {
                  setSehirId(deger);
                  setSayfaNo(1);
                }}
                secenekler={(sehirler.data ?? []).map((s) => ({ id: s.id, ad: s.name }))}
              />

              <form
                className="flex flex-1 items-end gap-stack-sm"
                onSubmit={(olay) => {
                  olay.preventDefault();
                  setArama(aramaTaslak);
                  setSayfaNo(1);
                }}
              >
                <div className="flex-1">
                  <TextField
                    etiket="Mekân ara"
                    value={aramaTaslak}
                    placeholder="Mekân adıyla ara"
                    onChange={(olay) => setAramaTaslak(olay.target.value)}
                  />
                </div>
                <Button type="submit" gorunum="cizgili">
                  Ara
                </Button>
              </form>
            </div>

            {mekanlar.isPending && (
              <div className="animate-pulse space-y-base" aria-hidden="true">
                <div className="h-16 rounded-md bg-surface-variant/40" />
                <div className="h-16 rounded-md bg-surface-variant/40" />
                <div className="h-16 rounded-md bg-surface-variant/40" />
                <span className="sr-only" role="status">
                  Mekânlar yükleniyor
                </span>
              </div>
            )}

            {mekanlar.isError && (
              <p
                role="alert"
                className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
              >
                {hataMesaji(mekanlar.error, 'Mekânlar yüklenemedi.')}
              </p>
            )}

            {mekanlar.data && mekanlar.data.items.length === 0 && (
              <p className="rounded-md border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-stack-md font-body text-body-sm text-on-surface-variant">
                Kayıtlı mekân bulunamadı.
              </p>
            )}

            {mekanlar.data && mekanlar.data.items.length > 0 && (
              <>
                <ul className="space-y-base">
                  {mekanlar.data.items.map((mekan) => {
                    const secili = mekan.id === seciliMekanId;

                    return (
                      <li key={mekan.id}>
                        <button
                          type="button"
                          aria-pressed={secili}
                          onClick={() => setSeciliMekanId(mekan.id)}
                          className={`w-full rounded-md border px-stack-sm py-base text-left transition-colors ${
                            secili
                              ? 'border-primary bg-surface-variant/40'
                              : 'border-outline-variant/40 bg-surface-variant/10 hover:border-outline'
                          }`}
                        >
                          <div className="flex items-center justify-between gap-stack-sm">
                            <span className="font-body text-body-md text-on-surface">
                              {mekan.name}
                            </span>
                            <span
                              className={`font-body text-body-sm ${
                                mekan.isActive ? 'text-primary' : 'text-on-surface-variant/60'
                              }`}
                            >
                              {mekan.isActive ? 'Aktif' : 'Pasif'}
                            </span>
                          </div>
                          <p className="font-body text-body-sm text-on-surface-variant">
                            {mekan.cityName} · {mekan.hallCount} salon
                          </p>
                        </button>
                      </li>
                    );
                  })}
                </ul>

                <div className="flex items-center justify-between gap-stack-sm">
                  <Button
                    type="button"
                    gorunum="sade"
                    disabled={sayfaNo <= 1}
                    onClick={() => setSayfaNo((s) => s - 1)}
                  >
                    Önceki
                  </Button>
                  <span className="font-body text-body-sm text-on-surface-variant">
                    Sayfa {sayfaNo} / {toplamSayfa}
                  </span>
                  <Button
                    type="button"
                    gorunum="sade"
                    disabled={sayfaNo >= toplamSayfa}
                    onClick={() => setSayfaNo((s) => s + 1)}
                  >
                    Sonraki
                  </Button>
                </div>
              </>
            )}

            <YeniMekanFormu
              sehirler={sehirler.data ?? []}
              onOlusturuldu={(id) => setSeciliMekanId(id)}
            />
          </section>

          <section>
            {seciliMekanId ? (
              <MekanDetayPaneli
                key={seciliMekanId}
                mekanId={seciliMekanId}
                onKapat={() => setSeciliMekanId(null)}
                onSilindi={() => setSeciliMekanId(null)}
              />
            ) : (
              <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/10 px-stack-sm py-stack-lg text-center font-body text-body-sm text-on-surface-variant">
                Detaylarını görmek için soldaki listeden bir mekân seç.
              </p>
            )}
          </section>
        </div>
      </div>
    </main>
  );
}

// --- Ortak yardimcilar ------------------------------------------------------

interface HataYapisi {
  ozet: string;
  liste: string[];
}

function hataYapisiOlustur(hata: unknown, varsayilan: string): HataYapisi {
  return { ozet: hataMesaji(hata, varsayilan), liste: dogrulamaHatalari(hata) };
}

function HataKutusu({ hata }: { hata: HataYapisi | null }) {
  if (!hata) return null;

  return (
    <div
      role="alert"
      className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
    >
      <p>{hata.ozet}</p>
      {/* Tek mesaj zaten ozette gorunuyor; liste yalnizca birden fazla alan
          hatali oldugunda ek bilgi tasir. */}
      {hata.liste.length > 1 && (
        <ul className="mt-base list-disc space-y-[2px] pl-stack-md">
          {hata.liste.map((mesaj) => (
            <li key={mesaj}>{mesaj}</li>
          ))}
        </ul>
      )}
    </div>
  );
}


/**
 * Silme dugmesi — tek tiklamayla islem tetiklemez.
 *
 * Ilk tiklama yalnizca onay metnini acar; gercek istek ikinci tiklamada
 * gider. Istek bittiginde (basarili ya da basarisiz) onay kapanir ki
 * kullanici basarisiz bir denemeden sonra kutuda takili kalmasin.
 */
function SilmeButonu({
  etiket,
  onayMetni,
  yukleniyor,
  onOnayla,
}: {
  etiket: string;
  onayMetni: string;
  yukleniyor: boolean;
  onOnayla: () => void;
}) {
  const [onayBekliyor, setOnayBekliyor] = useState(false);
  const oncekiYukleniyor = useRef(yukleniyor);

  useEffect(() => {
    if (oncekiYukleniyor.current && !yukleniyor) {
      setOnayBekliyor(false);
    }

    oncekiYukleniyor.current = yukleniyor;
  }, [yukleniyor]);

  if (onayBekliyor) {
    return (
      <div className="flex flex-wrap items-center gap-stack-sm">
        <span className="font-body text-body-sm text-on-surface-variant">{onayMetni}</span>
        <button
          type="button"
          onClick={onOnayla}
          disabled={yukleniyor}
          className="rounded-md bg-error px-stack-sm py-base font-body text-body-sm font-semibold text-on-error disabled:opacity-60"
        >
          {yukleniyor ? 'Siliniyor' : 'Evet, sil'}
        </button>
        <button
          type="button"
          onClick={() => setOnayBekliyor(false)}
          className="font-body text-body-sm text-primary underline underline-offset-2"
        >
          Vazgeç
        </button>
      </div>
    );
  }

  return (
    <button
      type="button"
      onClick={() => setOnayBekliyor(true)}
      className="font-body text-body-sm text-error underline underline-offset-2"
    >
      {etiket}
    </button>
  );
}

// --- Yeni mekan ekleme -------------------------------------------------------

function YeniMekanFormu({
  sehirler,
  onOlusturuldu,
}: {
  sehirler: Sehir[];
  onOlusturuldu: (id: string) => void;
}) {
  const queryClient = useQueryClient();
  const [sehirId, setSehirId] = useState('');
  const [ad, setAd] = useState('');
  const [adres, setAdres] = useState('');
  const [aciklama, setAciklama] = useState('');
  const [telefon, setTelefon] = useState('');
  const [hata, setHata] = useState<HataYapisi | null>(null);

  const olustur = useMutation({
    mutationFn: () =>
      mekanOlustur({
        cityId: sehirId,
        name: ad,
        address: adres,
        description: aciklama,
        phoneNumber: telefon,
      }),
    onSuccess: async (id) => {
      setSehirId('');
      setAd('');
      setAdres('');
      setAciklama('');
      setTelefon('');
      setHata(null);
      await queryClient.invalidateQueries({ queryKey: ['mekan-listesi'] });
      onOlusturuldu(id);
    },
    onError: (h) => setHata(hataYapisiOlustur(h, 'Mekân oluşturulamadı.')),
  });

  return (
    <form
      className="space-y-stack-sm rounded-lg border border-outline-variant/40 bg-surface-variant/10 p-stack-sm"
      onSubmit={(olay) => {
        olay.preventDefault();
        setHata(null);
        olustur.mutate();
      }}
    >
      <h2 className="font-body text-body-md font-semibold text-on-surface">Yeni mekân ekle</h2>

      <HataKutusu hata={hata} />

      <Secim
        etiket="Şehir"
        deger={sehirId}
        onDegis={setSehirId}
        secenekler={sehirler.map((s) => ({ id: s.id, ad: s.name }))}
      />
      <TextField etiket="Mekân adı" value={ad} required onChange={(o) => setAd(o.target.value)} />
      <TextField
        etiket="Adres"
        value={adres}
        required
        onChange={(o) => setAdres(o.target.value)}
      />
      <TextField
        etiket="Açıklama"
        value={aciklama}
        onChange={(o) => setAciklama(o.target.value)}
      />
      <TextField
        etiket="Telefon"
        value={telefon}
        onChange={(o) => setTelefon(o.target.value)}
      />

      <Button type="submit" yukleniyor={olustur.isPending}>
        Mekânı oluştur
      </Button>
    </form>
  );
}

// --- Secili mekanin detayi ---------------------------------------------------

function MekanDetayPaneli({
  mekanId,
  onKapat,
  onSilindi,
}: {
  mekanId: string;
  onKapat: () => void;
  onSilindi: () => void;
}) {
  const detay = useQuery({
    queryKey: ['mekan-detay', mekanId],
    queryFn: () => mekanDetayiGetir(mekanId),
  });

  if (detay.isPending) {
    return (
      <div
        className="animate-pulse space-y-stack-sm rounded-lg border border-outline-variant/40 p-stack-sm"
        aria-hidden="true"
      >
        <div className="h-6 w-40 rounded bg-surface-variant/60" />
        <div className="h-40 rounded bg-surface-variant/40" />
        <span className="sr-only" role="status">
          Mekân detayı yükleniyor
        </span>
      </div>
    );
  }

  if (detay.isError) {
    return (
      <p
        role="alert"
        className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
      >
        {hataMesaji(detay.error, 'Mekân detayı yüklenemedi.')}
      </p>
    );
  }

  return <MekanDetayIcerigi mekan={detay.data} onKapat={onKapat} onSilindi={onSilindi} />;
}

function MekanDetayIcerigi({
  mekan,
  onKapat,
  onSilindi,
}: {
  mekan: MekanDetayi;
  onKapat: () => void;
  onSilindi: () => void;
}) {
  const queryClient = useQueryClient();

  // Form alanlari sunucu verisinden yalnizca ilk cizimde baslatiliyor.
  // Salon eklenip silindiginde bu bilesen yeniden monte olmuyor (parent
  // `mekanId` degismedigi surece key sabit) — kullanicinin duzenlemekte
  // oldugu ad/adres metni, salon listesindeki bir degisiklik yuzunden
  // ustune yazilmiyor.
  const [ad, setAd] = useState(mekan.name);
  const [adres, setAdres] = useState(mekan.address);
  const [aciklama, setAciklama] = useState(mekan.description ?? '');
  const [telefon, setTelefon] = useState(mekan.phoneNumber ?? '');
  const [duzenleHata, setDuzenleHata] = useState<HataYapisi | null>(null);
  const [silmeHata, setSilmeHata] = useState<string | null>(null);
  const [salonHata, setSalonHata] = useState<HataYapisi | null>(null);
  const [salonSilmeHatasi, setSalonSilmeHatasi] = useState<string | null>(null);
  const [salonAd, setSalonAd] = useState('');
  const [salonKapasite, setSalonKapasite] = useState('');

  const ortakInvalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['mekan-detay', mekan.id] }),
      queryClient.invalidateQueries({ queryKey: ['mekan-listesi'] }),
    ]);

  const guncelle = useMutation({
    mutationFn: () =>
      mekanGuncelle(mekan.id, {
        name: ad,
        address: adres,
        description: aciklama,
        phoneNumber: telefon,
      }),
    onSuccess: async () => {
      setDuzenleHata(null);
      await ortakInvalidate();
    },
    onError: (h) => setDuzenleHata(hataYapisiOlustur(h, 'Mekân güncellenemedi.')),
  });

  const mekaniSil = useMutation({
    mutationFn: () => mekanSil(mekan.id),
    onSuccess: async () => {
      setSilmeHata(null);
      await queryClient.invalidateQueries({ queryKey: ['mekan-listesi'] });
      onSilindi();
    },
    // 409 burada en olasi hata: salonu olan bir mekan silinemez. Sunucunun
    // dondugu detay mesaji dogrudan gosteriliyor.
    onError: (h) => setSilmeHata(hataMesaji(h, 'Mekân silinemedi.')),
  });

  const salonEkleMutasyonu = useMutation({
    mutationFn: () => salonEkle(mekan.id, { name: salonAd, capacity: Number(salonKapasite) }),
    onSuccess: async () => {
      setSalonAd('');
      setSalonKapasite('');
      setSalonHata(null);
      await ortakInvalidate();
    },
    onError: (h) => setSalonHata(hataYapisiOlustur(h, 'Salon eklenemedi.')),
  });

  const salonSilMutasyonu = useMutation({
    mutationFn: (salonId: string) => salonSil(salonId),
    onSuccess: async () => {
      setSalonSilmeHatasi(null);
      await ortakInvalidate();
    },
    // 409 burada en olasi hata: bagli oturma plani olan salon silinemez.
    onError: (h) => setSalonSilmeHatasi(hataMesaji(h, 'Salon silinemedi.')),
  });

  return (
    <div className="space-y-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/10 p-stack-sm">
      <div className="flex items-center justify-between gap-stack-sm">
        <h2 className="font-headline text-title-lg text-on-surface">{mekan.name}</h2>
        <button
          type="button"
          onClick={onKapat}
          className="font-body text-body-sm text-primary underline underline-offset-2"
        >
          Listeye dön
        </button>
      </div>

      <form
        className="space-y-stack-sm"
        onSubmit={(olay) => {
          olay.preventDefault();
          setDuzenleHata(null);
          guncelle.mutate();
        }}
      >
        <HataKutusu hata={duzenleHata} />

        <TextField etiket="Mekân adı" value={ad} required onChange={(o) => setAd(o.target.value)} />
        <TextField
          etiket="Adres"
          value={adres}
          required
          onChange={(o) => setAdres(o.target.value)}
        />
        <TextField
          etiket="Açıklama"
          value={aciklama}
          onChange={(o) => setAciklama(o.target.value)}
        />
        <TextField
          etiket="Telefon"
          value={telefon}
          onChange={(o) => setTelefon(o.target.value)}
        />

        <div className="flex flex-wrap items-center gap-stack-sm">
          <Button type="submit" yukleniyor={guncelle.isPending}>
            Değişiklikleri kaydet
          </Button>

          <SilmeButonu
            etiket="Mekânı sil"
            onayMetni={`"${mekan.name}" silinsin mi? Bu işlem geri alınamaz.`}
            yukleniyor={mekaniSil.isPending}
            onOnayla={() => mekaniSil.mutate()}
          />
        </div>

        {silmeHata && (
          <p role="alert" className="font-body text-body-sm text-error">
            {silmeHata}
          </p>
        )}
      </form>

      <div className="space-y-stack-sm border-t border-outline-variant/30 pt-stack-sm">
        <h3 className="font-body text-body-md font-semibold text-on-surface">
          Salonlar ({mekan.halls.length})
        </h3>

        {mekan.halls.length === 0 ? (
          <p className="font-body text-body-sm text-on-surface-variant">
            Henüz salon eklenmemiş.
          </p>
        ) : (
          <ul className="space-y-base">
            {mekan.halls.map((salon) => (
              <li
                key={salon.id}
                className="flex flex-wrap items-center justify-between gap-stack-sm rounded-md border border-outline-variant/30 bg-surface-variant/10 px-stack-sm py-base"
              >
                <div>
                  <p className="font-body text-body-sm text-on-surface">{salon.name}</p>
                  <p className="font-body text-body-sm text-on-surface-variant">
                    {salon.capacity} kişilik · {salon.isActive ? 'Aktif' : 'Pasif'}
                  </p>
                </div>

                <SilmeButonu
                  etiket="Sil"
                  onayMetni={`"${salon.name}" salonu silinsin mi?`}
                  yukleniyor={salonSilMutasyonu.isPending && salonSilMutasyonu.variables === salon.id}
                  onOnayla={() => salonSilMutasyonu.mutate(salon.id)}
                />
              </li>
            ))}
          </ul>
        )}

        {salonSilmeHatasi && (
          <p role="alert" className="font-body text-body-sm text-error">
            {salonSilmeHatasi}
          </p>
        )}

        <form
          className="space-y-stack-sm rounded-md border border-outline-variant/30 p-stack-sm"
          onSubmit={(olay) => {
            olay.preventDefault();
            setSalonHata(null);
            salonEkleMutasyonu.mutate();
          }}
        >
          <HataKutusu hata={salonHata} />

          <div className="grid gap-stack-sm md:grid-cols-2">
            <TextField
              etiket="Salon adı"
              value={salonAd}
              required
              onChange={(o) => setSalonAd(o.target.value)}
            />
            <TextField
              etiket="Kapasite"
              type="number"
              min={1}
              value={salonKapasite}
              required
              onChange={(o) => setSalonKapasite(o.target.value)}
            />
          </div>

          <Button type="submit" gorunum="cizgili" yukleniyor={salonEkleMutasyonu.isPending}>
            Salon ekle
          </Button>
        </form>
      </div>
    </div>
  );
}
