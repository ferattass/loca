import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { alanHatalari as alanHatalariniAyikla, hataMesaji } from '../../api/client';
import {
  smtpAyarlariGetir,
  smtpAyarlariKaydet,
  smtpBaglantisiDene,
  type SmtpAyarlari,
} from '../../api/admin';
import { Button } from '../../components/ui/Button';
import { TextField } from '../../components/ui/TextField';
import { OnayIkonu, UyariIkonu } from '../../components/ui/Ikon';

interface Form {
  host: string;
  port: string;
  useSsl: boolean;
  userName: string;
  password: string;
  fromAddress: string;
  fromName: string;
  sifreyiKaldir: boolean;
}

const BOS_FORM: Form = {
  host: '',
  port: '587',
  useSsl: true,
  userName: '',
  password: '',
  fromAddress: '',
  fromName: 'Loca',
  sifreyiKaldir: false,
};

const KAYNAK_METNI: Record<SmtpAyarlari['source'], string> = {
  Database: 'Bu ekrandan girilmiş',
  Configuration: 'Sunucu yapılandırmasından geliyor',
  Mixed: 'Bir kısmı bu ekrandan, bir kısmı sunucu yapılandırmasından',
  None: 'Henüz tanımlı değil',
};

/**
 * Posta sunucusu ayarlari.
 *
 * <b>Neden veritabaninda:</b> SMTP bir isletme karari, altyapi degil. Posta
 * saglayicisi degistiginde deploy beklemek gerekmemeli. Veritabani baglanti
 * dizesi ve JWT anahtari BURAYA GIRMEZ; onlar uygulamanin ayaga kalkmasi
 * icin gerekli ve panele erisen birinin altyapiyi ele gecirmesi demek olurdu.
 *
 * <para>
 * Sifre alani her acilista BOS geliyor cunku sunucu sifreyi hicbir zaman
 * geri gondermiyor. Bos birakilip kaydedilirse mevcut sifre korunuyor;
 * silmek ayri ve acik bir secim.
 * </para>
 */
export function AyarlarPage() {
  const queryClient = useQueryClient();

  const { data, isPending, isError, error } = useQuery<SmtpAyarlari>({
    queryKey: ['admin-smtp'],
    queryFn: smtpAyarlariGetir,
  });

  const [form, setForm] = useState<Form>(BOS_FORM);
  const [kaydedildi, setKaydedildi] = useState(false);

  // Sunucudan gelen degerler forma yalnizca ILK yuklemede yaziliyor:
  // her yeniden getirmede yazsaydi, kullanicinin yazmakta oldugu metin
  // arka planda gelen bir cevapla silinirdi.
  useEffect(() => {
    if (!data) return;

    setForm({
      host: data.host,
      port: String(data.port),
      useSsl: data.useSsl,
      userName: data.userName ?? '',
      password: '',
      fromAddress: data.fromAddress,
      fromName: data.fromName,
      sifreyiKaldir: false,
    });
  }, [data]);

  const kaydet = useMutation({
    mutationFn: () =>
      smtpAyarlariKaydet({
        host: form.host.trim(),
        // Bos birakilan port sunucuya NaN olarak gitmesin diye 0'a
        // dusuruluyor; dogrulama zaten 1-65535 istiyor ve alan bazli
        // hatayi sunucu donuyor.
        port: Number.parseInt(form.port, 10) || 0,
        useSsl: form.useSsl,
        userName: form.userName.trim() || null,
        password: form.password || null,
        fromAddress: form.fromAddress.trim(),
        fromName: form.fromName.trim(),
        clearPassword: form.sifreyiKaldir,
      }),
    onSuccess: async () => {
      setKaydedildi(true);
      setForm((onceki) => ({ ...onceki, password: '', sifreyiKaldir: false }));
      await queryClient.invalidateQueries({ queryKey: ['admin-smtp'] });
    },
  });

  const dene = useMutation({ mutationFn: smtpBaglantisiDene });

  const gonder = (olay: React.FormEvent) => {
    olay.preventDefault();
    setKaydedildi(false);
    dene.reset();
    kaydet.mutate();
  };

  const alan = (ad: keyof Form) => (olay: React.ChangeEvent<HTMLInputElement>) => {
    const deger = olay.target.type === 'checkbox' ? olay.target.checked : olay.target.value;
    setKaydedildi(false);
    setForm((onceki) => ({ ...onceki, [ad]: deger }));
  };

  const alanHatalari = alanHatalariniAyikla(kaydet.error);

  return (
    <div className="mx-auto max-w-2xl">
      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-md text-on-surface">Posta ayarları</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          Şifre sıfırlama ve bilet bildirimleri bu sunucu üzerinden gönderilir.
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
          {hataMesaji(error, 'Ayarlar yüklenemedi.')}
        </p>
      )}

      {data && (
        <>
          <p
            className={`mb-stack-md flex items-start gap-base rounded-md border px-stack-sm py-base font-body text-body-sm ${
              data.isConfigured
                ? 'border-outline-variant/60 bg-surface-variant/20 text-on-surface-variant'
                : 'border-tertiary/40 bg-tertiary-container/10 text-tertiary'
            }`}
          >
            {data.isConfigured ? (
              <OnayIkonu className="mt-[2px] h-4 w-4 shrink-0" />
            ) : (
              <UyariIkonu className="mt-[2px] h-4 w-4 shrink-0" />
            )}
            <span>
              {data.isConfigured
                ? `Yapılandırılmış — ${KAYNAK_METNI[data.source]}.`
                : 'Posta sunucusu tanımlı değil. Şu an şifre sıfırlama e-postası gönderilemiyor.'}
            </span>
          </p>

          <form onSubmit={gonder} className="space-y-stack-md" noValidate>
            <TextField
              etiket="Sunucu adresi"
              value={form.host}
              onChange={alan('host')}
              hata={alanHatalari.Host?.[0]}
              placeholder="smtp.saglayici.com"
              autoComplete="off"
            />

            <div className="grid gap-stack-md sm:grid-cols-2">
              <TextField
                etiket="Port"
                type="number"
                inputMode="numeric"
                value={form.port}
                onChange={alan('port')}
                hata={alanHatalari.Port?.[0]}
                ipucu="465 doğrudan TLS, 587 STARTTLS, 1025 yerel Mailpit"
              />

              <label className="flex items-start gap-stack-sm self-end rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-stack-sm">
                <input
                  type="checkbox"
                  checked={form.useSsl}
                  onChange={alan('useSsl')}
                  className="mt-1 h-4 w-4 accent-primary"
                />
                <span className="font-body text-body-sm text-on-surface">
                  Şifreli bağlantı (TLS)
                  <span className="mt-[2px] block text-body-sm text-on-surface-variant/70">
                    Kapalıysa kullanıcı adı ve şifre ağ üzerinde açık gider.
                  </span>
                </span>
              </label>
            </div>

            <TextField
              etiket="Kullanıcı adı"
              value={form.userName}
              onChange={alan('userName')}
              hata={alanHatalari.UserName?.[0]}
              ipucu="Sunucu kimlik doğrulaması istemiyorsa boş bırak."
              autoComplete="off"
            />

            <div>
              <TextField
                etiket="Şifre"
                type="password"
                value={form.password}
                onChange={alan('password')}
                hata={alanHatalari.Password?.[0]}
                disabled={form.sifreyiKaldir}
                ipucu={
                  data.hasPassword
                    ? 'Kayıtlı bir şifre var. Boş bırakırsan değişmez.'
                    : 'Kayıtlı şifre yok.'
                }
                autoComplete="new-password"
              />

              {/* Kaldirma AYRI bir secim: bos alan "koru" demek. Bu secenek
                  olmasaydi bir kez kaydedilen sifre hicbir zaman
                  silinemez, veritabaninda kalirdi. */}
              {data.hasPassword && (
                <label className="mt-base flex items-center gap-base font-body text-body-sm text-on-surface-variant">
                  <input
                    type="checkbox"
                    checked={form.sifreyiKaldir}
                    onChange={alan('sifreyiKaldir')}
                    className="h-4 w-4 accent-error"
                  />
                  Kayıtlı şifreyi kaldır
                </label>
              )}
            </div>

            <div className="grid gap-stack-md sm:grid-cols-2">
              <TextField
                etiket="Gönderen adresi"
                type="email"
                value={form.fromAddress}
                onChange={alan('fromAddress')}
                hata={alanHatalari.FromAddress?.[0]}
                placeholder="bilet@loca.dev"
                autoComplete="off"
              />

              <TextField
                etiket="Gönderen adı"
                value={form.fromName}
                onChange={alan('fromName')}
                hata={alanHatalari.FromName?.[0]}
                autoComplete="off"
              />
            </div>

            {/* Ozet kutusu YALNIZCA alan bazli hata yokken: dogrulama
                hatasi zaten kendi alaninin altinda yaziyor ve ayni cumleyi
                bir de altta tekrar etmek kullaniciya iki ayri sorun
                oldugunu dusundururdu. Burasi 500, ag kopmasi gibi bir
                alana baglanamayan hatalar icin. */}
            {kaydet.isError && Object.keys(alanHatalari).length === 0 && (
              <p
                role="alert"
                className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
              >
                {hataMesaji(kaydet.error, 'Ayarlar kaydedilemedi.')}
              </p>
            )}

            {kaydedildi && (
              <p
                role="status"
                className="flex items-center gap-base rounded-md border border-primary/40 bg-primary-container/15 px-stack-sm py-base font-body text-body-sm text-primary"
              >
                <OnayIkonu className="h-4 w-4 shrink-0" />
                Ayarlar kaydedildi.
              </p>
            )}

            <div className="flex flex-wrap items-center gap-stack-sm">
              <Button type="submit" yukleniyor={kaydet.isPending}>
                Kaydet
              </Button>

              {/* Deneme KAYITLI ayarlarla yapiliyor, formdakiyle degil:
                  sunucuya baglanma isini istemciye tasimak, panele erisen
                  birinin sunucuyu ic aga baglanmak icin kullanmasi demek
                  olurdu. Bu yuzden once kaydetmek gerekiyor. */}
              <Button
                type="button"
                gorunum="cizgili"
                yukleniyor={dene.isPending}
                disabled={!data.isConfigured}
                onClick={() => {
                  setKaydedildi(false);
                  dene.mutate();
                }}
              >
                Bağlantıyı dene
              </Button>

              <span className="font-body text-body-sm text-on-surface-variant/70">
                Deneme kayıtlı ayarlarla yapılır, posta göndermez.
              </span>
            </div>
          </form>

          {dene.data && (
            <p
              role="status"
              className={`mt-stack-md flex items-start gap-base rounded-md border px-stack-sm py-stack-sm font-body text-body-sm ${
                dene.data.succeeded
                  ? 'border-primary/40 bg-primary-container/15 text-primary'
                  : 'border-error/40 bg-error-container/20 text-error'
              }`}
            >
              {dene.data.succeeded ? (
                <OnayIkonu className="mt-[2px] h-4 w-4 shrink-0" />
              ) : (
                <UyariIkonu className="mt-[2px] h-4 w-4 shrink-0" />
              )}
              <span className="min-w-0 break-words">
                {dene.data.succeeded
                  ? 'Sunucuya bağlanıldı ve kimlik doğrulandı.'
                  : `Bağlanılamadı: ${dene.data.error}`}
              </span>
            </p>
          )}

          {dene.isError && (
            <p
              role="alert"
              className="mt-stack-md rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
            >
              {hataMesaji(dene.error, 'Bağlantı denenemedi.')}
            </p>
          )}
        </>
      )}
    </div>
  );
}
