import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { hataMesaji } from '../../api/client';
import {
  adminKullanicilariGetir,
  rolDegistir,
  type AdminKullanici,
  type RolAdi,
  type SayfaliSonuc,
} from '../../api/admin';
import { useAuthStore } from '../../stores/authStore';
import { OnayIkonu, SolOkIkonu, SagOkIkonu } from '../../components/ui/Ikon';

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' });

const ROLLER: { deger: RolAdi; metin: string; aciklama: string }[] = [
  { deger: 'Customer', metin: 'Müşteri', aciklama: 'Bilet alabilir' },
  { deger: 'Organizer', metin: 'Organizatör', aciklama: 'Etkinlik açar, kapıda okutur' },
  { deger: 'Admin', metin: 'Admin', aciklama: 'Her şeye erişir' },
];

/**
 * Kullanici ve rol yonetimi.
 *
 * Roller kutucuk olarak duruyor cunku bir kullanici birden fazla role
 * sahip olabiliyor (organizator ayni zamanda bilet aliyor). Acilir liste
 * konsaydi "tek rol secilir" izlenimi verirdi.
 */
export function KullanicilarPage() {
  const queryClient = useQueryClient();
  const { kullanici: mevcutKullanici } = useAuthStore();

  const [arama, setArama] = useState('');
  const [aktifArama, setAktifArama] = useState('');
  const [rolFiltresi, setRolFiltresi] = useState<RolAdi | undefined>();
  const [sayfa, setSayfa] = useState(1);

  const { data, isPending, isError, error } = useQuery<SayfaliSonuc<AdminKullanici>>({
    queryKey: ['admin-users', aktifArama, rolFiltresi, sayfa],
    queryFn: () =>
      adminKullanicilariGetir({ search: aktifArama, role: rolFiltresi, pageNumber: sayfa }),
  });

  const rol = useMutation({
    mutationFn: ({ id, ad, ver }: { id: string; ad: RolAdi; ver: boolean }) =>
      rolDegistir(id, ad, ver),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['admin-users'] });
    },
  });

  const aramaGonder = (olay: React.FormEvent) => {
    olay.preventDefault();
    setAktifArama(arama);
    setSayfa(1);
  };

  return (
    <div className="mx-auto max-w-5xl">
      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-md text-on-surface">Kullanıcılar</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          {data ? `${data.totalCount} kullanıcı` : 'Rol atama ve arama.'}
        </p>
      </header>

      <div className="mb-stack-sm flex flex-wrap items-center gap-base">
        <FiltreDugmesi
          secili={rolFiltresi === undefined}
          onClick={() => {
            setRolFiltresi(undefined);
            setSayfa(1);
          }}
        >
          Hepsi
        </FiltreDugmesi>

        {ROLLER.map((secenek) => (
          <FiltreDugmesi
            key={secenek.deger}
            secili={rolFiltresi === secenek.deger}
            onClick={() => {
              setRolFiltresi(secenek.deger);
              setSayfa(1);
            }}
          >
            {secenek.metin}
          </FiltreDugmesi>
        ))}

        <form onSubmit={aramaGonder} className="ml-auto flex gap-base">
          <input
            value={arama}
            onChange={(olay) => setArama(olay.target.value)}
            placeholder="Ad veya e-posta"
            aria-label="Kullanıcı ara"
            className="w-56 rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface"
          />
          <button
            type="submit"
            className="rounded-md border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
          >
            Ara
          </button>
        </form>
      </div>

      {isPending && (
        <div className="animate-pulse space-y-base" aria-hidden="true">
          {[0, 1, 2, 3].map((sira) => (
            <div key={sira} className="h-20 rounded-md bg-surface-variant/40" />
          ))}
        </div>
      )}

      {isError && (
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hataMesaji(error, 'Kullanıcılar yüklenemedi.')}
        </p>
      )}

      {rol.isError && (
        <p
          role="alert"
          className="mb-stack-sm rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
        >
          {hataMesaji(rol.error, 'Rol değiştirilemedi.')}
        </p>
      )}

      {data && data.items.length === 0 && (
        <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-md py-stack-md font-body text-body-md text-on-surface-variant">
          Eşleşen kullanıcı yok.
        </p>
      )}

      <ul className="space-y-base">
        {data?.items.map((satir) => (
          <li
            key={satir.id}
            className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-sm"
          >
            <div className="flex flex-wrap items-start justify-between gap-stack-sm">
              <div className="min-w-0">
                <p className="font-body text-body-md text-on-surface">
                  {satir.fullName}
                  {satir.emailConfirmed && (
                    <OnayIkonu
                      className="ml-base inline h-4 w-4 text-primary"
                      etiket="E-postası doğrulanmış"
                    />
                  )}
                </p>
                <p className="font-body text-body-sm text-on-surface-variant">{satir.email}</p>
                <p className="mt-base font-body text-body-sm text-on-surface-variant">
                  {satir.reservationCount} rezervasyon · {satir.ticketCount} bilet ·{' '}
                  {tarihBicimi.format(new Date(satir.createdAt))} tarihinde katıldı
                </p>
              </div>

              <div className="flex flex-wrap gap-base">
                {ROLLER.map((secenek) => {
                  const sahip = satir.roles.includes(secenek.deger);

                  // Admin kendi admin rolunu alamaz: tek adminli bir sistemde
                  // panele girisi olan hic kimse kalmayabilirdi. Sunucu da
                  // ayni kurali uyguluyor; buradaki kilit yalnizca kullaniciyi
                  // reddedilecek bir istekten koruyor.
                  const kendiAdminRolu =
                    secenek.deger === 'Admin' && satir.id === mevcutKullanici?.id && sahip;

                  return (
                    <button
                      key={secenek.deger}
                      type="button"
                      onClick={() =>
                        rol.mutate({ id: satir.id, ad: secenek.deger, ver: !sahip })
                      }
                      disabled={rol.isPending || kendiAdminRolu}
                      title={
                        kendiAdminRolu ? 'Kendi admin rolünü kaldıramazsın' : secenek.aciklama
                      }
                      aria-pressed={sahip}
                      className={`rounded-full border px-stack-sm py-1 font-body text-body-sm transition-colors disabled:opacity-50 ${
                        sahip
                          ? 'border-primary/50 bg-primary-container/25 font-semibold text-primary'
                          : 'border-outline-variant text-on-surface-variant hover:text-on-surface'
                      }`}
                    >
                      {secenek.metin}
                    </button>
                  );
                })}
              </div>
            </div>
          </li>
        ))}
      </ul>

      {data && data.totalPages > 1 && (
        <nav
          aria-label="Sayfalar"
          className="mt-stack-sm flex items-center justify-center gap-stack-sm"
        >
          <button
            type="button"
            onClick={() => setSayfa((s) => s - 1)}
            disabled={!data.hasPreviousPage}
            aria-label="Önceki sayfa"
            className="rounded-md border border-outline p-base text-on-surface disabled:opacity-40"
          >
            <SolOkIkonu className="h-4 w-4" />
          </button>

          <span className="font-body text-body-sm tabular-nums text-on-surface-variant">
            {data.pageNumber} / {data.totalPages}
          </span>

          <button
            type="button"
            onClick={() => setSayfa((s) => s + 1)}
            disabled={!data.hasNextPage}
            aria-label="Sonraki sayfa"
            className="rounded-md border border-outline p-base text-on-surface disabled:opacity-40"
          >
            <SagOkIkonu className="h-4 w-4" />
          </button>
        </nav>
      )}
    </div>
  );
}

function FiltreDugmesi({
  secili,
  onClick,
  children,
}: {
  secili: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={secili}
      className={`rounded-full px-stack-sm py-1 font-body text-body-sm transition-colors ${
        secili
          ? 'bg-primary-container/25 font-semibold text-primary'
          : 'border border-outline-variant text-on-surface-variant hover:text-on-surface'
      }`}
    >
      {children}
    </button>
  );
}
