import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { z } from 'zod';

import { girisYap } from '../api/auth';
import { hataMesaji } from '../api/client';
import { AuthLayout } from '../components/AuthLayout';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { useAuthStore } from '../stores/authStore';

/**
 * Giriste sifre kurallari DOGRULANMAZ — yalnizca bos olup olmadigina bakilir.
 * Sunucu tarafinda da ayni tercih yapildi: kural sonradan sikilastirilirsa
 * eski sifreye sahip kullanicilar giris yapamaz hale gelirdi.
 */
const girisSemasi = z.object({
  email: z.string().min(1, 'E-posta zorunludur.').email('Gecerli bir e-posta adresi girin.'),
  password: z.string().min(1, 'Sifre zorunludur.'),
});

type GirisFormu = z.infer<typeof girisSemasi>;

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const oturumAc = useAuthStore((durum) => durum.oturumAc);
  const [sunucuHatasi, setSunucuHatasi] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<GirisFormu>({ resolver: zodResolver(girisSemasi) });

  // Korumali bir sayfaya gitmeye calisip yonlendirilmisse giristen sonra
  // oraya donulur; kullanici bastan aramak zorunda kalmasin.
  const yonlendirmeDurumu = location.state as { hedef?: string; bilgi?: string } | null;
  const hedef = yonlendirmeDurumu?.hedef ?? '/';

  // Sifre sifirlandiktan sonra buraya bir bilgi mesajiyla donuluyor.
  const bilgi = yonlendirmeDurumu?.bilgi;

  const gonder = async (veri: GirisFormu) => {
    setSunucuHatasi(null);

    try {
      oturumAc(await girisYap(veri));
      navigate(hedef, { replace: true });
    } catch (hata) {
      setSunucuHatasi(hataMesaji(hata, 'Giris yapilamadi.'));
    }
  };

  return (
    <AuthLayout
      baslik="Giris yap"
      aciklama="Biletlerini gormek ve rezervasyon yapmak icin oturum ac."
      altBaglantiMetni="Hesabin yok mu?"
      altBaglantiEtiketi="Kayit ol"
      altBaglantiYolu="/kayit"
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

        <TextField
          etiket="Sifre"
          type="password"
          autoComplete="current-password"
          placeholder="••••••••"
          hata={errors.password?.message}
          {...register('password')}
        />

        <Link
          to="/sifremi-unuttum"
          className="self-end font-body text-body-sm text-primary underline underline-offset-2"
        >
          Sifremi unuttum
        </Link>

        {bilgi && (
          <div
            role="status"
            className="rounded-md border border-outline/40 bg-surface-variant/40 px-stack-sm py-base"
          >
            <p className="font-body text-body-sm text-on-surface-variant">{bilgi}</p>
          </div>
        )}

        {sunucuHatasi && (
          <div
            role="alert"
            className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base"
          >
            <p className="font-body text-body-sm text-error">{sunucuHatasi}</p>
          </div>
        )}

        <Button type="submit" yukleniyor={isSubmitting} className="mt-base w-full">
          {isSubmitting ? 'Giris yapiliyor' : 'Giris yap'}
        </Button>
      </form>
    </AuthLayout>
  );
}
