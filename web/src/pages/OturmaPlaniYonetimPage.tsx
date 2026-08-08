import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { hataMesaji } from '../api/client';
import { planGetir, type OturmaPlani } from '../api/seatLayouts';
import {
  bolumEkle,
  koltukUret,
  mekanlariGetir,
  planOlustur,
  planSil,
  planlariGetir,
  salonlariGetir,
  type KoltukUretIstek,
  type KoltukUretSonuc,
} from '../api/seatLayoutAdmin';
import { SeatMap } from '../components/SeatMap';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { Secim } from '../components/ui/Secim';

/** Bolumler alt alta dizilir; yeni bolume onerilen bosluk bu kadar birim. */
const BOLUM_ARASI_BOSLUK = 80;

/**
 * Oturma plani yonetim ekrani.
 *
 * Organizator sihirbazindaki (EtkinlikOlusturPage) salon secimi var olan bir
 * plani SECER; burasi ise plani var eden taraf: olusturma, silme, bolum
 * ekleme ve toplu koltuk uretme. Ikisi ayni domain nesnelerine bakiyor ama
 * yetkisi ve amaci farkli oldugu icin ayri sayfa ve ayri API dosyasinda.
 */
export function OturmaPlaniYonetimPage() {
  const [mekanId, setMekanId] = useState('');
  const [salonId, setSalonId] = useState('');
  const [planId, setPlanId] = useState('');
  const [hata, setHata] = useState<string | null>(null);

  const mekanlar = useQuery({ queryKey: ['admin-venues'], queryFn: mekanlariGetir });

  const salonlar = useQuery({
    queryKey: ['admin-halls', mekanId],
    queryFn: () => salonlariGetir(mekanId),
    enabled: mekanId !== '',
  });

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-5xl space-y-stack-md">
        <header>
          <h1 className="font-headline text-headline-md text-on-surface">
            Oturma planı yönetimi
          </h1>
          <p className="font-body text-body-sm text-on-surface-variant">
            Salon seç, plan oluştur veya sil, bölüm ekle ve koltukları toplu üret.
          </p>
        </header>

        {hata && (
          <p
            role="alert"
            className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
          >
            {hata}
          </p>
        )}

        <div className="grid gap-stack-sm md:grid-cols-2">
          <Secim
            etiket="Mekân"
            deger={mekanId}
            onDegis={(secilen) => {
              // Zincirin ustu degisince alttakiler sifirlanir; aksi halde
              // baska bir mekanin salonu secili kalir ve sunucu bunu
              // tutarsiz bulur.
              setMekanId(secilen);
              setSalonId('');
              setPlanId('');
            }}
            secenekler={(mekanlar.data ?? []).map((m) => ({
              id: m.id,
              ad: `${m.name} (${m.hallCount} salon)`,
            }))}
          />
          <Secim
            etiket="Salon"
            deger={salonId}
            devreDisi={mekanId === ''}
            onDegis={(secilen) => {
              setSalonId(secilen);
              setPlanId('');
            }}
            secenekler={(salonlar.data ?? []).map((s) => ({
              id: s.id,
              ad: `${s.name} — ${s.capacity} kişilik`,
            }))}
          />
        </div>

        {salonId !== '' && (
          <section className="space-y-stack-sm">
            <h2 className="font-body text-body-sm font-semibold text-on-surface">
              Oturma planları
            </h2>
            {/*
              key=salonId: salon degisince listedeki silme onayi ve yeni plan
              formu sifirlanir. Aksi halde bir onceki salonun formu doldurulmus
              halde yeni salona tasinirdi.
            */}
            <PlanYonetimi
              key={salonId}
              salonId={salonId}
              planId={planId}
              onPlanSec={setPlanId}
              onHata={setHata}
            />
          </section>
        )}

        {planId !== '' && (
          <section>
            <PlanDetayi key={planId} planId={planId} salonId={salonId} onHata={setHata} />
          </section>
        )}
      </div>
    </main>
  );
}

// --- Ortak secim kutusu ----------------------------------------------------


// --- Plan listesi ve olusturma ----------------------------------------------

function PlanYonetimi({
  salonId,
  planId,
  onPlanSec,
  onHata,
}: {
  salonId: string;
  planId: string;
  onPlanSec: (id: string) => void;
  onHata: (mesaj: string | null) => void;
}) {
  const queryClient = useQueryClient();
  const [silinecekId, setSilinecekId] = useState<string | null>(null);
  const [ad, setAd] = useState('');
  const [aciklama, setAciklama] = useState('');

  const planlar = useQuery({
    queryKey: ['admin-seat-layouts', salonId],
    queryFn: () => planlariGetir(salonId),
  });

  const olustur = useMutation({
    mutationFn: () =>
      planOlustur({ hallId: salonId, name: ad, description: aciklama === '' ? null : aciklama }),
    onSuccess: async (yeniPlanId) => {
      onHata(null);
      setAd('');
      setAciklama('');
      onPlanSec(yeniPlanId);
      await queryClient.invalidateQueries({ queryKey: ['admin-seat-layouts', salonId] });
    },
    onError: (h) => onHata(hataMesaji(h, 'Oturma planı oluşturulamadı.')),
  });

  const sil = useMutation({
    mutationFn: (id: string) => planSil(id),
    onSuccess: async (_veri, silinenId) => {
      setSilinecekId(null);
      onHata(null);

      // Silinen plan o an ekranda aciksa detay panelini kapatmak gerekir;
      // aksi halde sayfa artik var olmayan bir plani gostermeye calisirdi.
      if (silinenId === planId) onPlanSec('');

      await queryClient.invalidateQueries({ queryKey: ['admin-seat-layouts', salonId] });
    },
    onError: (h) => {
      setSilinecekId(null);
      onHata(hataMesaji(h, 'Oturma planı silinemedi.'));
    },
  });

  return (
    <div className="space-y-stack-sm">
      {planlar.isPending && (
        <p role="status" className="font-body text-body-sm text-on-surface-variant">
          Planlar yükleniyor…
        </p>
      )}

      {planlar.isError && (
        <p role="alert" className="font-body text-body-sm text-error">
          {hataMesaji(planlar.error, 'Planlar yüklenemedi.')}
        </p>
      )}

      {planlar.data && planlar.data.length > 0 && (
        <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
          {planlar.data.map((plan) => (
            <li
              key={plan.id}
              className="flex flex-wrap items-center justify-between gap-stack-sm font-body text-body-sm"
            >
              <button
                type="button"
                onClick={() => onPlanSec(plan.id)}
                className={
                  plan.id === planId
                    ? 'text-left font-semibold text-primary'
                    : 'text-left text-on-surface'
                }
              >
                {plan.name} ({plan.sectionCount} bölüm)
              </button>

              {silinecekId === plan.id ? (
                <span className="flex flex-wrap items-center gap-base">
                  <span className="text-on-surface-variant">Silinsin mi?</span>
                  <Button
                    type="button"
                    gorunum="cizgili"
                    yukleniyor={sil.isPending}
                    onClick={() => sil.mutate(plan.id)}
                  >
                    Evet, sil
                  </Button>
                  <Button type="button" gorunum="sade" onClick={() => setSilinecekId(null)}>
                    Vazgeç
                  </Button>
                </span>
              ) : (
                <Button type="button" gorunum="sade" onClick={() => setSilinecekId(plan.id)}>
                  Sil
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}

      <form
        className="flex flex-wrap items-end gap-stack-sm"
        onSubmit={(olay) => {
          olay.preventDefault();
          onHata(null);
          olustur.mutate();
        }}
      >
        <TextField
          etiket="Yeni plan adı"
          value={ad}
          required
          onChange={(o) => setAd(o.target.value)}
        />
        <TextField
          etiket="Açıklama (opsiyonel)"
          value={aciklama}
          onChange={(o) => setAciklama(o.target.value)}
        />
        <Button type="submit" gorunum="cizgili" yukleniyor={olustur.isPending}>
          Plan oluştur
        </Button>
      </form>
    </div>
  );
}

// --- Plan detayi: bolumler, koltuk uretimi, gorsel plan ---------------------

function PlanDetayi({
  planId,
  salonId,
  onHata,
}: {
  planId: string;
  salonId: string;
  onHata: (mesaj: string | null) => void;
}) {
  const { data, isPending, isError, error } = useQuery<OturmaPlani>({
    queryKey: ['seat-layout', planId],
    queryFn: () => planGetir(planId, true),
  });

  if (isPending) {
    return (
      <p role="status" className="font-body text-body-sm text-on-surface-variant">
        Plan yükleniyor…
      </p>
    );
  }

  if (isError) {
    return (
      <p role="alert" className="font-body text-body-sm text-error">
        {hataMesaji(error, 'Plan yüklenemedi.')}
      </p>
    );
  }

  const bolumlerSirali = [...data.sections].sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <div className="space-y-stack-md">
      <header>
        <p className="font-body text-label-caps uppercase tracking-widest text-primary">
          {data.hallName}
        </p>
        <h2 className="font-headline text-title-lg text-on-surface">{data.name}</h2>
        <p className="font-body text-body-sm text-on-surface-variant">
          {data.totalSeatCount} / {data.hallCapacity} koltuk
          {data.description ? ` — ${data.description}` : ''}
        </p>
      </header>

      <section className="space-y-stack-sm">
        <h3 className="font-body text-body-sm font-semibold text-on-surface">Bölümler</h3>

        {bolumlerSirali.length === 0 && (
          <p className="font-body text-body-sm text-on-surface-variant">
            Henüz bölüm eklenmemiş.
          </p>
        )}

        {bolumlerSirali.length > 0 && (
          <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
            {bolumlerSirali.map((bolum) => (
              <li key={bolum.id} className="font-body text-body-sm text-on-surface">
                {bolum.name} — {bolum.seats.length} koltuk
              </li>
            ))}
          </ul>
        )}

        <BolumEkleFormu planId={planId} salonId={salonId} onHata={onHata} />
      </section>

      <section className="space-y-stack-sm">
        <h3 className="font-body text-body-sm font-semibold text-on-surface">Koltuk üret</h3>
        <KoltukUretFormu planId={planId} plan={data} onHata={onHata} />
      </section>

      <section>
        <h3 className="mb-stack-sm font-body text-body-sm font-semibold text-on-surface">
          Görsel plan
        </h3>
        <SeatMap bolumler={data.sections} />
      </section>
    </div>
  );
}

// --- Bolum ekleme (onay ister) ----------------------------------------------

function BolumEkleFormu({
  planId,
  salonId,
  onHata,
}: {
  planId: string;
  salonId: string;
  onHata: (mesaj: string | null) => void;
}) {
  const queryClient = useQueryClient();
  const [ad, setAd] = useState('');
  const [sira, setSira] = useState('');
  const [onayBekleyen, setOnayBekleyen] = useState<{ name: string; displayOrder: number } | null>(
    null,
  );

  const ekle = useMutation({
    mutationFn: (istek: { name: string; displayOrder: number }) => bolumEkle(planId, istek),
    onSuccess: async () => {
      setOnayBekleyen(null);
      setAd('');
      setSira('');
      onHata(null);

      // Sectionlar hem plan detayinda (koltuk sayimi icin) hem de plan
      // listesinde (sectionCount rozeti icin) gorunuyor; ikisi de tazelenir.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['seat-layout', planId] }),
        queryClient.invalidateQueries({ queryKey: ['admin-seat-layouts', salonId] }),
      ]);
    },
    onError: (h) => {
      setOnayBekleyen(null);
      onHata(hataMesaji(h, 'Bölüm eklenemedi.'));
    },
  });

  if (onayBekleyen) {
    return (
      <div className="flex flex-wrap items-center gap-stack-sm rounded-md border border-outline-variant/40 bg-surface-variant/30 px-stack-sm py-base">
        <span className="font-body text-body-sm text-on-surface">
          "{onayBekleyen.name}" adında yeni bir bölüm eklenecek. Onaylıyor musun?
        </span>
        <Button type="button" yukleniyor={ekle.isPending} onClick={() => ekle.mutate(onayBekleyen)}>
          Evet, ekle
        </Button>
        <Button type="button" gorunum="sade" onClick={() => setOnayBekleyen(null)}>
          Vazgeç
        </Button>
      </div>
    );
  }

  return (
    <form
      className="flex flex-wrap items-end gap-stack-sm"
      onSubmit={(olay) => {
        olay.preventDefault();
        onHata(null);
        setOnayBekleyen({ name: ad, displayOrder: Number(sira) });
      }}
    >
      <TextField etiket="Bölüm adı" value={ad} required onChange={(o) => setAd(o.target.value)} />
      <TextField
        etiket="Görüntülenme sırası"
        type="number"
        min={0}
        value={sira}
        required
        ipucu="Bölümlerin ekranda hangi sırayla listeleneceğini belirler."
        onChange={(o) => setSira(o.target.value)}
      />
      <Button type="submit" gorunum="cizgili">
        Bölüm ekle
      </Button>
    </form>
  );
}

// --- Koltuk uretimi ----------------------------------------------------------

/**
 * "A,B,C" ve "A-H" bicimlerini sira etiketi dizisine cevirir.
 *
 * Sunucu her siraya ayri bir etiket bekliyor, kisaltma kabul etmiyor.
 * Sekiz sirali bir bolum icin kullaniciya "A,B,C,D,E,F,G,H" yazdirmak
 * yerine "A-H" araligini genisletmek, hem daha az hata payi birakiyor
 * hem de formu hizli doldurulabilir kiliyor.
 */
function siralariAyristir(girdi: string): string[] {
  const sonuc: string[] = [];

  for (const parca of girdi.split(',')) {
    const temiz = parca.trim();
    if (temiz === '') continue;

    const araligiEslesen = /^([A-Za-z])-([A-Za-z])$/.exec(temiz);

    if (araligiEslesen) {
      const bas = araligiEslesen[1].toUpperCase().charCodeAt(0);
      const bit = araligiEslesen[2].toUpperCase().charCodeAt(0);

      if (bit >= bas) {
        for (let kod = bas; kod <= bit; kod++) {
          sonuc.push(String.fromCharCode(kod));
        }
        continue;
      }
    }

    sonuc.push(temiz);
  }

  return sonuc;
}

function KoltukUretFormu({
  planId,
  plan,
  onHata,
}: {
  planId: string;
  plan: OturmaPlani;
  onHata: (mesaj: string | null) => void;
}) {
  const queryClient = useQueryClient();

  // Zaten koltugu olan bir bolume tekrar uretim istegi sunucudan 409 doner;
  // secenek listesinden cikararak kullanici bu hatayla hic karsilasmaz.
  const bosBolumler = plan.sections.filter((b) => b.seats.length === 0);

  const [bolumId, setBolumId] = useState('');
  const [sira, setSira] = useState('');
  const [koltukSayisi, setKoltukSayisi] = useState('');
  const [yatayAralik, setYatayAralik] = useState('30');
  const [dikeyAralik, setDikeyAralik] = useState('30');
  const [originY, setOriginY] = useState('');
  const [sonSonuc, setSonSonuc] = useState<KoltukUretSonuc | null>(null);

  const uret = useMutation({
    mutationFn: (istek: KoltukUretIstek) => koltukUret(planId, istek),
    onSuccess: async (sonuc) => {
      onHata(null);
      setSonSonuc(sonuc);
      setBolumId('');
      setSira('');
      setKoltukSayisi('');
      setOriginY('');
      await queryClient.invalidateQueries({ queryKey: ['seat-layout', planId] });
    },
    onError: (h) => onHata(hataMesaji(h, 'Koltuklar üretilemedi.')),
  });

  const siralar = siralariAyristir(sira);
  const canliSayi = siralar.length * (Number(koltukSayisi) || 0);
  const projeksiyon = plan.totalSeatCount + canliSayi;
  const kapasiteAsiyor = canliSayi > 0 && projeksiyon > plan.hallCapacity;

  if (bosBolumler.length === 0) {
    return (
      <p role="status" className="font-body text-body-sm text-on-surface-variant">
        Tüm bölümlerde koltuk üretilmiş. Yeni bir bölüm eklersen buradan koltuk üretebilirsin.
      </p>
    );
  }

  return (
    <form
      className="space-y-stack-sm"
      onSubmit={(olay) => {
        olay.preventDefault();
        onHata(null);

        if (siralar.length === 0) {
          onHata('En az bir sıra etiketi gerekli.');
          return;
        }

        uret.mutate({
          seatSectionId: bolumId,
          rowLabels: siralar,
          seatsPerRow: Number(koltukSayisi),
          horizontalSpacing: Number(yatayAralik),
          verticalSpacing: Number(dikeyAralik),
          originY: Number(originY),
        });
      }}
    >
      <Secim
        etiket="Bölüm"
        deger={bolumId}
        onDegis={(secilen) => {
          setBolumId(secilen);

          // Bolumler alt alta dizilir; oncekinin bittigi yerden baslamayan
          // bir originY, iki bolumun koltuklarini gorsel olarak ust uste
          // bindirir. Oneri her zaman duzenlenebilir kalir.
          const enBuyukY = plan.sections
            .flatMap((b) => b.seats.map((k) => k.positionY))
            .reduce((buyuk, y) => Math.max(buyuk, y), -1);

          setOriginY(enBuyukY < 0 ? '0' : String(enBuyukY + BOLUM_ARASI_BOSLUK));
        }}
        secenekler={bosBolumler.map((b) => ({ id: b.id, ad: b.name }))}
      />

      <div className="grid gap-stack-sm md:grid-cols-2">
        <TextField
          etiket="Sıra etiketleri"
          value={sira}
          required
          placeholder="A,B,C veya A-H"
          ipucu="Virgülle ayır veya aralık yaz: A-H sekiz sıra üretir."
          onChange={(o) => setSira(o.target.value)}
        />
        <TextField
          etiket="Sıra başına koltuk sayısı"
          type="number"
          min={1}
          value={koltukSayisi}
          required
          onChange={(o) => setKoltukSayisi(o.target.value)}
        />
      </div>

      <div className="grid gap-stack-sm md:grid-cols-3">
        <TextField
          etiket="Yatay aralık"
          type="number"
          min={1}
          value={yatayAralik}
          required
          onChange={(o) => setYatayAralik(o.target.value)}
        />
        <TextField
          etiket="Dikey aralık"
          type="number"
          min={1}
          value={dikeyAralik}
          required
          onChange={(o) => setDikeyAralik(o.target.value)}
        />
        <TextField
          etiket="Başlangıç Y (originY)"
          type="number"
          min={0}
          value={originY}
          required
          ipucu="Bölüm seçilince önceki bölümün bittiği yer önerilir."
          onChange={(o) => setOriginY(o.target.value)}
        />
      </div>

      {siralar.length > 0 && koltukSayisi !== '' && (
        <p className="font-body text-body-sm text-on-surface-variant">
          {siralar.length} sıra × {koltukSayisi} koltuk = {canliSayi} koltuk üretilecek.
        </p>
      )}

      {kapasiteAsiyor && (
        <p
          role="alert"
          className="rounded-md border border-tertiary/40 bg-tertiary-container/20 px-stack-sm py-base font-body text-body-sm text-tertiary"
        >
          Bu üretim salon kapasitesini aşıyor: {projeksiyon} / {plan.hallCapacity}.
        </p>
      )}

      {sonSonuc && (
        <p role="status" className="font-body text-body-sm text-primary">
          {sonSonuc.generatedCount} koltuk üretildi. Toplam {sonSonuc.totalSeatCount} koltuk.
        </p>
      )}

      <Button type="submit" yukleniyor={uret.isPending}>
        Koltukları üret
      </Button>
    </form>
  );
}
