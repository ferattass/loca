import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { hataMesaji } from '../../api/client';
import { type OturmaPlani } from '../../api/seatLayouts';
import { type KoltukUretIstek, type KoltukUretSonuc, koltukUret } from '../../api/seatLayoutAdmin';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { Secim } from '../../components/ui/Secim';

/** Bolumler alt alta dizilir; yeni bolume onerilen bosluk bu kadar birim. */
const BOLUM_ARASI_BOSLUK = 80;

/**
 * "A,B,C" ve "A-H" bicimlerini sira etiketi dizisine cevirir.
 *
 * Sunucu her siraya ayri bir etiket bekliyor, kisaltma kabul etmiyor.
 * Sekiz sirali bir bolum icin kullaniciya "A,B,C,D,E,F,G,H" yazdirmak
 * yerine "A-H" araligini genisletmek, hem daha az hata payi birakiyor
 * hem de formu hizli doldurulabilir kiliyor.
 */
export function siralariAyristir(girdi: string): string[] {
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

export function KoltukUretFormu({
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
