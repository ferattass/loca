import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';

import { biletTuruEkle } from '../../api/events';
import { planGetir } from '../../api/seatLayouts';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { Alan, Secim, type HataBildir } from './ortak';
import { yereldenUtc } from './tarih';

// --- 3 · Bilet türleri ----------------------------------------------------

export function BiletTuruAdimi({
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

