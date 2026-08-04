import { useCallback, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { dogrulamaHatalari, hataMesaji } from '../api/client';
import {
  mekanlariGetir,
  planlariGetir,
  salonlariGetir,
  sehirleriGetir,
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

  const adimlar = ['Etkinlik', 'Oturumlar', 'Bilet türleri', 'Afiş', 'Onay'];

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

        {adim === 5 && etkinlikId && !gonderildi && (
          <OnayAdimi
            etkinlikId={etkinlikId}
            oturumSayisi={oturumlar.length}
            turSayisi={biletTurleri.length}
            afisVar={afisId !== null}
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

        <div className="flex flex-wrap gap-stack-sm">
          <Button type="submit" gorunum="cizgili" yukleniyor={ekle.isPending}>
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

// --- 5 · Onay -------------------------------------------------------------

function OnayAdimi({
  etkinlikId,
  oturumSayisi,
  turSayisi,
  afisVar,
  onGonderildi,
  onHata,
}: {
  etkinlikId: string;
  oturumSayisi: number;
  turSayisi: number;
  afisVar: boolean;
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
