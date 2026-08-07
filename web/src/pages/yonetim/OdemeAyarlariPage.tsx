import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { alanHatalari as alanHatalariniAyikla, hataMesaji } from '../../api/client';
import {
  odemeAyarlariGetir,
  odemeAyarlariKaydet,
  type AyarKaynagi,
  type OdemeAyarlari,
} from '../../api/admin';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { OnayIkonu, UyariIkonu } from '../../components/ui/Ikon';

interface Form {
  apiKey: string;
  secretKey: string;
  sandbox: boolean;
  callbackUrl: string;
  returnUrl: string;
  anahtarlariKaldir: boolean;
  havaleAcik: boolean;
  bankaAdi: string;
  hesapSahibi: string;
  iban: string;
  odemeSuresi: string;
}

const BOS_FORM: Form = {
  apiKey: '',
  secretKey: '',
  sandbox: true,
  callbackUrl: '',
  returnUrl: '',
  anahtarlariKaldir: false,
  havaleAcik: false,
  bankaAdi: '',
  hesapSahibi: '',
  iban: '',
  odemeSuresi: '24',
};

const KAYNAK_METNI: Record<AyarKaynagi, string> = {
  Database: 'Bu ekrandan girilmiş',
  Configuration: 'Sunucu yapılandırmasından geliyor',
  Mixed: 'Bir kısmı bu ekrandan, bir kısmı sunucu yapılandırmasından',
  None: 'Henüz tanımlı değil',
};

/**
 * IBAN'i dortlu gruplara ayirir.
 *
 * Yalnizca GOSTERIM icin: sunucuya bosluksuz gidiyor ve veritabaninda da
 * bosluksuz duruyor. Gruplama olmadan yirmi alti karakterlik tek blok
 * ekrandan okunup bankaya yazilamiyor.
 */
function ibaniGrupla(iban: string): string {
  const temiz = iban.replace(/\s+/g, '').toUpperCase();
  return temiz.replace(/(.{4})/g, '$1 ').trim();
}

/**
 * Odeme ayarlari ekrani.
 *
 * <b>Iki odeme yolu tek formda:</b> kart (iyzico) ve havale. Ayri ekranlara
 * bolunmedi cunku ikisi birbirinin alternatifi — yonetici "hangisi acik"
 * sorusunun cevabini tek bakista gormeli.
 *
 * <para>
 * Anahtar alanlari her acilista BOS geliyor: sunucu anahtarlari hicbir zaman
 * geri gondermiyor, yalnizca tanimli olup olmadiklarini soyluyor. Bos
 * birakilip kaydedilirse mevcut anahtar korunuyor; silmek ayri ve acik bir
 * secim.
 * </para>
 */
export function OdemeAyarlariPage() {
  const queryClient = useQueryClient();

  const { data, isPending, isError, error } = useQuery<OdemeAyarlari>({
    queryKey: ['admin-odeme-ayarlari'],
    queryFn: odemeAyarlariGetir,
  });

  const [form, setForm] = useState<Form>(BOS_FORM);
  const [kaydedildi, setKaydedildi] = useState(false);

  // Sunucudan gelen degerler forma yalnizca ILK yuklemede yaziliyor: her
  // yeniden getirmede yazsaydi, kullanicinin yazmakta oldugu metin arka
  // planda gelen bir cevapla silinirdi.
  useEffect(() => {
    if (!data) return;

    setForm({
      apiKey: '',
      secretKey: '',
      sandbox: data.useSandbox,
      callbackUrl: data.callbackUrl,
      returnUrl: data.returnUrl,
      anahtarlariKaldir: false,
      havaleAcik: data.bankTransfer.enabled,
      bankaAdi: data.bankTransfer.bankName,
      hesapSahibi: data.bankTransfer.accountName,
      iban: ibaniGrupla(data.bankTransfer.iban),
      odemeSuresi: String(data.bankTransfer.deadlineHours),
    });
  }, [data]);

  const kaydet = useMutation({
    mutationFn: () =>
      odemeAyarlariKaydet({
        apiKey: form.apiKey.trim() || null,
        secretKey: form.secretKey.trim() || null,
        useSandbox: form.sandbox,
        callbackUrl: form.callbackUrl.trim(),
        returnUrl: form.returnUrl.trim(),
        bankTransferEnabled: form.havaleAcik,
        bankName: form.bankaAdi.trim(),
        accountName: form.hesapSahibi.trim(),
        iban: form.iban.replace(/\s+/g, '').toUpperCase(),
        // Bos birakilan sure sunucuya NaN olarak gitmesin diye 0'a
        // dusuruluyor; dogrulama 1-168 istiyor ve alan bazli hatayi
        // sunucu donuyor.
        deadlineHours: Number.parseInt(form.odemeSuresi, 10) || 0,
        clearIyzicoKeys: form.anahtarlariKaldir,
      }),
    onSuccess: async () => {
      setKaydedildi(true);
      setForm((onceki) => ({
        ...onceki,
        apiKey: '',
        secretKey: '',
        anahtarlariKaldir: false,
      }));
      await queryClient.invalidateQueries({ queryKey: ['admin-odeme-ayarlari'] });
    },
  });

  const gonder = (olay: React.FormEvent) => {
    olay.preventDefault();
    setKaydedildi(false);
    kaydet.mutate();
  };

  const alan = (ad: keyof Form) => (olay: React.ChangeEvent<HTMLInputElement>) => {
    const deger = olay.target.type === 'checkbox' ? olay.target.checked : olay.target.value;
    setKaydedildi(false);
    setForm((onceki) => ({ ...onceki, [ad]: deger }));
  };

  const alanHatalari = alanHatalariniAyikla(kaydet.error);
  const anahtarVar = Boolean(data?.hasApiKey || data?.hasSecretKey);

  return (
    <div className="mx-auto max-w-2xl">
      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-md text-on-surface">Ödeme ayarları</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          Sağlayıcı anahtarları ve havale bilgileri. Anahtarlar şifreli saklanır ve bu
          ekrana bir daha geri gelmez.
        </p>
      </header>

      {isPending && (
        <div className="animate-pulse space-y-stack-sm" aria-hidden="true">
          {[0, 1, 2, 3].map((sira) => (
            <div key={sira} className="h-14 rounded-md bg-surface-variant/40" />
          ))}
        </div>
      )}

      {isError && (
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hataMesaji(error, 'Ödeme ayarları yüklenemedi.')}
        </p>
      )}

      {data && (
        <>
          {/* Calisan saglayici panelden DEGISTIRILEMIYOR: secim acilista bir
              kez yapiliyor, cunku istek basina secilseydi ayni odemenin
              baslatilmasi ve tamamlanmasi iki farkli saglayiciya dusebilirdi.
              Gorunmesinin sebebi, anahtar girip "neden calismiyor" diye
              sorulmasini onlemek. */}
          <div className="mb-stack-md rounded-md border border-outline-variant/60 bg-surface-container-low px-stack-sm py-stack-sm">
            <div className="flex flex-wrap items-baseline justify-between gap-base">
              <span className="font-body text-body-sm text-on-surface-variant">
                Şu an çalışan sağlayıcı
              </span>
              <span className="font-body text-body-md font-semibold text-on-surface">
                {data.activeProvider}
              </span>
            </div>

            <p className="mt-base font-body text-body-sm text-on-surface-variant/70">
              Sağlayıcı seçimi açılışta yapılır, bu ekrandan değiştirilemez. Değiştirmek
              için sunucudaki <code className="font-mono">Payment:Provider</code> ayarı
              güncellenip uygulama yeniden başlatılmalı.
            </p>
          </div>

          <p
            className={`mb-stack-md flex items-start gap-base rounded-md border px-stack-sm py-base font-body text-body-sm ${
              data.iyzicoConfigured
                ? 'border-outline-variant/60 bg-surface-variant/20 text-on-surface-variant'
                : 'border-tertiary/40 bg-tertiary-container/10 text-tertiary'
            }`}
          >
            {data.iyzicoConfigured ? (
              <OnayIkonu className="mt-[2px] h-4 w-4 shrink-0" />
            ) : (
              <UyariIkonu className="mt-[2px] h-4 w-4 shrink-0" />
            )}
            {/* Uc ayri durum, uc ayri cumle. Ikiye indirilseydi ("tanimli /
                tanimli degil") anahtarlari girmis ama geri donus adresini
                bos birakmis yonetici "anahtar tanimli degil" mesajini
                gorup anahtari tekrar tekrar girerdi; eksik olan baska bir
                alan. */}
            <span>
              {data.iyzicoConfigured
                ? `iyzico yapılandırılmış — ${KAYNAK_METNI[data.source]}.`
                : anahtarVar
                  ? 'iyzico anahtarları tanımlı ama sunucu geri dönüş adresi boş. Bu hâliyle kartla ödeme başlatılamıyor.'
                  : 'iyzico anahtarları tanımlı değil. Şu an kartla ödeme başlatılamıyor.'}
            </span>
          </p>

          <form onSubmit={gonder} className="space-y-stack-lg" noValidate>
            <section className="space-y-stack-md">
              <h2 className="font-headline text-title-lg text-on-surface">
                Kart ödemesi (iyzico)
              </h2>

              <TextField
                etiket="API anahtarı"
                type="password"
                value={form.apiKey}
                onChange={alan('apiKey')}
                hata={alanHatalari.ApiKey?.[0]}
                disabled={form.anahtarlariKaldir}
                ipucu={
                  data.hasApiKey
                    ? 'Kayıtlı bir anahtar var. Boş bırakırsan değişmez.'
                    : 'Kayıtlı anahtar yok.'
                }
                autoComplete="new-password"
              />

              <TextField
                etiket="Gizli anahtar"
                type="password"
                value={form.secretKey}
                onChange={alan('secretKey')}
                hata={alanHatalari.SecretKey?.[0]}
                disabled={form.anahtarlariKaldir}
                ipucu={
                  data.hasSecretKey
                    ? 'Kayıtlı bir anahtar var. Boş bırakırsan değişmez.'
                    : 'Kayıtlı anahtar yok.'
                }
                autoComplete="new-password"
              />

              {/* Kaldirma AYRI bir secim: bos alan "koru" demek. Bu secenek
                  olmasaydi bir kez kaydedilen anahtar hicbir zaman
                  silinemez, veritabaninda kalirdi. */}
              {anahtarVar && (
                <label className="flex items-center gap-base font-body text-body-sm text-on-surface-variant">
                  <input
                    type="checkbox"
                    checked={form.anahtarlariKaldir}
                    onChange={alan('anahtarlariKaldir')}
                    className="h-4 w-4 accent-error"
                  />
                  Kayıtlı anahtarları kaldır
                </label>
              )}

              {alanHatalari.ClearIyzicoKeys?.[0] && (
                <p role="alert" className="font-body text-body-sm text-error">
                  {alanHatalari.ClearIyzicoKeys[0]}
                </p>
              )}

              {/* Sandbox VARSAYILAN: yapilandirma unutulursa yanlislikla
                  canli tahsilat yapmak yerine sandbox'ta kalinir. Kapatmak
                  gercek kart, gercek para demek — o yuzden uyari metni
                  onaylandiginda degil, secildiginde gorunuyor. */}
              <label className="flex items-start gap-stack-sm rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-stack-sm">
                <input
                  type="checkbox"
                  checked={form.sandbox}
                  onChange={alan('sandbox')}
                  className="mt-1 h-4 w-4 accent-primary"
                />
                <span className="font-body text-body-sm text-on-surface">
                  Test ortamı (sandbox)
                  <span className="mt-[2px] block text-body-sm text-on-surface-variant/70">
                    {form.sandbox
                      ? 'Ödemeler iyzico test ortamına gider, gerçek para çekilmez.'
                      : 'Kapalı: gerçek kart, gerçek tahsilat. Üye işyeri onayı gerekir.'}
                  </span>
                </span>
              </label>

              <TextField
                etiket="Sunucu geri dönüş adresi"
                value={form.callbackUrl}
                onChange={alan('callbackUrl')}
                hata={alanHatalari.CallbackUrl?.[0]}
                placeholder="https://alanadi.com/api/v1/payments/callback"
                ipucu="iyzico ödeme sonucunu bu adrese POST eder. Dışarıdan erişilebilir olmalı."
                autoComplete="off"
              />

              <TextField
                etiket="Arayüz dönüş adresi"
                value={form.returnUrl}
                onChange={alan('returnUrl')}
                hata={alanHatalari.ReturnUrl?.[0]}
                placeholder="https://alanadi.com/odeme/sonuc"
                ipucu="Kullanıcının ödeme sonrası göreceği sayfa."
                autoComplete="off"
              />
            </section>

            <section className="space-y-stack-md border-t border-outline-variant/40 pt-stack-md">
              <h2 className="font-headline text-title-lg text-on-surface">Havale / EFT</h2>

              <label className="flex items-start gap-stack-sm rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-stack-sm">
                <input
                  type="checkbox"
                  checked={form.havaleAcik}
                  onChange={alan('havaleAcik')}
                  className="mt-1 h-4 w-4 accent-primary"
                />
                <span className="font-body text-body-sm text-on-surface">
                  Havale ile ödemeye izin ver
                  <span className="mt-[2px] block text-body-sm text-on-surface-variant/70">
                    Açıkken koltuklar ödeme yapılmadan da tutulur; ödeme gelmezse süre
                    sonunda serbest bırakılır.
                  </span>
                </span>
              </label>

              {/* Alanlar havale kapaliyken de gorunuyor ama pasif: gizlenseydi
                  yonetici "IBAN nereye kaydedildi" diye aramak zorunda kalirdi
                  ve kayitli deger duruyor. */}
              <TextField
                etiket="Banka adı"
                value={form.bankaAdi}
                onChange={alan('bankaAdi')}
                hata={alanHatalari.BankName?.[0]}
                disabled={!form.havaleAcik}
                autoComplete="off"
              />

              <TextField
                etiket="Hesap sahibi"
                value={form.hesapSahibi}
                onChange={alan('hesapSahibi')}
                hata={alanHatalari.AccountName?.[0]}
                disabled={!form.havaleAcik}
                autoComplete="off"
              />

              <TextField
                etiket="IBAN"
                value={form.iban}
                onChange={alan('iban')}
                onBlur={() =>
                  setForm((onceki) => ({ ...onceki, iban: ibaniGrupla(onceki.iban) }))
                }
                hata={alanHatalari.Iban?.[0]}
                disabled={!form.havaleAcik}
                placeholder="TR00 0000 0000 0000 0000 0000 00"
                ipucu="Boşluklu yapıştırılabilir; kaydederken temizlenir."
                autoComplete="off"
              />

              <TextField
                etiket="Ödeme süresi (saat)"
                type="number"
                inputMode="numeric"
                value={form.odemeSuresi}
                onChange={alan('odemeSuresi')}
                hata={alanHatalari.DeadlineHours?.[0]}
                disabled={!form.havaleAcik}
                ipucu="Koltuk kilidi on dakika; havale banka saatlerine bağlı olduğu için ayrı ve uzun bir süre gerekir."
              />
            </section>

            {/* Ozet kutusu YALNIZCA alan bazli hata yokken: dogrulama hatasi
                zaten kendi alaninin altinda yaziyor ve ayni cumleyi altta
                tekrar etmek iki ayri sorun oldugunu dusundururdu. */}
            {kaydet.isError && Object.keys(alanHatalari).length === 0 && (
              <p
                role="alert"
                className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
              >
                {hataMesaji(kaydet.error, 'Ödeme ayarları kaydedilemedi.')}
              </p>
            )}

            {kaydedildi && (
              <p
                role="status"
                className="flex items-center gap-base rounded-md border border-primary/40 bg-primary-container/15 px-stack-sm py-base font-body text-body-sm text-primary"
              >
                <OnayIkonu className="h-4 w-4 shrink-0" />
                Ödeme ayarları kaydedildi.
              </p>
            )}

            <Button type="submit" yukleniyor={kaydet.isPending}>
              Kaydet
            </Button>
          </form>
        </>
      )}
    </div>
  );
}
