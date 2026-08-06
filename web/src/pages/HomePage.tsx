import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { benKimim, sifreDegistir } from '../api/auth';
import { alanHatalari, hataMesaji } from '../api/client';
import { rezervasyonlarimGetir, type RezervasyonOzeti } from '../api/reservations';
import { biletlerimGetir, type Bilet } from '../api/tickets';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { BiletIkonu, OnayIkonu, SagOkIkonu, UyariIkonu } from '../components/ui/Ikon';
import { useAuthStore } from '../stores/authStore';

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'long', timeStyle: 'short' });

const ROL_METNI: Record<string, string> = {
  Admin: 'Yönetici',
  Organizer: 'Organizatör',
  Customer: 'Müşteri',
};

/**
 * Hesap ekrani.
 *
 * <b>Gun 3'ten kalma bir yer tutucuydu:</b> yalnizca ad, e-posta ve rol
 * yaziyordu ve icinde "etkinlik listesi Gun 5'te buraya baglanacak"
 * notu duruyordu — o gun geldi gecti, ekran oldugu yerde kaldi.
 *
 * <para>
 * Simdi kullanicinin "hesabimda ne var" sorusuna cevap veriyor: bir
 * sonraki etkinligi, bekleyen rezervasyonu, bilet sayisi ve rolune gore
 * gidebilecegi yerler. Sayfa kendi basina bir ise yaramiyorsa menude
 * bir baglanti olmasinin da anlami yok.
 * </para>
 */
export function HomePage() {
  const yereldekiKullanici = useAuthStore((durum) => durum.kullanici);

  // Rol ve hesap durumu token uretildikten sonra degismis olabilir; bu
  // yuzden ekrandaki bilgi token claim'lerinden degil sunucudan okunur.
  const { data: sunucudakiKullanici } = useQuery({
    queryKey: ['ben'],
    queryFn: benKimim,
  });

  const kullanici = sunucudakiKullanici ?? yereldekiKullanici;

  const { data: biletler } = useQuery<Bilet[]>({
    queryKey: ['tickets'],
    queryFn: () => biletlerimGetir(),
  });

  const { data: rezervasyonlar } = useQuery<RezervasyonOzeti[]>({
    queryKey: ['reservations'],
    queryFn: rezervasyonlarimGetir,
  });

  const simdi = Date.now();

  const yaklasanBiletler = (biletler ?? [])
    .filter(
      (bilet) =>
        bilet.status === 'Valid' && new Date(bilet.eventStartsAtUtc).getTime() >= simdi,
    )
    .sort(
      (a, b) =>
        new Date(a.eventStartsAtUtc).getTime() - new Date(b.eventStartsAtUtc).getTime(),
    );

  const siradaki = yaklasanBiletler[0] ?? null;

  // Suresi dolmamis, henuz odenmemis rezervasyon: kullanicinin
  // yapabilecegi bir sey oldugu tek durum bu.
  const bekleyen = (rezervasyonlar ?? []).filter(
    (rezervasyon) => rezervasyon.status === 'Pending' && rezervasyon.remainingSeconds > 0,
  );

  const roller = kullanici?.roles ?? [];
  const organizatorMu = roller.includes('Organizer') || roller.includes('Admin');
  const adminMi = roller.includes('Admin');

  return (
    <main className="min-h-screen px-container-margin-mobile py-stack-lg md:px-container-margin-desktop">
      <div className="mx-auto max-w-4xl">
        <HesapBasligi kullanici={kullanici} roller={roller} />

        {bekleyen.length > 0 && <BekleyenUyarisi rezervasyonlar={bekleyen} />}

        <section aria-label="Özet" className="mt-stack-md grid gap-stack-sm sm:grid-cols-3">
          <SayiKutusu baslik="Yaklaşan bilet" deger={yaklasanBiletler.length} />
          <SayiKutusu baslik="Bekleyen rezervasyon" deger={bekleyen.length} vurgulu={bekleyen.length > 0} />
          <SayiKutusu baslik="Toplam bilet" deger={biletler?.length ?? 0} />
        </section>

        {siradaki && <SiradakiEtkinlik bilet={siradaki} />}

        <HizliErisim organizatorMu={organizatorMu} adminMi={adminMi} />

        {/* items-start: iki kart farkli boyda ve esitlenirlerse kisa
            olan altinda genis bir bosluk birakiyor. */}
        <div className="mt-stack-lg grid items-start gap-stack-md md:grid-cols-2">
          <HesapBilgileri kullanici={kullanici} roller={roller} />
          <SifreDegistir />
        </div>
      </div>
    </main>
  );
}

function HesapBasligi({
  kullanici,
  roller,
}: {
  kullanici: { fullName: string; email: string } | null | undefined;
  roller: string[];
}) {
  // Bas harfler: gercek bir profil fotografi yok ve yer tutucu bir
  // silüet koymak, olmayan bir ozellik varmis izlenimi verirdi.
  const basHarfler = (kullanici?.fullName ?? '')
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((parca) => parca[0]?.toLocaleUpperCase('tr-TR'))
    .join('');

  return (
    <header className="flex flex-wrap items-center gap-stack-md">
      <span
        aria-hidden="true"
        className="grid h-16 w-16 shrink-0 place-items-center rounded-full bg-gradient-to-br from-[#3b1b92] via-[#aa2484] to-[#fd2d6e] font-headline text-title-lg font-bold text-white"
      >
        {basHarfler || 'L'}
      </span>

      <div className="min-w-0">
        <p className="font-body text-label-caps uppercase text-primary">Hesabım</p>
        <h1 className="font-headline text-headline-lg text-on-surface">
          {kullanici?.fullName ?? 'Hesabım'}
        </h1>
        <p className="font-body text-body-sm text-on-surface-variant">{kullanici?.email}</p>
      </div>

      <div className="ml-auto flex flex-wrap gap-base">
        {roller.map((rol) => (
          <span
            key={rol}
            className="rounded-full border border-primary/40 bg-primary-container/20 px-stack-sm py-1 font-body text-[11px] uppercase tracking-widest text-primary"
          >
            {ROL_METNI[rol] ?? rol}
          </span>
        ))}
      </div>
    </header>
  );
}

/**
 * Suresi dolmak uzere olan rezervasyonlar.
 *
 * En uste konuyor cunku kullanicinin ZAMAN SINIRLI olarak yapabilecegi
 * tek sey bu; asagida bir yerde dursaydi koltuk kilidi kullanici farkina
 * varmadan duserdi.
 */
function BekleyenUyarisi({ rezervasyonlar }: { rezervasyonlar: RezervasyonOzeti[] }) {
  return (
    <section className="mt-stack-md rounded-lg border border-tertiary/40 bg-tertiary-container/10 p-stack-sm">
      <p className="flex items-center gap-base font-body text-body-md text-tertiary">
        <UyariIkonu className="h-5 w-5 shrink-0" />
        Ödemesi tamamlanmamış {rezervasyonlar.length} rezervasyonun var.
      </p>

      <ul className="mt-stack-sm space-y-base">
        {rezervasyonlar.map((rezervasyon) => (
          <li key={rezervasyon.id}>
            <Link
              to={`/rezervasyonlar/${rezervasyon.id}`}
              className="flex flex-wrap items-center justify-between gap-base rounded-md bg-surface-container-low px-stack-sm py-base transition-colors hover:bg-surface-container-high"
            >
              <span className="min-w-0 font-body text-body-md text-on-surface">
                {rezervasyon.eventTitle}
                <span className="text-on-surface-variant">
                  {' · '}
                  {rezervasyon.seatCount} koltuk
                </span>
              </span>

              <span className="shrink-0 font-body text-body-sm text-tertiary">
                {Math.max(1, Math.round(rezervasyon.remainingSeconds / 60))} dk kaldı
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

function SayiKutusu({
  baslik,
  deger,
  vurgulu = false,
}: {
  baslik: string;
  deger: number;
  vurgulu?: boolean;
}) {
  return (
    <div className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-md">
      <p className="font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
        {baslik}
      </p>
      <p
        className={`mt-base font-headline text-headline-md tabular-nums ${
          vurgulu ? 'text-tertiary' : 'text-on-surface'
        }`}
      >
        {deger}
      </p>
    </div>
  );
}

function SiradakiEtkinlik({ bilet }: { bilet: Bilet }) {
  const baslangic = new Date(bilet.eventStartsAtUtc);
  const kalanGun = Math.ceil((baslangic.getTime() - Date.now()) / 86_400_000);

  return (
    <section className="mt-stack-md overflow-hidden rounded-lg border border-outline-variant/40">
      <div
        aria-hidden="true"
        className="h-1 w-full bg-gradient-to-r from-[#3b1b92] via-[#aa2484] to-[#fd2d6e]"
      />

      <div className="flex flex-wrap items-center justify-between gap-stack-sm bg-surface-variant/20 p-stack-md">
        <div className="min-w-0">
          <p className="font-body text-label-caps uppercase tracking-widest text-on-surface-variant">
            Sıradaki etkinliğin
          </p>
          <h2 className="mt-base font-headline text-title-lg text-on-surface">
            {bilet.eventTitle}
          </h2>
          <p className="font-body text-body-sm text-on-surface-variant">
            {tarihBicimi.format(baslangic)} · {bilet.venueName} · {bilet.seatLabel}
          </p>
        </div>

        <div className="flex shrink-0 items-center gap-stack-sm">
          {/* Gun sayisi metin olarak: "2 gün kaldı" bir tarihten daha
              hizli okunuyor ve tarih zaten hemen yaninda duruyor. */}
          <span className="font-body text-body-sm text-primary">
            {kalanGun <= 0 ? 'Bugün' : kalanGun === 1 ? 'Yarın' : `${kalanGun} gün kaldı`}
          </span>

          <Link
            to="/biletlerim"
            className="inline-flex items-center gap-base rounded-full bg-primary px-stack-md py-stack-sm font-body text-body-sm font-semibold text-on-primary"
          >
            <BiletIkonu className="h-4 w-4" />
            Bileti göster
          </Link>
        </div>
      </div>
    </section>
  );
}

function HizliErisim({ organizatorMu, adminMi }: { organizatorMu: boolean; adminMi: boolean }) {
  // Role gore: gidemeyecegi bir yeri gostermek, tikladiginda 403
  // gormesi demek olurdu.
  const baglantilar = [
    { yol: '/biletlerim', metin: 'Biletlerim', aciklama: 'QR kodun ve indirme', gorunur: true },
    {
      yol: '/rezervasyonlarim',
      metin: 'Rezervasyonlarım',
      aciklama: 'Bekleyen ve geçmiş',
      gorunur: true,
    },
    { yol: '/', metin: 'Etkinlik keşfet', aciklama: 'Yaklaşan etkinlikler', gorunur: true },
    {
      yol: '/etkinlikler/yeni',
      metin: 'Etkinlik oluştur',
      aciklama: 'Yeni etkinlik ve seans',
      gorunur: organizatorMu,
    },
    { yol: '/kapi', metin: 'Kapı okutma', aciklama: 'Girişte QR doğrula', gorunur: organizatorMu },
    { yol: '/yonetim', metin: 'Yönetim paneli', aciklama: 'Ödemeler ve ayarlar', gorunur: adminMi },
  ].filter((baglanti) => baglanti.gorunur);

  return (
    <section className="mt-stack-lg">
      <h2 className="mb-stack-sm font-body text-label-caps uppercase tracking-widest text-on-surface-variant">
        Hızlı erişim
      </h2>

      <div className="grid gap-stack-sm sm:grid-cols-2 lg:grid-cols-3">
        {baglantilar.map((baglanti) => (
          <Link
            key={baglanti.yol}
            to={baglanti.yol}
            className="group flex items-center justify-between gap-base rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-sm transition-colors hover:border-primary/50 hover:bg-surface-container-high"
          >
            <span className="min-w-0">
              <span className="block font-body text-body-md text-on-surface">
                {baglanti.metin}
              </span>
              <span className="block font-body text-body-sm text-on-surface-variant">
                {baglanti.aciklama}
              </span>
            </span>

            <SagOkIkonu className="h-4 w-4 shrink-0 text-on-surface-variant transition-transform group-hover:translate-x-1" />
          </Link>
        ))}
      </div>
    </section>
  );
}

function HesapBilgileri({
  kullanici,
  roller,
}: {
  kullanici: { fullName: string; email: string; emailConfirmed?: boolean } | null | undefined;
  roller: string[];
}) {
  return (
    <section className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-md">
      <h2 className="mb-stack-sm font-headline text-title-lg text-on-surface">Hesap bilgileri</h2>

      <dl className="space-y-stack-sm">
        <div>
          <dt className="font-body text-body-sm text-on-surface-variant">Ad soyad</dt>
          <dd className="font-body text-body-md text-on-surface">{kullanici?.fullName ?? '—'}</dd>
        </div>

        <div>
          <dt className="font-body text-body-sm text-on-surface-variant">E-posta</dt>
          <dd className="break-all font-body text-body-md text-on-surface">
            {kullanici?.email ?? '—'}
          </dd>
        </div>

        <div>
          <dt className="font-body text-body-sm text-on-surface-variant">Roller</dt>
          <dd className="font-body text-body-md text-on-surface">
            {roller.map((rol) => ROL_METNI[rol] ?? rol).join(', ') || '—'}
          </dd>
        </div>
      </dl>
    </section>
  );
}

/**
 * Sifre degistirme.
 *
 * Ayri bir sayfa yerine burada: kullanicinin sifresini degistirmek icin
 * gidecegi baska bir yer yoktu — <c>/sifremi-unuttum</c> akisi oturumu
 * OLMAYAN kullanici icin ve oturumu acik biri icin posta beklemek
 * gereksiz bir adim.
 */
function SifreDegistir() {
  const [mevcut, setMevcut] = useState('');
  const [yeni, setYeni] = useState('');
  const [yeniTekrar, setYeniTekrar] = useState('');
  const [tamam, setTamam] = useState(false);
  const [hata, setHata] = useState<unknown>(null);
  const [gonderiliyor, setGonderiliyor] = useState(false);

  // Iki alanin ayni olup olmadigi ISTEMCIDE kontrol ediliyor: sunucunun
  // ikinci alandan haberi yok ve olmasi da gerekmiyor.
  const eslesmiyor = yeniTekrar.length > 0 && yeni !== yeniTekrar;

  const gonder = async (olay: React.FormEvent) => {
    olay.preventDefault();

    if (eslesmiyor) return;

    setTamam(false);
    setHata(null);
    setGonderiliyor(true);

    try {
      await sifreDegistir(mevcut, yeni);
      setTamam(true);
      setMevcut('');
      setYeni('');
      setYeniTekrar('');
    } catch (yakalanan) {
      setHata(yakalanan);
    } finally {
      setGonderiliyor(false);
    }
  };

  const alanlar = alanHatalari(hata);

  return (
    <section className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 p-stack-md">
      <h2 className="font-headline text-title-lg text-on-surface">Şifre değiştir</h2>
      <p className="mt-base font-body text-body-sm text-on-surface-variant">
        Şifreni değiştirdiğinde açık olan tüm oturumların kapanır.
      </p>

      <form onSubmit={gonder} className="mt-stack-sm space-y-stack-sm" noValidate>
        <TextField
          etiket="Mevcut şifre"
          type="password"
          value={mevcut}
          onChange={(olay) => setMevcut(olay.target.value)}
          hata={alanlar.CurrentPassword?.[0]}
          autoComplete="current-password"
        />

        <TextField
          etiket="Yeni şifre"
          type="password"
          value={yeni}
          onChange={(olay) => setYeni(olay.target.value)}
          hata={alanlar.NewPassword?.[0]}
          ipucu="En az 8 karakter, bir harf ve bir rakam."
          autoComplete="new-password"
        />

        <TextField
          etiket="Yeni şifre (tekrar)"
          type="password"
          value={yeniTekrar}
          onChange={(olay) => setYeniTekrar(olay.target.value)}
          hata={eslesmiyor ? 'Şifreler eşleşmiyor.' : undefined}
          autoComplete="new-password"
        />

        {hata !== null && Object.keys(alanlar).length === 0 && (
          <p
            role="alert"
            className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
          >
            {hataMesaji(hata, 'Şifre değiştirilemedi.')}
          </p>
        )}

        {tamam && (
          <p
            role="status"
            className="flex items-center gap-base rounded-md border border-primary/40 bg-primary-container/15 px-stack-sm py-base font-body text-body-sm text-primary"
          >
            <OnayIkonu className="h-4 w-4 shrink-0" />
            Şifren değiştirildi.
          </p>
        )}

        <Button
          type="submit"
          yukleniyor={gonderiliyor}
          disabled={!mevcut || !yeni || eslesmiyor}
        >
          Şifreyi değiştir
        </Button>
      </form>
    </section>
  );
}
