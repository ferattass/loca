import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { hataMesaji } from '../../api/client';
import {
  adminKullanicilariGetir,
  hesapAc,
  rolDegistir,
  type AcilanHesap,
  type AdminKullanici,
  type RolAdi,
  type SayfaliSonuc,
} from '../../api/admin';
import { useAuthStore } from '../../stores/authStore';
import { OnayIkonu, SolOkIkonu, SagOkIkonu } from '../../components/ui/Ikon';
import { TextField } from '../../components/ui/TextField';

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' });

const ROLLER: { deger: RolAdi; metin: string; aciklama: string }[] = [
  { deger: 'Customer', metin: 'Müşteri', aciklama: 'Bilet alabilir' },
  { deger: 'Organizer', metin: 'Organizatör', aciklama: 'Etkinlik açar, kapıda okutur' },
  { deger: 'Moderator', metin: 'Moderatör', aciklama: 'Onay kuyruğunu yürütür' },
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
  const [hesapAcikMi, setHesapAcikMi] = useState(false);

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
      <header className="mb-stack-md flex flex-wrap items-start justify-between gap-stack-sm">
        <div>
          <h1 className="font-headline text-headline-md text-on-surface">Kullanıcılar</h1>
          <p className="font-body text-body-sm text-on-surface-variant">
            {data ? `${data.totalCount} kullanıcı` : 'Rol atama ve arama.'}
          </p>
        </div>

        {/* Hesap acma AYRI bir eylem, satir icinde degil: rol atama var olan
            bir kullaniciyi degistiriyor, bu ise yeni bir kayit uretiyor. */}
        <button
          type="button"
          onClick={() => setHesapAcikMi(true)}
          className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary"
        >
          Hesap aç
        </button>
      </header>

      {hesapAcikMi && (
        <HesapAcmaPenceresi
          onKapat={() => setHesapAcikMi(false)}
          onAcildi={async () => {
            setHesapAcikMi(false);
            await queryClient.invalidateQueries({ queryKey: ['admin-users'] });
          }}
        />
      )}

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

/**
 * Organizator/sanatci hesabi acma penceresi.
 *
 * <b>Sifre alani YOK ve bu bilincli.</b> Yonetici bir sifre yazsaydi onu
 * kullaniciya bir kanaldan iletmesi gerekirdi (telefon, mesaj) ve o kanalda
 * kalici olarak dururdu; ustelik yonetici hesabin sifresini bilmeye devam
 * ederdi. Sunucu rastgele bir sifre uretiyor ve kullaniciya sifirlama
 * baglantisi gonderiyor — ilk sifreyi hesabin sahibi koyuyor.
 *
 * <para>
 * Admin rolu listede YOK: hesap acma ve yetki yukseltme ayri isler ve
 * sunucu da bu uctan admin verilmesini reddediyor.
 * </para>
 */
function HesapAcmaPenceresi({
  onKapat,
  onAcildi,
}: {
  onKapat: () => void;
  onAcildi: () => Promise<void>;
}) {
  const [eposta, setEposta] = useState('');
  const [adSoyad, setAdSoyad] = useState('');
  const [telefon, setTelefon] = useState('');
  const [roller, setRoller] = useState<RolAdi[]>(['Customer', 'Organizer']);
  const [sonuc, setSonuc] = useState<AcilanHesap | null>(null);

  const ac = useMutation({
    mutationFn: () =>
      hesapAc({
        email: eposta.trim(),
        fullName: adSoyad.trim(),
        phoneNumber: telefon.trim() || null,
        roles: roller,
      }),
    onSuccess: (cevap) => setSonuc(cevap),
  });

  const rolKutusunuCevir = (rol: RolAdi) =>
    setRoller((onceki) =>
      onceki.includes(rol) ? onceki.filter((secili) => secili !== rol) : [...onceki, rol],
    );

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/60 px-container-margin-mobile">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="hesap-ac-basligi"
        className="w-full max-w-md rounded-lg border border-outline-variant/40 bg-surface-container p-stack-md"
      >
        <h2 id="hesap-ac-basligi" className="font-headline text-title-lg text-on-surface">
          Hesap aç
        </h2>

        {sonuc ? (
          <>
            <p className="mt-stack-sm rounded-md border border-primary/40 bg-primary-container/15 px-stack-sm py-base font-body text-body-sm text-primary">
              {sonuc.email} için hesap açıldı.
            </p>

            <p className="mt-base font-body text-body-sm text-on-surface-variant">
              {sonuc.resetLinkSent
                ? 'Şifre belirleme bağlantısı e-posta ile gönderildi. Şifreyi hesabın sahibi koyuyor; panelde görünmez.'
                : 'Hesap açıldı ama posta gönderilemedi (posta sunucusu tanımlı değil). Kullanıcı giriş ekranındaki "Şifremi unuttum" ile kendi bağlantısını isteyebilir.'}
            </p>

            <div className="mt-stack-sm flex justify-end">
              <button
                type="button"
                onClick={() => void onAcildi()}
                className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary"
              >
                Tamam
              </button>
            </div>
          </>
        ) : (
          <form
            className="mt-stack-sm space-y-stack-sm"
            onSubmit={(olay) => {
              olay.preventDefault();
              ac.mutate();
            }}
          >
            <TextField
              etiket="E-posta"
              type="email"
              value={eposta}
              onChange={(o) => setEposta(o.target.value)}
              required
              autoComplete="off"
            />

            <TextField
              etiket="Ad soyad"
              value={adSoyad}
              onChange={(o) => setAdSoyad(o.target.value)}
              required
              autoComplete="off"
            />

            <TextField
              etiket="Telefon (isteğe bağlı)"
              value={telefon}
              onChange={(o) => setTelefon(o.target.value)}
              autoComplete="off"
            />

            <fieldset>
              <legend className="mb-base font-body text-body-sm text-on-surface-variant">
                Roller
              </legend>

              <div className="space-y-base">
                {ROLLER.filter((secenek) => secenek.deger !== 'Admin').map((secenek) => (
                  <label
                    key={secenek.deger}
                    className="flex items-start gap-base font-body text-body-sm text-on-surface"
                  >
                    <input
                      type="checkbox"
                      checked={roller.includes(secenek.deger)}
                      onChange={() => rolKutusunuCevir(secenek.deger)}
                      className="mt-1 h-4 w-4 accent-primary"
                    />
                    <span>
                      {secenek.metin}
                      <span className="ml-base text-on-surface-variant/70">
                        {secenek.aciklama}
                      </span>
                    </span>
                  </label>
                ))}
              </div>

              <p className="mt-base font-body text-body-sm text-on-surface-variant/70">
                Admin yetkisi buradan verilemez; hesap açıldıktan sonra listeden atanır.
              </p>
            </fieldset>

            <p className="rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface-variant">
              Şifre girilmiyor. Kullanıcıya şifre belirleme bağlantısı gönderilir.
            </p>

            {ac.isError && (
              <p role="alert" className="font-body text-body-sm text-error">
                {hataMesaji(ac.error, 'Hesap açılamadı.')}
              </p>
            )}

            <div className="flex justify-end gap-base">
              <button
                type="button"
                onClick={onKapat}
                className="rounded-md border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface"
              >
                Vazgeç
              </button>
              <button
                type="submit"
                disabled={ac.isPending || roller.length === 0}
                className="rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary disabled:opacity-50"
              >
                {ac.isPending ? 'Açılıyor' : 'Hesabı aç'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
