import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';

import { kayitOl } from '../api/auth';
import { hataMesaji } from '../api/client';
import { AuthLayout } from '../components/AuthLayout';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { useAuthStore } from '../stores/authStore';

/**
 * Kurallar sunucudaki <c>RegisterCommandValidator</c> ile ayni.
 *
 * Iki yerde dogrulama tekrar gibi gorunuyor ama farkli isler yapiyorlar:
 * buradaki, kullaniciya aninda geri bildirim verip bosuna istek atilmasini
 * onler; sunucudaki ise gercek korumadir — istemci dogrulamasi atlatilabilir.
 */
const kayitSemasi = z.object({
  fullName: z
    .string()
    .min(3, 'Ad soyad en az 3 karakter olmali.')
    .max(150, 'Ad soyad en fazla 150 karakter olabilir.'),
  email: z
    .string()
    .min(1, 'E-posta zorunludur.')
    .email('Gecerli bir e-posta adresi girin.')
    .max(256, 'E-posta en fazla 256 karakter olabilir.'),
  password: z
    .string()
    .min(8, 'Sifre en az 8 karakter olmali.')
    .max(128, 'Sifre en fazla 128 karakter olabilir.')
    .regex(/[A-Za-z]/, 'Sifre en az bir harf icermeli.')
    .regex(/[0-9]/, 'Sifre en az bir rakam icermeli.'),
  phoneNumber: z
    .string()
    .regex(/^[0-9+()\s-]+$/, 'Telefon numarasi gecersiz.')
    .max(20, 'Telefon numarasi en fazla 20 karakter olabilir.')
    .optional()
    .or(z.literal('')),
});

type KayitFormu = z.infer<typeof kayitSemasi>;

export function RegisterPage() {
  const navigate = useNavigate();
  const oturumAc = useAuthStore((durum) => durum.oturumAc);
  const [sunucuHatasi, setSunucuHatasi] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<KayitFormu>({ resolver: zodResolver(kayitSemasi) });

  const gonder = async (veri: KayitFormu) => {
    setSunucuHatasi(null);

    try {
      const cevap = await kayitOl({
        ...veri,
        // Bos metin yerine alan hic gonderilmez; sunucuda "bos ama gecersiz"
        // gibi bir ara durum olusmasin.
        phoneNumber: veri.phoneNumber?.trim() || undefined,
      });

      oturumAc(cevap);
      navigate('/', { replace: true });
    } catch (hata) {
      setSunucuHatasi(hataMesaji(hata, 'Kayit olusturulamadi.'));
    }
  };

  return (
    <AuthLayout
      baslik="Kayit ol"
      aciklama="Bilet almak icin once bir hesap olustur."
      altBaglantiMetni="Zaten hesabin var mi?"
      altBaglantiEtiketi="Giris yap"
      altBaglantiYolu="/giris"
    >
      <form onSubmit={handleSubmit(gonder)} className="flex flex-col gap-stack-sm" noValidate>
        <TextField
          etiket="Ad soyad"
          autoComplete="name"
          placeholder="Ferat Tas"
          hata={errors.fullName?.message}
          {...register('fullName')}
        />

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
          autoComplete="new-password"
          placeholder="••••••••"
          ipucu="En az 8 karakter, bir harf ve bir rakam."
          hata={errors.password?.message}
          {...register('password')}
        />

        <TextField
          etiket="Telefon (istege bagli)"
          type="tel"
          autoComplete="tel"
          placeholder="+90 555 000 00 00"
          hata={errors.phoneNumber?.message}
          {...register('phoneNumber')}
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
          {isSubmitting ? 'Hesap olusturuluyor' : 'Hesap olustur'}
        </Button>
      </form>
    </AuthLayout>
  );
}
