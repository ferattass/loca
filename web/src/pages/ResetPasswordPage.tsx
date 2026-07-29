import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { z } from 'zod';

import { sifreSifirla } from '../api/auth';
import { hataMesaji } from '../api/client';
import { AuthLayout } from '../components/AuthLayout';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';

/** Sunucudaki ResetPasswordCommandValidator ile ayni kurallar. */
const yeniSifreSemasi = z
  .object({
    newPassword: z
      .string()
      .min(8, 'Sifre en az 8 karakter olmali.')
      .max(128, 'Sifre en fazla 128 karakter olabilir.')
      .regex(/[A-Za-z]/, 'Sifre en az bir harf icermeli.')
      .regex(/[0-9]/, 'Sifre en az bir rakam icermeli.'),
    newPasswordTekrar: z.string().min(1, 'Sifre tekrari zorunludur.'),
  })
  .refine((veri) => veri.newPassword === veri.newPasswordTekrar, {
    message: 'Sifreler eslesmiyor.',
    path: ['newPasswordTekrar'],
  });

type YeniSifreFormu = z.infer<typeof yeniSifreSemasi>;

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [aramaParametreleri] = useSearchParams();
  const [sunucuHatasi, setSunucuHatasi] = useState<string | null>(null);

  const token = aramaParametreleri.get('token');

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<YeniSifreFormu>({ resolver: zodResolver(yeniSifreSemasi) });

  // Token yoksa form hic gosterilmez: kullanici dolduracak, sonra "gecersiz
  // baglanti" cevabi alacakti.
  if (!token) {
    return (
      <AuthLayout
        baslik="Baglanti gecersiz"
        aciklama="Sifirlama baglantisi eksik veya bozuk gorunuyor."
        altBaglantiMetni="Yeni baglanti mi lazim?"
        altBaglantiEtiketi="Sifremi unuttum"
        altBaglantiYolu="/sifremi-unuttum"
      >
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          Baglantiyi e-postadan kopyalarken eksik almis olabilirsin. Yeni bir
          sifirlama baglantisi iste.
        </p>
      </AuthLayout>
    );
  }

  const gonder = async (veri: YeniSifreFormu) => {
    setSunucuHatasi(null);

    try {
      await sifreSifirla(token, veri.newPassword);

      // Sunucu sifirlamayla birlikte tum oturumlari kapatiyor; dogrudan
      // giris ekranina gonderiliyor.
      navigate('/giris', { replace: true, state: { bilgi: 'Sifren guncellendi. Yeni sifrenle giris yap.' } });
    } catch (hata) {
      setSunucuHatasi(hataMesaji(hata, 'Sifre sifirlanamadi.'));
    }
  };

  return (
    <AuthLayout
      baslik="Yeni sifre belirle"
      aciklama="Sifirlama baglantisi bir saat gecerli ve yalnizca bir kez kullanilabilir."
      altBaglantiMetni="Sifreni hatirladin mi?"
      altBaglantiEtiketi="Giris yap"
      altBaglantiYolu="/giris"
    >
      <form onSubmit={handleSubmit(gonder)} className="flex flex-col gap-stack-sm" noValidate>
        <TextField
          etiket="Yeni sifre"
          type="password"
          autoComplete="new-password"
          placeholder="••••••••"
          hata={errors.newPassword?.message}
          {...register('newPassword')}
        />

        <TextField
          etiket="Yeni sifre (tekrar)"
          type="password"
          autoComplete="new-password"
          placeholder="••••••••"
          hata={errors.newPasswordTekrar?.message}
          {...register('newPasswordTekrar')}
        />

        {sunucuHatasi && (
          <div
            role="alert"
            className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base"
          >
            <p className="font-body text-body-sm text-error">{sunucuHatasi}</p>
            <Link
              to="/sifremi-unuttum"
              className="font-body text-body-sm text-primary underline underline-offset-2"
            >
              Yeni baglanti iste
            </Link>
          </div>
        )}

        <Button type="submit" yukleniyor={isSubmitting} className="mt-base w-full">
          {isSubmitting ? 'Kaydediliyor' : 'Sifreyi guncelle'}
        </Button>
      </form>
    </AuthLayout>
  );
}
