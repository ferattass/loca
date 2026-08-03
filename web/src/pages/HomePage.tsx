import { useQuery } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router-dom';

import { benKimim, cikisYap } from '../api/auth';
import { Button } from '../components/ui/Button';
import { useAuthStore } from '../stores/authStore';

/**
 * Oturum acan kullanicinin karsilastigi ilk ekran.
 *
 * Su an yalnizca kimligi ve rollerini gosteriyor. Etkinlik listesi Gun 5'te,
 * koltuk secimi Gun 6'da buraya baglanacak.
 */
export function HomePage() {
  const navigate = useNavigate();
  const { kullanici, refreshToken, oturumKapat } = useAuthStore();

  // Rol ve hesap durumu token uretildikten sonra degismis olabilir; bu yuzden
  // ekrandaki bilgi token claim'lerinden degil sunucudan okunur.
  const { data: sunucudakiKullanici, isLoading } = useQuery({
    queryKey: ['ben'],
    queryFn: benKimim,
  });

  const goruntulenen = sunucudakiKullanici ?? kullanici;

  const cikis = async () => {
    try {
      if (refreshToken) await cikisYap(refreshToken);
    } catch {
      // Sunucuya ulasilamasa bile yerel oturum kapatilir: kullanici cikmak
      // istedi, bu istegi ag hatasi yuzunden gormezden gelmek yanlis olur.
    } finally {
      oturumKapat();
      navigate('/giris', { replace: true });
    }
  };

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <header className="flex flex-wrap items-start justify-between gap-stack-sm mb-stack-lg">
        <div>
          <p className="font-body text-label-caps text-primary uppercase">Loca</p>
          <h1 className="font-display text-display-lg-mobile md:text-display-lg text-on-surface mt-base">
            Hos geldin{goruntulenen ? `, ${goruntulenen.fullName.split(' ')[0]}` : ''}
          </h1>
        </div>

        <Button gorunum="cizgili" onClick={cikis}>
          Cikis yap
        </Button>
      </header>

      <section className="glass rounded-xl p-stack-md max-w-2xl">
        <h2 className="font-headline text-headline-md text-on-surface mb-stack-sm">
          Hesap bilgileri
        </h2>

        {isLoading && !goruntulenen ? (
          <p className="font-body text-body-md text-on-surface-variant">Yukleniyor…</p>
        ) : (
          <dl className="grid gap-stack-sm sm:grid-cols-2">
            <div>
              <dt className="font-body text-body-sm text-on-surface-variant">Ad soyad</dt>
              <dd className="font-body text-body-md text-on-surface mt-1">
                {goruntulenen?.fullName}
              </dd>
            </div>

            <div>
              <dt className="font-body text-body-sm text-on-surface-variant">E-posta</dt>
              <dd className="font-body text-body-md text-on-surface mt-1">
                {goruntulenen?.email}
              </dd>
            </div>

            <div className="sm:col-span-2">
              <dt className="font-body text-body-sm text-on-surface-variant">Roller</dt>
              <dd className="flex flex-wrap gap-base mt-1">
                {goruntulenen?.roles.map((rol) => (
                  <span
                    key={rol}
                    className="rounded-full bg-primary-container/30 border border-primary/40 px-stack-sm py-1 font-body text-body-sm text-primary"
                  >
                    {rol}
                  </span>
                ))}
              </dd>
            </div>
          </dl>
        )}
      </section>

      <Kisayollar roller={goruntulenen?.roles ?? []} />
    </main>
  );
}

/**
 * Rolun gordugu ekranlar.
 *
 * <b>Menu sunucudan gelen rollere gore kuruluyor</b>, token claim'lerine
 * gore degil: rol admin tarafindan degistirilmis olabilir ve elindeki
 * token hâlâ eski rolu tasiyor olur. Gorunmeyen bag zaten koruma degil —
 * gercek kontrol sunucudaki policy'lerde; buradaki filtre yalnizca
 * kullaniciya erisemeyecegi bir sayfayi gostermemek icin.
 */
function Kisayollar({ roller }: { roller: string[] }) {
  const adminMi = roller.includes('Admin');
  const organizatorMu = adminMi || roller.includes('Organizer');

  const baglantilar = [
    { yol: '/rezervasyonlarim', metin: 'Rezervasyonlarim', gorunur: true },
    { yol: '/biletlerim', metin: 'Biletlerim', gorunur: true },
    { yol: '/etkinlikler/yeni', metin: 'Yeni etkinlik olustur', gorunur: organizatorMu },
    { yol: '/yonetim/mekanlar', metin: 'Mekan ve salon yonetimi', gorunur: adminMi },
    { yol: '/yonetim/oturma-planlari', metin: 'Oturma plani yonetimi', gorunur: adminMi },
  ].filter((baglanti) => baglanti.gorunur);

  return (
    <section className="mt-stack-md max-w-2xl">
      <h2 className="font-headline text-headline-md text-on-surface mb-stack-sm">Islemler</h2>

      <ul className="flex flex-col gap-base">
        {baglantilar.map((baglanti) => (
          <li key={baglanti.yol}>
            <Link
              to={baglanti.yol}
              className="font-body text-body-md text-primary hover:underline"
            >
              {baglanti.metin} →
            </Link>
          </li>
        ))}
      </ul>

      <Link
        to="/tasarim"
        className="inline-block mt-stack-sm font-body text-body-sm text-on-surface-variant hover:underline"
      >
        Tasarim sistemi dogrulama sayfasi →
      </Link>
    </section>
  );
}
