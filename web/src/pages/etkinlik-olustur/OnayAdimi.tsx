import { useMutation } from '@tanstack/react-query';

import { onayaGonder } from '../../api/events';
import { Button } from '../../components/ui/Button';
import { CarpiIkonu, OnayIkonu } from '../../components/ui/Ikon';
import { type HataBildir } from './ortak';

// --- 6 · Onay -------------------------------------------------------------

export function OnayAdimi({
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
