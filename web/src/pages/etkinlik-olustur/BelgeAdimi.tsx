import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';

import { dosyaAdresi } from '../../api/client';
import {
  BELGE_TURU_METNI,
  belgeBagla,
  belgeSil,
  belgeYukle,
  etkinlikBelgeleriniGetir,
  type BelgeTuru,
} from '../../api/onay';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { Secim } from '../../components/ui/Secim';
import { type HataBildir } from './ortak';

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
export function BelgeAdimi({
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

