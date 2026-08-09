import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { hataMesaji } from '../../api/client';
import { type MekanDetayi, mekanDetayiGetir, mekanGuncelle, mekanSil, salonEkle, salonSil } from '../../api/venues';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { HataKutusu, hataYapisiOlustur, type HataYapisi } from './hata';
import { SilmeButonu } from './SilmeButonu';

// --- Secili mekanin detayi ---------------------------------------------------

export function MekanDetayPaneli({
  mekanId,
  onKapat,
  onSilindi,
}: {
  mekanId: string;
  onKapat: () => void;
  onSilindi: () => void;
}) {
  const detay = useQuery({
    queryKey: ['mekan-detay', mekanId],
    queryFn: () => mekanDetayiGetir(mekanId),
  });

  if (detay.isPending) {
    return (
      <div
        className="animate-pulse space-y-stack-sm rounded-lg border border-outline-variant/40 p-stack-sm"
        aria-hidden="true"
      >
        <div className="h-6 w-40 rounded bg-surface-variant/60" />
        <div className="h-40 rounded bg-surface-variant/40" />
        <span className="sr-only" role="status">
          Mekân detayı yükleniyor
        </span>
      </div>
    );
  }

  if (detay.isError) {
    return (
      <p
        role="alert"
        className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
      >
        {hataMesaji(detay.error, 'Mekân detayı yüklenemedi.')}
      </p>
    );
  }

  return <MekanDetayIcerigi mekan={detay.data} onKapat={onKapat} onSilindi={onSilindi} />;
}

export function MekanDetayIcerigi({
  mekan,
  onKapat,
  onSilindi,
}: {
  mekan: MekanDetayi;
  onKapat: () => void;
  onSilindi: () => void;
}) {
  const queryClient = useQueryClient();

  // Form alanlari sunucu verisinden yalnizca ilk cizimde baslatiliyor.
  // Salon eklenip silindiginde bu bilesen yeniden monte olmuyor (parent
  // `mekanId` degismedigi surece key sabit) — kullanicinin duzenlemekte
  // oldugu ad/adres metni, salon listesindeki bir degisiklik yuzunden
  // ustune yazilmiyor.
  const [ad, setAd] = useState(mekan.name);
  const [adres, setAdres] = useState(mekan.address);
  const [aciklama, setAciklama] = useState(mekan.description ?? '');
  const [telefon, setTelefon] = useState(mekan.phoneNumber ?? '');
  const [duzenleHata, setDuzenleHata] = useState<HataYapisi | null>(null);
  const [silmeHata, setSilmeHata] = useState<string | null>(null);
  const [salonHata, setSalonHata] = useState<HataYapisi | null>(null);
  const [salonSilmeHatasi, setSalonSilmeHatasi] = useState<string | null>(null);
  const [salonAd, setSalonAd] = useState('');
  const [salonKapasite, setSalonKapasite] = useState('');

  const ortakInvalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ['mekan-detay', mekan.id] }),
      queryClient.invalidateQueries({ queryKey: ['mekan-listesi'] }),
    ]);

  const guncelle = useMutation({
    mutationFn: () =>
      mekanGuncelle(mekan.id, {
        name: ad,
        address: adres,
        description: aciklama,
        phoneNumber: telefon,
      }),
    onSuccess: async () => {
      setDuzenleHata(null);
      await ortakInvalidate();
    },
    onError: (h) => setDuzenleHata(hataYapisiOlustur(h, 'Mekân güncellenemedi.')),
  });

  const mekaniSil = useMutation({
    mutationFn: () => mekanSil(mekan.id),
    onSuccess: async () => {
      setSilmeHata(null);
      await queryClient.invalidateQueries({ queryKey: ['mekan-listesi'] });
      onSilindi();
    },
    // 409 burada en olasi hata: salonu olan bir mekan silinemez. Sunucunun
    // dondugu detay mesaji dogrudan gosteriliyor.
    onError: (h) => setSilmeHata(hataMesaji(h, 'Mekân silinemedi.')),
  });

  const salonEkleMutasyonu = useMutation({
    mutationFn: () => salonEkle(mekan.id, { name: salonAd, capacity: Number(salonKapasite) }),
    onSuccess: async () => {
      setSalonAd('');
      setSalonKapasite('');
      setSalonHata(null);
      await ortakInvalidate();
    },
    onError: (h) => setSalonHata(hataYapisiOlustur(h, 'Salon eklenemedi.')),
  });

  const salonSilMutasyonu = useMutation({
    mutationFn: (salonId: string) => salonSil(salonId),
    onSuccess: async () => {
      setSalonSilmeHatasi(null);
      await ortakInvalidate();
    },
    // 409 burada en olasi hata: bagli oturma plani olan salon silinemez.
    onError: (h) => setSalonSilmeHatasi(hataMesaji(h, 'Salon silinemedi.')),
  });

  return (
    <div className="space-y-stack-md rounded-lg border border-outline-variant/40 bg-surface-variant/10 p-stack-sm">
      <div className="flex items-center justify-between gap-stack-sm">
        <h2 className="font-headline text-title-lg text-on-surface">{mekan.name}</h2>
        <button
          type="button"
          onClick={onKapat}
          className="font-body text-body-sm text-primary underline underline-offset-2"
        >
          Listeye dön
        </button>
      </div>

      <form
        className="space-y-stack-sm"
        onSubmit={(olay) => {
          olay.preventDefault();
          setDuzenleHata(null);
          guncelle.mutate();
        }}
      >
        <HataKutusu hata={duzenleHata} />

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

        <div className="flex flex-wrap items-center gap-stack-sm">
          <Button type="submit" yukleniyor={guncelle.isPending}>
            Değişiklikleri kaydet
          </Button>

          <SilmeButonu
            etiket="Mekânı sil"
            onayMetni={`"${mekan.name}" silinsin mi? Bu işlem geri alınamaz.`}
            yukleniyor={mekaniSil.isPending}
            onOnayla={() => mekaniSil.mutate()}
          />
        </div>

        {silmeHata && (
          <p role="alert" className="font-body text-body-sm text-error">
            {silmeHata}
          </p>
        )}
      </form>

      <div className="space-y-stack-sm border-t border-outline-variant/30 pt-stack-sm">
        <h3 className="font-body text-body-md font-semibold text-on-surface">
          Salonlar ({mekan.halls.length})
        </h3>

        {mekan.halls.length === 0 ? (
          <p className="font-body text-body-sm text-on-surface-variant">
            Henüz salon eklenmemiş.
          </p>
        ) : (
          <ul className="space-y-base">
            {mekan.halls.map((salon) => (
              <li
                key={salon.id}
                className="flex flex-wrap items-center justify-between gap-stack-sm rounded-md border border-outline-variant/30 bg-surface-variant/10 px-stack-sm py-base"
              >
                <div>
                  <p className="font-body text-body-sm text-on-surface">{salon.name}</p>
                  <p className="font-body text-body-sm text-on-surface-variant">
                    {salon.capacity} kişilik · {salon.isActive ? 'Aktif' : 'Pasif'}
                  </p>
                </div>

                <SilmeButonu
                  etiket="Sil"
                  onayMetni={`"${salon.name}" salonu silinsin mi?`}
                  yukleniyor={salonSilMutasyonu.isPending && salonSilMutasyonu.variables === salon.id}
                  onOnayla={() => salonSilMutasyonu.mutate(salon.id)}
                />
              </li>
            ))}
          </ul>
        )}

        {salonSilmeHatasi && (
          <p role="alert" className="font-body text-body-sm text-error">
            {salonSilmeHatasi}
          </p>
        )}

        <form
          className="space-y-stack-sm rounded-md border border-outline-variant/30 p-stack-sm"
          onSubmit={(olay) => {
            olay.preventDefault();
            setSalonHata(null);
            salonEkleMutasyonu.mutate();
          }}
        >
          <HataKutusu hata={salonHata} />

          <div className="grid gap-stack-sm md:grid-cols-2">
            <TextField
              etiket="Salon adı"
              value={salonAd}
              required
              onChange={(o) => setSalonAd(o.target.value)}
            />
            <TextField
              etiket="Kapasite"
              type="number"
              min={1}
              value={salonKapasite}
              required
              onChange={(o) => setSalonKapasite(o.target.value)}
            />
          </div>

          <Button type="submit" gorunum="cizgili" yukleniyor={salonEkleMutasyonu.isPending}>
            Salon ekle
          </Button>
        </form>
      </div>
    </div>
  );
}
