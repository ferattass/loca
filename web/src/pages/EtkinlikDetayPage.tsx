import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';

import { dosyaAdresi, hataKodu, hataMesaji } from '../api/client';
import {
  etkinlikDetayiGetir,
  type EtkinlikDetayi,
  type OturumOzeti,
} from '../api/eventCatalog';
import {
  KisiIkonu,
  KonumIkonu,
  SaatIkonu,
  TakvimIkonu,
  UyariIkonu,
} from '../components/ui/Ikon';
import { Uyari } from '../components/ui/Uyari';
import { para, paraKurussuz, saatBicimi, tamTarihSaatBicimi } from '../lib/bicim';

// Seans kutusundaki gun/ay. Ay adi Intl'den kucuk harfle geliyor; buyuk
// harfe CSS ile degil toLocaleUpperCase('tr-TR') ile ceviriliyor, cunku
// tarayici locale bilmeden uppercase yapinca Turkce "i" noktasini
// kaybediyor.
const gunBicimi = new Intl.DateTimeFormat('tr-TR', { day: '2-digit' });
const ayBicimi = new Intl.DateTimeFormat('tr-TR', { month: 'short' });

/**
 * Etkinlik detayi.
 *
 * <b>Bu sayfa yoktu ve eksikligi gorunmuyordu.</b> Etkinlik karti
 * <c>/etkinlikler/:id</c> adresine baglaniyordu ama o rota hic tanimli
 * degildi; tanimsiz her adres <c>*</c> kuralina dusup sessizce ana sayfaya
 * yonlendiriliyordu. Yani "Bilet al"a basan kullanici hata gormuyor, ana
 * sayfaya donuyordu — kirik bir baglanti, calisan bir baglanti gibi
 * davraniyordu.
 *
 * <para>
 * Satin alma yolu buradan geciyor: kullanici hangi OTURUMA bilet aldigini
 * secmeden koltuk secemez. Kart dogrudan koltuk ekranina baglansaydi cok
 * oturumlu bir etkinlikte hangi gunun koltuklarini sectigi belirsiz
 * kalirdi.
 * </para>
 *
 * <para>
 * <b>Duzen kahraman bolumu uzerine kuruldu.</b> Once sayfa bastan sona duz
 * metindi: afis hic gosterilmiyordu (kesfet kartinda gosterilmesine ragmen),
 * baslik govde metniyle ayni boyuttaydi ve "bilet al" diye bir cikis yoktu —
 * kullanici satin almak icin once seans listesini okumak zorundaydi. Afis
 * artik hem kartta hem arkada bulanik zemin olarak duruyor ve birincil
 * eylem ilk ekranda.
 * </para>
 */
export function EtkinlikDetayPage() {
  const { id } = useParams<{ id: string }>();

  // Afis silinmis ya da adres bozuksa alan tamamen kapaniyor; kirik gorsel
  // ikonu gostermenin faydasi yok. Ucretsiz barindirma katmaninda dosya
  // sistemi gecici oldugu icin bu gercekten olabiliyor.
  const [afisBozuk, setAfisBozuk] = useState(false);

  const { data, isPending, isError, error } = useQuery<EtkinlikDetayi>({
    queryKey: ['etkinlik', id],
    queryFn: () => etkinlikDetayiGetir(id!),
    enabled: Boolean(id),
  });

  if (isPending) {
    return (
      <div className="mx-auto max-w-5xl animate-pulse px-container-margin-mobile py-stack-lg">
        <div className="grid gap-stack-md sm:grid-cols-[13rem_minmax(0,1fr)]">
          <div className="aspect-[3/4] rounded-lg bg-surface-variant/40" />
          <div className="space-y-stack-sm">
            <div className="h-4 w-24 rounded-md bg-surface-variant/40" />
            <div className="h-10 w-2/3 rounded-md bg-surface-variant/40" />
            <div className="h-4 w-1/2 rounded-md bg-surface-variant/40" />
            <div className="h-4 w-1/3 rounded-md bg-surface-variant/40" />
            <div className="h-12 w-40 rounded-full bg-surface-variant/40" />
          </div>
        </div>
      </div>
    );
  }

  if (isError) {
    // 404 ile gercek bir hata ayri seyler: birincisinde kullanicinin
    // yapabilecegi tek sey listeye donmek, ikincisinde tekrar denemek.
    const bulunamadi = hataKodu(error) === 'Event.NotFound';

    return (
      <div className="mx-auto max-w-2xl px-container-margin-mobile py-stack-lg text-center">
        <h1 className="font-headline text-headline-md text-on-surface">
          {bulunamadi ? 'Etkinlik bulunamadı' : 'Etkinlik yüklenemedi'}
        </h1>
        <p className="mt-base font-body text-body-md text-on-surface-variant">
          {bulunamadi
            ? 'Bu etkinlik kaldırılmış ya da adres yanlış olabilir.'
            : hataMesaji(error, 'Beklenmeyen bir hata oluştu.')}
        </p>
        <Link
          to="/"
          className="mt-stack-md inline-flex rounded-full bg-primary px-stack-md py-stack-sm font-body text-body-md font-semibold text-on-primary"
        >
          Etkinliklere dön
        </Link>
      </div>
    );
  }

  const iptalEdildi = data.status === 'Cancelled';
  const satisaKapali = data.status === 'SalesClosed' || data.status === 'Completed';
  const satilabilir = !iptalEdildi && !satisaKapali;

  // Iptal edilen oturum listede GOSTERILMIYOR: kullanici tiklayip koltuk
  // ekraninda bos bir salonla karsilasmasin.
  const oturumlar = data.sessions
    .filter((oturum) => oturum.status !== 'Cancelled')
    .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime());

  const acikOturumlar = oturumlar.filter((oturum) => oturumDurumu(oturum, satilabilir).acik);

  const aktifTurler = data.ticketTypes.filter((tur) => tur.isActive);

  const enDusukFiyat = aktifTurler.length
    ? Math.min(...aktifTurler.map((tur) => tur.price))
    : null;

  const afis = afisBozuk ? null : dosyaAdresi(data.posterFileId);

  return (
    <div className="mx-auto max-w-5xl px-container-margin-mobile py-stack-md">
      <nav aria-label="Konum" className="mb-stack-sm font-body text-body-sm text-on-surface-variant">
        <Link to="/" className="hover:text-on-surface">
          Etkinlikler
        </Link>
        <span aria-hidden="true"> / </span>
        <span>{data.categoryName}</span>
      </nav>

      <section className="relative overflow-hidden rounded-xl border border-white/5">
        {/* Afisin bulanik kopyasi zemin oluyor: tek gorselden sayfaya
            etkinlige ozel bir renk geliyor ve kart ile arka plan arasinda
            kontrast doguyor. Tamamen dekoratif — ayni gorsel yaninda zaten
            net hâliyle duruyor. */}
        <div aria-hidden="true" className="absolute inset-0">
          {afis ? (
            <img src={afis} alt="" className="h-full w-full scale-110 object-cover blur-2xl" />
          ) : (
            <div className="h-full w-full bg-loca-degrade opacity-40" />
          )}
          <div className="absolute inset-0 bg-gradient-to-t from-surface via-surface/90 to-surface/70" />
        </div>

        {/* Afis yoksa tek sutun: iki sutunlu izgara korunsaydi solda 13rem
            genisliginde bos bir alan kalir, baslik ise gereksiz yere dar bir
            sutuna sikisip uc satira bolunurdu. */}
        <div
          className={`relative grid gap-stack-md p-stack-md ${
            afis ? 'sm:grid-cols-[13rem_minmax(0,1fr)]' : ''
          }`}
        >
          {afis && (
            <div className="mx-auto w-40 shrink-0 sm:mx-0 sm:w-full">
              <img
                src={afis}
                // Afis DEKORATIF: etkinligin adi hemen yaninda yaziyor ve
                // gorseli ayrica tarif etmek ekran okuyucuda ayni bilgiyi
                // iki kez okuturdu.
                alt=""
                onError={() => setAfisBozuk(true)}
                className="aspect-[3/4] w-full rounded-lg object-cover shadow-glow-primary"
              />
            </div>
          )}

          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-base">
              <span className="rounded-full bg-primary/15 px-stack-sm py-[3px] font-body text-label-caps text-primary">
                {data.categoryName}
              </span>

              {data.minimumAge !== null && data.minimumAge > 0 && (
                <span className="inline-flex items-center gap-1 rounded-full border border-outline-variant px-stack-sm py-[3px] font-body text-body-sm text-on-surface-variant">
                  <KisiIkonu className="h-3.5 w-3.5" />
                  {data.minimumAge}+ yaş
                </span>
              )}
            </div>

            <h1 className="mt-stack-sm font-headline text-display-lg-mobile text-on-surface md:text-display-lg">
              {data.title}
            </h1>

            <dl className="mt-stack-sm space-y-base font-body text-body-md text-on-surface-variant">
              <div className="flex items-start gap-base">
                <dt className="sr-only">Tarih</dt>
                <TakvimIkonu className="mt-1 h-4 w-4 shrink-0 text-primary" />
                <dd className="text-on-surface">
                  {tamTarihSaatBicimi.format(new Date(data.eventDateUtc))}
                </dd>
              </div>

              <div className="flex items-start gap-base">
                <dt className="sr-only">Yer</dt>
                <KonumIkonu className="mt-1 h-4 w-4 shrink-0 text-primary" />
                <dd>
                  {data.venueName} · {data.hallName} · {data.cityName}
                </dd>
              </div>

              <div className="flex items-start gap-base">
                <dt className="sr-only">Süre</dt>
                <SaatIkonu className="mt-1 h-4 w-4 shrink-0 text-primary" />
                <dd>{data.durationMinutes} dakika</dd>
              </div>
            </dl>

            <div className="mt-stack-md flex flex-wrap items-center gap-stack-sm">
              {/* Tek acik seans varsa aradaki adim gereksiz: kullanici
                  dogrudan koltuk ekranina gidiyor. Birden fazlaysa once
                  hangi gune bilet aldigini secmeli, buton listeye
                  goturuyor. */}
              {acikOturumlar.length === 1 ? (
                <Link
                  to={`/oturumlar/${acikOturumlar[0].id}/koltuklar`}
                  className="rounded-full bg-primary px-stack-md py-stack-sm font-body text-body-md font-semibold text-on-primary shadow-glow-primary transition-shadow hover:shadow-glow-primary-lg"
                >
                  Koltuk seç
                </Link>
              ) : acikOturumlar.length > 1 ? (
                <a
                  href="#seanslar"
                  className="rounded-full bg-primary px-stack-md py-stack-sm font-body text-body-md font-semibold text-on-primary shadow-glow-primary transition-shadow hover:shadow-glow-primary-lg"
                >
                  Seans seç ({acikOturumlar.length})
                </a>
              ) : null}

              {enDusukFiyat !== null && (
                <p className="font-body text-body-sm text-on-surface-variant">
                  <span className="text-on-surface-variant/70">başlangıç </span>
                  <strong className="font-headline text-title-lg text-on-surface">
                    {paraKurussuz(enDusukFiyat, aktifTurler[0].currency)}
                  </strong>
                </p>
              )}
            </div>

            <p className="mt-stack-sm font-body text-body-sm text-on-surface-variant/80">
              Düzenleyen: {data.organizerName}
            </p>
          </div>
        </div>
      </section>

      {iptalEdildi && (
        <Uyari
          role="alert"
          boyut="md"
          className="mt-stack-md"
          ikon={<UyariIkonu className="mt-[2px] h-5 w-5 shrink-0" />}
        >
          <span>
            Bu etkinlik iptal edildi.
            {data.cancellationReason ? ` Sebep: ${data.cancellationReason}` : ''}
          </span>
        </Uyari>
      )}

      {!iptalEdildi && satisaKapali && (
        <p className="mt-stack-md rounded-md border border-outline-variant bg-surface-variant/20 px-stack-sm py-stack-sm font-body text-body-md text-on-surface-variant">
          Bu etkinlik için bilet satışı kapandı.
        </p>
      )}

      <div className="mt-stack-md grid gap-stack-md lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="space-y-stack-md">
          <section id="seanslar" className="glass rounded-lg p-stack-md">
            <h2 className="mb-stack-sm font-headline text-headline-md text-on-surface">Seanslar</h2>

            {oturumlar.length === 0 ? (
              <p className="font-body text-body-md text-on-surface-variant">
                Bu etkinlik için henüz seans açılmadı.
              </p>
            ) : (
              <ul className="space-y-base">
                {oturumlar.map((oturum) => (
                  <OturumSatiri key={oturum.id} oturum={oturum} satilabilir={satilabilir} />
                ))}
              </ul>
            )}
          </section>

          {data.description && (
            <section className="glass rounded-lg p-stack-md">
              <h2 className="mb-stack-sm font-headline text-headline-md text-on-surface">
                Etkinlik hakkında
              </h2>
              <p className="whitespace-pre-line font-body text-body-md leading-relaxed text-on-surface-variant">
                {data.description}
              </p>
            </section>
          )}

          {data.cancellationPolicy && (
            <section className="glass rounded-lg p-stack-md">
              <h2 className="mb-stack-sm font-headline text-headline-md text-on-surface">
                İptal ve iade
              </h2>
              <p className="whitespace-pre-line font-body text-body-sm leading-relaxed text-on-surface-variant">
                {data.cancellationPolicy}
              </p>
            </section>
          )}
        </div>

        {aktifTurler.length > 0 && (
          // Bilet turleri tablodan listeye alindi: uc sutunlu tablo dar
          // ekranda yatay kaydirma gerektiriyordu ve satirlarin tamami
          // ayni agirlikta okunuyordu. Onemli olan fiyat, artik satirin
          // sagindaki tek vurgulu deger.
          <aside className="glass h-fit rounded-lg p-stack-md lg:sticky lg:top-stack-md">
            <h2 className="mb-stack-sm font-headline text-headline-md text-on-surface">
              Bilet türleri
            </h2>

            <ul className="space-y-stack-sm">
              {aktifTurler.map((tur) => (
                <li
                  key={tur.id}
                  className="flex items-start justify-between gap-stack-sm border-b border-outline-variant/20 pb-stack-sm last:border-0 last:pb-0"
                >
                  <div className="min-w-0">
                    <p className="font-body text-body-md text-on-surface">{tur.name}</p>
                    <p className="font-body text-body-sm text-on-surface-variant">
                      {tur.seatSectionName ?? 'Tüm salon'}
                    </p>
                    {tur.requiresVerification && (
                      <span className="mt-1 inline-flex rounded-full border border-tertiary/50 px-base py-[1px] font-body text-[11px] text-tertiary">
                        Belge gerekli
                      </span>
                    )}
                  </div>

                  <p className="shrink-0 font-headline text-title-lg tabular-nums text-on-surface">
                    {para(tur.price, tur.currency)}
                  </p>
                </li>
              ))}
            </ul>
          </aside>
        )}
      </div>
    </div>
  );
}

/**
 * Bir oturumun satisa acik olup olmadigi ve degilse sebebi.
 *
 * Hem seans satiri hem kahraman bolumundeki birincil buton ayni karari
 * veriyor; iki yerde hesaplansaydi biri "koltuk sec" derken digeri satisi
 * kapali gosterebilirdi.
 */
function oturumDurumu(oturum: OturumOzeti, satilabilir: boolean) {
  const simdi = Date.now();
  const satisBasi = new Date(oturum.salesStartsAtUtc).getTime();
  const satisSonu = new Date(oturum.salesEndsAtUtc).getTime();

  // Koltuk uretilmemis bir oturumda secilecek koltuk YOK: etkinlik
  // yayina alindiginda uretiliyorlar. Baglanti acik birakilsaydi
  // kullanici bos bir salon gorurdu.
  const acik = satilabilir && oturum.seatsGenerated && simdi >= satisBasi && simdi < satisSonu;

  const kapaliSebep = !oturum.seatsGenerated
    ? 'Henüz satışa açılmadı'
    : simdi < satisBasi
      ? `Satış ${tamTarihSaatBicimi.format(new Date(oturum.salesStartsAtUtc))} tarihinde başlıyor`
      : simdi >= satisSonu
        ? 'Satış kapandı'
        : 'Satışa kapalı';

  return { acik, kapaliSebep };
}

function OturumSatiri({ oturum, satilabilir }: { oturum: OturumOzeti; satilabilir: boolean }) {
  const { acik, kapaliSebep } = oturumDurumu(oturum, satilabilir);
  const baslangic = new Date(oturum.startsAtUtc);

  const govde = (
    <>
      {/* Takvim yapragi: listedeki seanslar arasinda goz once gune, sonra
          saate gidiyor. Duz metin satirinda ikisi ayni agirliktaydi. */}
      <div className="flex h-14 w-14 shrink-0 flex-col items-center justify-center rounded-md bg-primary/15 text-primary">
        <span className="font-headline text-title-lg leading-none">
          {gunBicimi.format(baslangic)}
        </span>
        <span className="font-body text-[11px] leading-tight">
          {ayBicimi.format(baslangic).toLocaleUpperCase('tr-TR')}
        </span>
      </div>

      <div className="min-w-0 flex-1">
        <p className="font-body text-body-md text-on-surface">
          {tamTarihSaatBicimi.format(baslangic)}
        </p>
        <p className="font-body text-body-sm text-on-surface-variant">
          {saatBicimi.format(baslangic)} – {saatBicimi.format(new Date(oturum.endsAtUtc))} ·{' '}
          {oturum.hallName}
        </p>
      </div>

      {acik ? (
        <span className="shrink-0 rounded-full bg-primary px-stack-md py-stack-sm font-body text-body-md font-semibold text-on-primary">
          Koltuk seç
        </span>
      ) : (
        <span className="shrink-0 font-body text-body-sm text-on-surface-variant">
          {kapaliSebep}
        </span>
      )}
    </>
  );

  const ortakSinif =
    'flex flex-wrap items-center gap-stack-sm rounded-lg border p-stack-sm';

  return (
    <li>
      {acik ? (
        <Link
          to={`/oturumlar/${oturum.id}/koltuklar`}
          className={`${ortakSinif} border-outline-variant/40 bg-surface-container-low/60 transition-colors hover:border-primary/60 hover:bg-surface-container-high`}
        >
          {govde}
        </Link>
      ) : (
        // Kapali oturum BAGLANTI DEGIL: tiklanabilir gorunup hicbir sey
        // yapmayan bir oge, kullaniciya arayuzun bozuk oldugunu
        // dusundururdu.
        <div className={`${ortakSinif} border-outline-variant/25 bg-surface-variant/10 opacity-70`}>
          {govde}
        </div>
      )}
    </li>
  );
}
