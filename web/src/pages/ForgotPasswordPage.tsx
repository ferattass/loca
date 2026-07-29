import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';

import { sifremiUnuttum } from '../api/auth';
import { hataMesaji } from '../api/client';
import { AuthLayout } from '../components/AuthLayout';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';

const sifirlamaSemasi = z.object({
  email: z.string().min(1, 'E-posta zorunludur.').email('Gecerli bir e-posta adresi girin.'),
});

type SifirlamaFormu = z.infer<typeof sifirlamaSemasi>;

export function ForgotPasswordPage() {
  const [gonderildi, setGonderildi] = useState(false);
  const [sunucuHatasi, setSunucuHatasi] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SifirlamaFormu>({ resolver: zodResolver(sifirlamaSemasi) });

  const gonder = async (veri: SifirlamaFormu) => {
    setSunucuHatasi(null);

    try {
      await sifremiUnuttum(veri.email);
      setGonderildi(true);
    } catch (hata) {
      setSunucuHatasi(hataMesaji(hata, 'Istek gonderilemedi.'));
    }
  };

  if (gonderildi) {
    return (
      <AuthLayout
        baslik="Baglanti gonderildi"
        aciklama="Adres kayitliysa sifre sifirlama baglantisi gonderildi. Baglanti bir saat gecerli."
        altBaglantiMetni="Sifreni hatirladin mi?"
        altBaglantiEtiketi="Giris yap"
        altBaglantiYolu="/giris"
      >
        {/*
          Adresin kayitli olup olmadigi SOYLENMEZ. "Boyle bir kullanici yok"
          denseydi, hangi e-postalarin sistemde oldugu bu ekran uzerinden
          tek tek denenerek ogrenilebilirdi. Sunucu da ayni sebeple her iki
          durumda da basarili donuyor.
        */}
        <p
          role="status"
          className="rounded-md border border-outline/40 bg-surface-variant/40 px-stack-sm py-stack-sm font-body text-body-sm text-on-surface-variant"
        >
          Gelen kutunu kontrol et. Posta birkac dakika icinde gelmezse spam
          klasorune bakmayi unutma.
        </p>
      </AuthLayout>
    );
  }

  return (
    <AuthLayout
      baslik="Sifremi unuttum"
      aciklama="Kayitli e-posta adresini gir, sifirlama baglantisi gonderelim."
      altBaglantiMetni="Sifreni hatirladin mi?"
      altBaglantiEtiketi="Giris yap"
      altBaglantiYolu="/giris"
    >
      <form onSubmit={handleSubmit(gonder)} className="flex flex-col gap-stack-sm" noValidate>
        <TextField
          etiket="E-posta"
          type="email"
          autoComplete="email"
          placeholder="ornek@loca.dev"
          hata={errors.email?.message}
          {...register('email')}
        />

        {sunucuHatasi && (
          <div
            role="alert"
            className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base"
          >
            <p className="font-body text-body-sm text-error">{sunucuHatasi}</p>
          </div>
        )}

        <Button type="submit" yukleniyor={isSubmitting} className="mt-base w-full">
          {isSubmitting ? 'Gonderiliyor' : 'Sifirlama baglantisi gonder'}
        </Button>
      </form>
    </AuthLayout>
  );
}
