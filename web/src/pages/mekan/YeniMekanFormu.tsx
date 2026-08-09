import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { type Sehir } from '../../api/catalog';
import { mekanOlustur } from '../../api/venues';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { Secim } from '../../components/ui/Secim';
import { HataKutusu, hataYapisiOlustur, type HataYapisi } from './hata';

// --- Yeni mekan ekleme -------------------------------------------------------

export function YeniMekanFormu({
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
