import { useCallback, useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { dogrulamaHatalari, dosyaAdresi, hataMesaji } from '../api/client';
import {
  mekanlariGetir,
  planlariGetir,
  salonDolulukGetir,
  salonlariGetir,
  sehirleriGetir,
  type SalonDoluluk,
} from '../api/catalog';
import {
  afisBagla,
  biletTuruEkle,
  etkinlikOlustur,
  gorselYukle,
  kategorileriGetir,
  onayaGonder,
  oturumEkle,
} from '../api/events';
import {
  BELGE_TURU_METNI,
  belgeBagla,
  belgeSil,
  belgeYukle,
  etkinlikBelgeleriniGetir,
  type BelgeTuru,
} from '../api/onay';
import { planGetir } from '../api/seatLayouts';
import { Button } from '../components/ui/Button';
import { CarpiIkonu, OnayIkonu } from '../components/ui/Ikon';
import { TextField } from '../components/ui/TextField';

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

/**
 * Tarayıcının yerel saatini sunucunun beklediği UTC'ye çevirir.
 *
 * `datetime-local` alanı saat dilimi taşımaz; değeri olduğu gibi göndermek,
 * Türkiye'de saat 20:00 diye seçilen etkinliğin sunucuda 20:00 UTC (yani
 * yerel 23:00) olarak kaydedilmesi demek olurdu.
 *
 * <b>Geçersiz tarih burada yakalanıyor.</b> `datetime-local` alanı beş
 * haneli yıl kabul ediyor (tarayıcı "132132" yazılmasına izin veriyor) ve
 * `toISOString()` böyle bir değerde `RangeError` fırlatıyor. Önce bu hata
 * yakalanmıyordu: istek sunucuya hiç gitmeden patlıyor, kullanıcı da
 * sebebini söylemeyen genel bir hata mesajı görüyordu.
 */
function yereldenUtc(deger: string, alan: string): string {
  const tarih = new Date(deger);

  if (Number.isNaN(tarih.getTime())) {
    throw new Error(`"${alan}" geçerli bir tarih değil.`);
  }

  // ECMAScript tarih araligi ±8.64e15 ms (yaklasik ±275760 yil). Disina
  // cikan deger toISOString'de RangeError firlatir.
  const yil = tarih.getUTCFullYear();

  if (yil < 2000 || yil > 2100) {
    throw new Error(`"${alan}" alanındaki yıl (${yil}) makul aralıkta değil. Örnek: 2027.`);
  }

  return tarih.toISOString();
}

/** Cocuk bilesenlerin hata bildirme sozlesmesi. `null` temizler. */
type HataBildir = (hata: unknown, varsayilan?: string) => void;

function Alan({ children }: { children: React.ReactNode }) {
  return <div className="grid gap-stack-sm md:grid-cols-2">{children}</div>;
}

function Secim({
  etiket,
  deger,
  onDegis,
  secenekler,
  bosMetin = 'Seçiniz',
  gerekli = true,
  devreDisi = false,
}: {
  etiket: string;
  deger: string;
  onDegis: (deger: string) => void;
  secenekler: Array<{ id: string; ad: string }>;
  bosMetin?: string;
  gerekli?: boolean;
  devreDisi?: boolean;
}) {
  return (
    <label className="flex flex-col gap-base">
      <span className="font-body text-body-sm text-on-surface-variant">{etiket}</span>
      <select
        value={deger}
        required={gerekli}
        disabled={devreDisi}
        onChange={(olay) => onDegis(olay.target.value)}
        className="w-full rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-stack-sm font-body text-body-md text-on-surface disabled:opacity-50"
      >
        <option value="">{bosMetin}</option>
        {secenekler.map((secenek) => (
          <option key={secenek.id} value={secenek.id}>
            {secenek.ad}
          </option>
        ))}
      </select>
    </label>
  );
}

// --- 1 · Etkinlik ---------------------------------------------------------

function EtkinlikAdimi({
  onTamam,
  onHata,
}: {
  onTamam: (etkinlikId: string, salonId: string) => void;
  onHata: HataBildir;
}) {
  const [kategoriId, setKategoriId] = useState('');
  const [baslik, setBaslik] = useState('');
  const [aciklama, setAciklama] = useState('');
  const [iptalPolitikasi, setIptalPolitikasi] = useState(
    'Etkinlikten 24 saat öncesine kadar tam iade yapılır.',
  );
  const [sehirId, setSehirId] = useState('');
  const [mekanId, setMekanId] = useState('');
  const [salonId, setSalonId] = useState('');
  const [tarih, setTarih] = useState('');
  const [sure, setSure] = useState('120');
  const [satisBas, setSatisBas] = useState('');
  const [satisBit, setSatisBit] = useState('');
  const [yasSiniri, setYasSiniri] = useState('');

  const kategoriler = useQuery({ queryKey: ['event-categories'], queryFn: kategorileriGetir });
  const sehirler = useQuery({ queryKey: ['cities'], queryFn: sehirleriGetir });

  const mekanlar = useQuery({
    queryKey: ['venues', sehirId],
    queryFn: () => mekanlariGetir(sehirId),
    enabled: sehirId !== '',
  });

  const salonlar = useQuery({
    queryKey: ['halls', mekanId],
    queryFn: () => salonlariGetir(mekanId),
    enabled: mekanId !== '',
  });

  const olustur = useMutation({
    mutationFn: () =>
      etkinlikOlustur({
        categoryId: kategoriId,
        title: baslik,
        description: aciklama,
        cancellationPolicy: iptalPolitikasi,
        cityId: sehirId,
        venueId: mekanId,
        hallId: salonId,
        eventDateUtc: yereldenUtc(tarih, 'Etkinlik tarihi ve saati'),
        durationMinutes: Number(sure),
        salesStartsAtUtc: yereldenUtc(satisBas, 'Satış başlangıcı'),
        salesEndsAtUtc: yereldenUtc(satisBit, 'Satış bitişi'),
        minimumAge: yasSiniri === '' ? null : Number(yasSiniri),
      }),
    onSuccess: (id) => onTamam(id, salonId),
    onError: (h) => onHata(h, 'Etkinlik oluşturulamadı.'),
  });

  return (
    <form
      className="space-y-stack-sm"
      onSubmit={(olay) => {
        olay.preventDefault();
        onHata(null);
        olustur.mutate();
      }}
    >
      <Alan>
        <Secim
          etiket="Kategori"
          deger={kategoriId}
          onDegis={setKategoriId}
          secenekler={(kategoriler.data ?? []).map((k) => ({ id: k.id, ad: k.name }))}
        />
        <TextField
          etiket="Başlık"
          value={baslik}
          required
          maxLength={200}
          onChange={(o) => setBaslik(o.target.value)}
        />
      </Alan>

      <TextField
        etiket="Açıklama"
        value={aciklama}
        required
        onChange={(o) => setAciklama(o.target.value)}
      />

      <TextField
        etiket="İptal politikası"
        value={iptalPolitikasi}
        required
        ipucu="Şartname zorunlu alan sayıyor; bilet sayfasında gösterilir."
        onChange={(o) => setIptalPolitikasi(o.target.value)}
      />

      <Alan>
        {/*
          Şehir → mekân → salon zinciri: üstteki değişince alttakiler
          sıfırlanıyor. Sıfırlanmasaydı Ankara'daki bir salon İstanbul
          seçiliyken kayıtlı kalır ve sunucu 400 dönerdi (zincir tutarsızlığı).
        */}
        <Secim
          etiket="Şehir"
          deger={sehirId}
          onDegis={(d) => {
            setSehirId(d);
            setMekanId('');
            setSalonId('');
          }}
          secenekler={(sehirler.data ?? []).map((s) => ({ id: s.id, ad: s.name }))}
        />
        <Secim
          etiket="Mekân"
          deger={mekanId}
          devreDisi={sehirId === ''}
          onDegis={(d) => {
            setMekanId(d);
            setSalonId('');
          }}
          secenekler={(mekanlar.data ?? []).map((m) => ({
            id: m.id,
            ad: `${m.name} (${m.hallCount} salon)`,
          }))}
        />
      </Alan>

      <Secim
        etiket="Salon"
        deger={salonId}
        devreDisi={mekanId === ''}
        onDegis={setSalonId}
        secenekler={(salonlar.data ?? []).map((s) => ({
          id: s.id,
          ad: `${s.name} — ${s.capacity} kişilik`,
        }))}
      />

      <Alan>
        <TextField
          etiket="Etkinlik tarihi ve saati"
          type="datetime-local"
          value={tarih}
          required
          onChange={(o) => setTarih(o.target.value)}
        />
        <TextField
          etiket="Süre (dakika)"
          type="number"
          min={1}
          value={sure}
          required
          onChange={(o) => setSure(o.target.value)}
        />
      </Alan>

      <Alan>
        <TextField
          etiket="Satış başlangıcı"
          type="datetime-local"
          value={satisBas}
          required
          onChange={(o) => setSatisBas(o.target.value)}
        />
        <TextField
          etiket="Satış bitişi"
          type="datetime-local"
          value={satisBit}
          required
          ipucu="Etkinlik başlamadan önce bitmeli."
          onChange={(o) => setSatisBit(o.target.value)}
        />
      </Alan>

      <TextField
        etiket="Yaş sınırı (boş bırakılabilir)"
        type="number"
        min={0}
        value={yasSiniri}
        onChange={(o) => setYasSiniri(o.target.value)}
      />

      <Button type="submit" yukleniyor={olustur.isPending}>
        Taslağı oluştur ve devam et
      </Button>
    </form>
  );
}

// --- 2 · Oturumlar --------------------------------------------------------

function OturumAdimi({
  etkinlikId,
  salonId,
  oturumlar,
  onEklendi,
  onDevam,
  onHata,
}: {
  etkinlikId: string;
  salonId: string;
  oturumlar: Array<{ id: string; baslangic: string }>;
  onEklendi: (kayit: { id: string; baslangic: string }, planId: string) => void;
  onDevam: () => void;
  onHata: HataBildir;
}) {
  const [planId, setPlanId] = useState('');
  const [bas, setBas] = useState('');
  const [bit, setBit] = useState('');
  const [satisBas, setSatisBas] = useState('');
  const [satisBit, setSatisBit] = useState('');

  const planlar = useQuery({
    queryKey: ['seat-layouts', salonId],
    queryFn: () => planlariGetir(salonId),
    enabled: salonId !== '',
  });

  /**
   * Salon doluluk sorgusu.
   *
   * Tarihler ISO'ya cevrilebiliyorsa soruluyor. Cevrilemiyorsa (yarim
   * yazilmis bir datetime alani) sorgu hic acilmiyor: her tus vurusunda
   * sunucuya gecersiz tarih gondermenin anlami yok.
   */
  const aralik = (() => {
    if (bas === '' || bit === '') return null;

    try {
      return { bas: yereldenUtc(bas, 'Başlangıç'), bit: yereldenUtc(bit, 'Bitiş') };
    } catch {
      return null;
    }
  })();

  const doluluk = useQuery({
    queryKey: ['hall-availability', salonId, aralik?.bas, aralik?.bit, etkinlikId],
    queryFn: () =>
      salonDolulukGetir(
        salonId,
        aralik!.bas,
        aralik!.bit,
        // Etkinligin KENDI oturumlari cakisma sayilmiyor: ikinci oturumu
        // eklerken birincisi "dolu" diye isaretlenseydi cok oturumlu
        // etkinlik hic kurulamazdi. Ayni etkinlik icindeki cakismayi
        // sunucu Event.AddSession'da ayrica yakaliyor.
        etkinlikId,
      ),
    enabled: salonId !== '' && aralik !== null && new Date(aralik.bit) > new Date(aralik.bas),
  });

  const ekle = useMutation({
    mutationFn: () =>
      oturumEkle(etkinlikId, {
        hallId: salonId,
        seatLayoutId: planId,
        startsAtUtc: yereldenUtc(bas, 'Başlangıç'),
        endsAtUtc: yereldenUtc(bit, 'Bitiş'),
        salesStartsAtUtc: yereldenUtc(satisBas, 'Satış başlangıcı'),
        salesEndsAtUtc: yereldenUtc(satisBit, 'Satış bitişi'),
      }),
    onSuccess: (id) => {
      onEklendi({ id, baslangic: bas }, planId);
      setBas('');
      setBit('');
    },
    onError: (h) => onHata(h, 'Oturum eklenemedi.'),
  });

  return (
    <div className="space-y-stack-sm">
      <p className="font-body text-body-sm text-on-surface-variant">
        En az bir oturum gerekli. Aynı salondaki oturumlar arasında en az bir saat
        temizlik payı bırakılmalı.
      </p>

      {oturumlar.length > 0 && (
        <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
          {oturumlar.map((oturum, sira) => (
            <li key={oturum.id} className="font-body text-body-sm text-on-surface">
              {sira + 1}. oturum — {new Date(oturum.baslangic).toLocaleString('tr-TR')}
            </li>
          ))}
        </ul>
      )}

      <form
        className="space-y-stack-sm"
        onSubmit={(olay) => {
          olay.preventDefault();
          onHata(null);
          ekle.mutate();
        }}
      >
        <Secim
          etiket="Oturma planı"
          deger={planId}
          onDegis={setPlanId}
          secenekler={(planlar.data ?? []).map((p) => ({
            id: p.id,
            ad: `${p.name} (${p.sectionCount} bölüm)`,
          }))}
        />

        <Alan>
          <TextField
            etiket="Başlangıç"
            type="datetime-local"
            value={bas}
            required
            onChange={(o) => setBas(o.target.value)}
          />
          <TextField
            etiket="Bitiş"
            type="datetime-local"
            value={bit}
            required
            onChange={(o) => setBit(o.target.value)}
          />
        </Alan>

        <Alan>
          <TextField
            etiket="Satış başlangıcı"
            type="datetime-local"
            value={satisBas}
            required
            onChange={(o) => setSatisBas(o.target.value)}
          />
          <TextField
            etiket="Satış bitişi"
            type="datetime-local"
            value={satisBit}
            required
            onChange={(o) => setSatisBit(o.target.value)}
          />
        </Alan>

        <SalonDolulukRozeti
          sorgu={doluluk}
          gecerliAralik={aralik !== null}
        />

        <div className="flex flex-wrap gap-stack-sm">
          {/* Dolu salonda dugme KAPALI. Acik biraksaydik sunucu zaten 409
              donerdi ama kullanici formu gonderip hata ekrani gormek yerine
              tarihi degistirmeli; kural ekranda okunuyor. */}
          <Button
            type="submit"
            gorunum="cizgili"
            yukleniyor={ekle.isPending}
            disabled={doluluk.data?.isAvailable === false}
          >
            Oturumu ekle
          </Button>
          <Button type="button" onClick={onDevam} disabled={oturumlar.length === 0}>
            Devam et
          </Button>
        </div>
      </form>
    </div>
  );
}

/**
 * Salonun secilen saatte dolu olup olmadigi.
 *
 * <b>Uc durum, uc gorunum:</b> henuz tarih girilmemis (hicbir sey yazma),
 * musait (yesil), dolu (kirmizi + cakisan oturumlar). Iki duruma
 * indirgenseydi tarih girilmeden once "musait" yazardi ve bu bir yalan
 * olurdu — hicbir sey sorulmamisti.
 */
function SalonDolulukRozeti({
  sorgu,
  gecerliAralik,
}: {
  sorgu: {
    data?: SalonDoluluk;
    isFetching: boolean;
    isError: boolean;
  };
  gecerliAralik: boolean;
}) {
  if (!gecerliAralik) return null;

  if (sorgu.isFetching && !sorgu.data) {
    return (
      <p className="font-body text-body-sm text-on-surface-variant" role="status">
        Salon müsaitliği kontrol ediliyor…
      </p>
    );
  }

  if (sorgu.isError) {
    return (
      <p className="font-body text-body-sm text-on-surface-variant">
        Salon müsaitliği kontrol edilemedi; kaydederken sunucu yine de kontrol edecek.
      </p>
    );
  }

  if (!sorgu.data) return null;

  if (sorgu.data.isAvailable) {
    return (
      <p
        role="status"
        className="rounded-md border border-primary/40 bg-primary-container/15 px-stack-sm py-base font-body text-body-sm text-primary"
      >
        Salon bu saatlerde müsait.
      </p>
    );
  }

  return (
    <div
      role="status"
      className="rounded-md border border-error/50 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
    >
      <p className="font-semibold">DOLU — bu salonda o saatlerde başka bir oturum var.</p>

      <ul className="mt-base space-y-[2px]">
        {sorgu.data.conflicts.map((cakisan) => (
          <li key={cakisan.eventSessionId}>
            {cakisan.eventTitle} — {new Date(cakisan.startsAtUtc).toLocaleString('tr-TR')} /{' '}
            {new Date(cakisan.endsAtUtc).toLocaleTimeString('tr-TR', {
              hour: '2-digit',
              minute: '2-digit',
            })}
          </li>
        ))}
      </ul>

      <p className="mt-base text-on-surface-variant">
        Oturumlar arasında en az {sorgu.data.cleanupBufferMinutes} dakika temizlik payı
        gerekiyor; bitişik saatler de dolu sayılır.
      </p>
    </div>
  );
}

// --- 3 · Bilet türleri ----------------------------------------------------

function BiletTuruAdimi({
  etkinlikId,
  planId,
  turler,
  onEklendi,
  onDevam,
  onHata,
}: {
  etkinlikId: string;
  planId: string;
  turler: Array<{ id: string; ad: string; fiyat: number }>;
  onEklendi: (kayit: { id: string; ad: string; fiyat: number }) => void;
  onDevam: () => void;
  onHata: HataBildir;
}) {
  const [ad, setAd] = useState('');
  const [fiyat, setFiyat] = useState('');
  const [kontenjan, setKontenjan] = useState('');
  const [satisBas, setSatisBas] = useState('');
  const [satisBit, setSatisBit] = useState('');
  const [belgeIster, setBelgeIster] = useState(false);
  const [bolumId, setBolumId] = useState('');

  const plan = useQuery({
    queryKey: ['seat-layout', planId],
    queryFn: () => planGetir(planId, false),
    enabled: planId !== '',
  });

  const ekle = useMutation({
    mutationFn: () =>
      biletTuruEkle(etkinlikId, {
        name: ad,
        price: Number(fiyat),
        currency: 'TRY',
        quota: Number(kontenjan),
        salesStartsAtUtc: yereldenUtc(satisBas, 'Satış başlangıcı'),
        salesEndsAtUtc: yereldenUtc(satisBit, 'Satış bitişi'),
        requiresVerification: belgeIster,
        seatSectionId: bolumId === '' ? null : bolumId,
      }),
    onSuccess: (id) => {
      onEklendi({ id, ad, fiyat: Number(fiyat) });
      setAd('');
      setFiyat('');
      setKontenjan('');
      setBolumId('');
      setBelgeIster(false);
    },
    onError: (h) => onHata(h, 'Bilet türü eklenemedi.'),
  });

  return (
    <div className="space-y-stack-sm">
      <p className="font-body text-body-sm text-on-surface-variant">
        En az bir aktif bilet türü gerekli. Bölüme atanmamış tür, eşleşmeyen tüm bölümler
        için varsayılan fiyat olur — koltuk üretiminde her koltuğun bir fiyatı olmak
        zorunda, o yüzden en az bir tür bölümsüz bırakılmalı.
      </p>

      {turler.length > 0 && (
        <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
          {turler.map((tur) => (
            <li key={tur.id} className="font-body text-body-sm text-on-surface">
              {tur.ad} — {tur.fiyat} TRY
            </li>
          ))}
        </ul>
      )}

      <form
        className="space-y-stack-sm"
        onSubmit={(olay) => {
          olay.preventDefault();
          onHata(null);
          ekle.mutate();
        }}
      >
        <Alan>
          <TextField
            etiket="Tür adı"
            value={ad}
            required
            onChange={(o) => setAd(o.target.value)}
          />
          <TextField
            etiket="Fiyat (TRY)"
            type="number"
            min={0}
            step="0.01"
            value={fiyat}
            required
            onChange={(o) => setFiyat(o.target.value)}
          />
        </Alan>

        <Alan>
          <TextField
            etiket="Kontenjan"
            type="number"
            min={1}
            value={kontenjan}
            required
            ipucu="Türlerin toplamı salon kapasitesini aşamaz."
            onChange={(o) => setKontenjan(o.target.value)}
          />
          <Secim
            etiket="Koltuk bölümü (boş = varsayılan tür)"
            deger={bolumId}
            gerekli={false}
            bosMetin="Bölüme atanmasın"
            onDegis={setBolumId}
            secenekler={(plan.data?.sections ?? []).map((b) => ({ id: b.id, ad: b.name }))}
          />
        </Alan>

        <Alan>
          <TextField
            etiket="Satış başlangıcı"
            type="datetime-local"
            value={satisBas}
            required
            onChange={(o) => setSatisBas(o.target.value)}
          />
          <TextField
            etiket="Satış bitişi"
            type="datetime-local"
            value={satisBit}
            required
            onChange={(o) => setSatisBit(o.target.value)}
          />
        </Alan>

        <label className="flex items-center gap-base font-body text-body-sm text-on-surface">
          <input
            type="checkbox"
            checked={belgeIster}
            onChange={(o) => setBelgeIster(o.target.checked)}
            className="h-4 w-4 accent-primary"
          />
          Öğrenci belgesi gerektirir
        </label>

        <div className="flex flex-wrap gap-stack-sm">
          <Button type="submit" gorunum="cizgili" yukleniyor={ekle.isPending}>
            Bilet türünü ekle
          </Button>
          <Button type="button" onClick={onDevam} disabled={turler.length === 0}>
            Devam et
          </Button>
        </div>
      </form>
    </div>
  );
}

// --- 4 · Afiş -------------------------------------------------------------

function AfisAdimi({
  etkinlikId,
  afisId,
  onYuklendi,
  onDevam,
  onHata,
}: {
  etkinlikId: string;
  afisId: string | null;
  onYuklendi: (dosyaId: string) => void;
  onDevam: () => void;
  onHata: HataBildir;
}) {
  const [dosya, setDosya] = useState<File | null>(null);

  const yukle = useMutation({
    mutationFn: async () => {
      const dosyaId = await gorselYukle(dosya!);
      await afisBagla(etkinlikId, dosyaId);
      return dosyaId;
    },
    onSuccess: onYuklendi,
    onError: (h) => onHata(h, 'Afiş yüklenemedi.'),
  });

  return (
    <div className="space-y-stack-sm">
      <p className="font-body text-body-sm text-on-surface-variant">
        Afiş, yayına alınmanın ön koşulu. En fazla 5 MB; tür dosyanın içeriğine bakılarak
        doğrulanır, uzantıya değil.
      </p>

      <label className="flex flex-col gap-base">
        <span className="font-body text-body-sm text-on-surface-variant">Afiş görseli</span>
        <input
          type="file"
          accept="image/png,image/jpeg,image/webp"
          onChange={(olay) => setDosya(olay.target.files?.[0] ?? null)}
          className="font-body text-body-sm text-on-surface file:mr-stack-sm file:rounded-md file:border-0 file:bg-primary file:px-stack-sm file:py-base file:font-semibold file:text-on-primary"
        />
      </label>

      {afisId && (
        <p role="status" className="font-body text-body-sm text-primary">
          Afiş yüklendi ve etkinliğe bağlandı.
        </p>
      )}

      <div className="flex flex-wrap gap-stack-sm">
        <Button
          type="button"
          gorunum="cizgili"
          disabled={dosya === null}
          yukleniyor={yukle.isPending}
          onClick={() => {
            onHata(null);
            yukle.mutate();
          }}
        >
          Yükle
        </Button>
        <Button type="button" onClick={onDevam} disabled={afisId === null}>
          Devam et
        </Button>
      </div>
    </div>
  );
}

// --- 5 · Belgeler ---------------------------------------------------------

/**
 * Sahne sozlesmesi ve diger belgeler.
 *
 * <b>Onaya gondermenin on kosulu.</b> Onay ekibinin bakacagi asil sey
 * salonun o tarih icin gercekten tutuldugunu gosteren belge; belgesiz bir
 * basvuru onaylayan kisiye "guven bana" demekten baska bir sey sunmuyor.
 *
 * <para>
 * Iki adimli yukleme: once dosya (<c>POST /files/belge</c>), sonra
 * etkinlige baglama. Tek istekte yapilsaydi yarim kalan bir yuklemede
 * etkinlik kaydi da etkilenirdi.
 * </para>
 */
function BelgeAdimi({
  etkinlikId,
  onDegisti,
  onDevam,
  onHata,
}: {
  etkinlikId: string;
  onDegisti: (sozlesmeSayisi: number) => void;
  onDevam: () => void;
  onHata: HataBildir;
}) {
  const [tur, setTur] = useState<BelgeTuru>('VenueContract');
  const [not, setNot] = useState('');
  const [dosya, setDosya] = useState<File | null>(null);

  const belgeler = useQuery({
    queryKey: ['etkinlik-belgeleri', etkinlikId],
    queryFn: () => etkinlikBelgeleriniGetir(etkinlikId),
  });

  // Sunucu ozellikle SAHNE SOZLESMESI ariyor; "belge var" yetmiyor.
  const sozlesmeSayisi =
    belgeler.data?.filter((belge) => belge.kind === 'VenueContract').length ?? 0;

  useEffect(() => {
    onDegisti(sozlesmeSayisi);
  }, [sozlesmeSayisi, onDegisti]);

  const yukle = useMutation({
    mutationFn: async () => {
      if (!dosya) throw new Error('Önce bir dosya seç.');

      const dosyaId = await belgeYukle(dosya);
      await belgeBagla(etkinlikId, dosyaId, tur, not.trim() || null);
    },
    onSuccess: async () => {
      setDosya(null);
      setNot('');
      onHata(null);
      await belgeler.refetch();
    },
    onError: (h) => onHata(h, 'Belge eklenemedi.'),
  });

  const sil = useMutation({
    mutationFn: (belgeId: string) => belgeSil(etkinlikId, belgeId),
    onSuccess: async () => {
      onHata(null);
      await belgeler.refetch();
    },
    onError: (h) => onHata(h, 'Belge kaldırılamadı.'),
  });

  return (
    <div className="space-y-stack-sm">
      <p className="font-body text-body-sm text-on-surface-variant">
        Salonu o tarih için tuttuğunu gösteren sözleşmeyi ekle. Onay ekibi bu belgeye
        bakarak etkinliği yayına alıyor; sözleşme olmadan onaya gönderilemiyor. PDF veya
        görsel, en fazla 5 MB.
      </p>

      {belgeler.data && belgeler.data.length > 0 && (
        <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
          {belgeler.data.map((belge) => (
            <li key={belge.id} className="flex flex-wrap items-baseline gap-base">
              <span className="rounded-full border border-outline-variant px-base py-[2px] font-body text-[11px] text-on-surface-variant">
                {BELGE_TURU_METNI[belge.kind]}
              </span>

              <a
                href={dosyaAdresi(belge.uploadedFileId) ?? '#'}
                target="_blank"
                rel="noreferrer"
                className="font-body text-body-sm text-primary underline underline-offset-2"
              >
                {belge.originalFileName}
              </a>

              {belge.note && (
                <span className="font-body text-body-sm text-on-surface-variant">
                  — {belge.note}
                </span>
              )}

              <button
                type="button"
                onClick={() => sil.mutate(belge.id)}
                disabled={sil.isPending}
                className="ml-auto font-body text-body-sm text-error underline underline-offset-2 disabled:opacity-50"
              >
                Kaldır
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="grid gap-stack-sm sm:grid-cols-2">
        <Secim
          etiket="Belge türü"
          deger={tur}
          onDegis={(deger) => setTur(deger as BelgeTuru)}
          secenekler={(Object.keys(BELGE_TURU_METNI) as BelgeTuru[]).map((anahtar) => ({
            id: anahtar,
            ad: BELGE_TURU_METNI[anahtar],
          }))}
        />

        <TextField
          etiket="Not (isteğe bağlı)"
          value={not}
          onChange={(o) => setNot(o.target.value)}
          placeholder="Örn. 3. salon, 12 Ağustos"
        />
      </div>

      <label className="flex flex-col gap-base">
        <span className="font-body text-body-sm text-on-surface-variant">Dosya</span>
        <input
          type="file"
          accept=".pdf,.png,.jpg,.jpeg,.webp"
          onChange={(o) => setDosya(o.target.files?.[0] ?? null)}
          className="font-body text-body-sm text-on-surface file:mr-stack-sm file:rounded-md file:border-0 file:bg-surface-container-high file:px-stack-sm file:py-base file:font-body file:text-body-sm file:text-on-surface"
        />
      </label>

      <div className="flex flex-wrap gap-stack-sm">
        <Button
          type="button"
          gorunum="cizgili"
          yukleniyor={yukle.isPending}
          disabled={dosya === null}
          onClick={() => yukle.mutate()}
        >
          Belgeyi ekle
        </Button>

        {/* Devam, sozlesme olmadan KAPALI: sunucu zaten reddediyor ama
            kullanici bir adim daha ilerleyip orada duvara carpmak yerine
            eksigi burada gormeli. */}
        <Button type="button" onClick={onDevam} disabled={sozlesmeSayisi === 0}>
          Devam et
        </Button>
      </div>
    </div>
  );
}

// --- 6 · Onay -------------------------------------------------------------

function OnayAdimi({
  etkinlikId,
  oturumSayisi,
  turSayisi,
  afisVar,
  belgeSayisi,
  onGonderildi,
  onHata,
}: {
  etkinlikId: string;
  oturumSayisi: number;
  turSayisi: number;
  afisVar: boolean;
  belgeSayisi: number;
  onGonderildi: () => void;
  onHata: HataBildir;
}) {
  const gonder = useMutation({
    mutationFn: () => onayaGonder(etkinlikId),
    onSuccess: onGonderildi,
    onError: (h) => onHata(h, 'Onaya gönderilemedi.'),
  });

  const kosullar = [
    { metin: `${oturumSayisi} oturum`, saglandi: oturumSayisi > 0 },
    { metin: `${turSayisi} aktif bilet türü`, saglandi: turSayisi > 0 },
    { metin: 'Afiş', saglandi: afisVar },
    // Sunucu ozellikle SAHNE SOZLESMESI ariyor; buradaki sayac tur ayirmiyor
    // ama Belgeler adimi sozlesme disindaki turleri sozlesme yerine
    // saymiyor — sayac oradan geliyor.
    { metin: 'Sahne sözleşmesi', saglandi: belgeSayisi > 0 },
  ];

  return (
    <div className="space-y-stack-sm">
      <p className="font-body text-body-sm text-on-surface-variant">
        Onaya gönderildikten sonra etkinliği yönetici yayına alır. Yayın anında satılabilir
        koltuklar üretilir.
      </p>

      <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
        {kosullar.map((kosul) => (
          <li
            key={kosul.metin}
            className={`font-body text-body-sm ${kosul.saglandi ? 'text-on-surface' : 'text-error'}`}
          >
            <span className="inline-flex items-center gap-base">
              {kosul.saglandi ? (
                <OnayIkonu etiket="Sağlandı" className="h-4 w-4" />
              ) : (
                <CarpiIkonu etiket="Eksik" className="h-4 w-4" />
              )}
              {kosul.metin}
            </span>
          </li>
        ))}
      </ul>

      {/*
        Ön koşullar burada da gösteriliyor ama KARAR sunucunun: aynı kontrol
        domainde tek yerde duruyor ve ihlal 409 dönüyor. Buradaki liste
        kullanıcının neyi eksik bıraktığını sunucuya gitmeden görmesi için.
      */}
      <Button
        type="button"
        yukleniyor={gonder.isPending}
        disabled={kosullar.some((k) => !k.saglandi)}
        onClick={() => {
          onHata(null);
          gonder.mutate();
        }}
      >
        Onaya gönder
      </Button>
    </div>
  );
}
