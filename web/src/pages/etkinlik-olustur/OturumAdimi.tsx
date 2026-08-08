import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';

import { planlariGetir, salonDolulukGetir, type SalonDoluluk } from '../../api/catalog';
import { oturumEkle } from '../../api/events';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { Secim } from '../../components/ui/Secim';
import { Alan, type HataBildir } from './ortak';
import { yereldenUtc } from './tarih';

// --- 2 · Oturumlar --------------------------------------------------------

export function OturumAdimi({
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

