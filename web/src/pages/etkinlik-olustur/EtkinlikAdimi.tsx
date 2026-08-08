import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';

import { mekanlariGetir, salonlariGetir, sehirleriGetir } from '../../api/catalog';
import { etkinlikOlustur, kategorileriGetir } from '../../api/events';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { Secim } from '../../components/ui/Secim';
import { Alan, type HataBildir } from './ortak';
import { yereldenUtc } from './tarih';

// --- 1 · Etkinlik ---------------------------------------------------------

export function EtkinlikAdimi({
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

