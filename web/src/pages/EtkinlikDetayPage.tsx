import { Link, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';

import { hataKodu, hataMesaji } from '../api/client';
import {
  etkinlikDetayiGetir,
  type EtkinlikDetayi,
  type OturumOzeti,
} from '../api/eventCatalog';
import { UyariIkonu } from '../components/ui/Ikon';
import { para } from '../lib/bicim';

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'full',
  timeStyle: 'short',
});

const saatBicimi = new Intl.DateTimeFormat('tr-TR', { timeStyle: 'short' });

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
 */
export function EtkinlikDetayPage() {
  const { id } = useParams<{ id: string }>();

  const { data, isPending, isError, error } = useQuery<EtkinlikDetayi>({
    queryKey: ['etkinlik', id],
    queryFn: () => etkinlikDetayiGetir(id!),
    enabled: Boolean(id),
  });

  if (isPending) {
    return (
      <div className="mx-auto max-w-4xl animate-pulse space-y-stack-md px-container-margin-mobile py-stack-lg">
        <div className="h-8 w-2/3 rounded-md bg-surface-variant/40" />
        <div className="h-4 w-1/3 rounded-md bg-surface-variant/40" />
        <div className="h-40 rounded-lg bg-surface-variant/40" />
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

  // Iptal edilen oturum listede GOSTERILMIYOR: kullanici tiklayip koltuk
  // ekraninda bos bir salonla karsilasmasin.
  const oturumlar = data.sessions
    .filter((oturum) => oturum.status !== 'Cancelled')
    .sort((a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime());

  const aktifTurler = data.ticketTypes.filter((tur) => tur.isActive);

  const enDusukFiyat = aktifTurler.length
    ? Math.min(...aktifTurler.map((tur) => tur.price))
    : null;

  return (
    <div className="mx-auto max-w-4xl px-container-margin-mobile py-stack-lg">
      <nav aria-label="Konum" className="mb-stack-sm font-body text-body-sm text-on-surface-variant">
        <Link to="/" className="hover:text-on-surface">
          Etkinlikler
        </Link>
        <span aria-hidden="true"> / </span>
        <span>{data.categoryName}</span>
      </nav>

      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-lg text-on-surface">{data.title}</h1>

        <p className="mt-base font-body text-body-md text-on-surface-variant">
          {data.venueName} · {data.hallName} · {data.cityName}
        </p>

        <p className="mt-base font-body text-body-md text-on-surface">
          {tarihBicimi.format(new Date(data.eventDateUtc))}
          <span className="text-on-surface-variant"> · {data.durationMinutes} dk</span>
        </p>

        {/* Sifirdan buyukse gosteriliyor: "0+ yas" hicbir sey soylemiyor
            ve alani, gercekten yas siniri olan etkinliklerde tasidigi
            anlamdan da ediyor. */}
        {data.minimumAge !== null && data.minimumAge > 0 && (
          <p className="mt-base inline-flex rounded-full border border-outline-variant px-stack-sm py-[2px] font-body text-body-sm text-on-surface-variant">
            {data.minimumAge}+ yaş
          </p>
        )}
      </header>

      {iptalEdildi && (
        <p
          role="alert"
          className="mb-stack-md flex items-start gap-base rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-md text-error"
        >
          <UyariIkonu className="mt-[2px] h-5 w-5 shrink-0" />
          <span>
            Bu etkinlik iptal edildi.
            {data.cancellationReason ? ` Sebep: ${data.cancellationReason}` : ''}
          </span>
        </p>
      )}

      {!iptalEdildi && satisaKapali && (
        <p className="mb-stack-md rounded-md border border-outline-variant bg-surface-variant/20 px-stack-sm py-stack-sm font-body text-body-md text-on-surface-variant">
          Bu etkinlik için bilet satışı kapandı.
        </p>
      )}

      {data.description && (
        <section className="mb-stack-lg">
          <h2 className="mb-base font-headline text-title-lg text-on-surface">Etkinlik hakkında</h2>
          <p className="whitespace-pre-line font-body text-body-md leading-relaxed text-on-surface-variant">
            {data.description}
          </p>
        </section>
      )}

      <section className="mb-stack-lg">
        <div className="mb-stack-sm flex flex-wrap items-baseline justify-between gap-base">
          <h2 className="font-headline text-title-lg text-on-surface">Seans seç</h2>

          {enDusukFiyat !== null && (
            <p className="font-body text-body-sm text-on-surface-variant">
              {para(enDusukFiyat, aktifTurler[0].currency)}'den başlayan fiyatlarla
            </p>
          )}
        </div>

        {oturumlar.length === 0 ? (
          <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-md py-stack-md font-body text-body-md text-on-surface-variant">
            Bu etkinlik için henüz seans açılmadı.
          </p>
        ) : (
          <ul className="space-y-base">
            {oturumlar.map((oturum) => (
              <OturumSatiri
                key={oturum.id}
                oturum={oturum}
                satilabilir={!iptalEdildi && !satisaKapali}
              />
            ))}
          </ul>
        )}
      </section>

      {aktifTurler.length > 0 && (
        <section className="mb-stack-lg">
          <h2 className="mb-stack-sm font-headline text-title-lg text-on-surface">Bilet türleri</h2>

          <div className="overflow-x-auto">
            <table className="w-full min-w-[32rem] border-collapse text-left">
              <thead>
                <tr className="border-b border-outline-variant/40">
                  <th className="py-base font-body text-body-sm font-semibold text-on-surface-variant">
                    Tür
                  </th>
                  <th className="py-base font-body text-body-sm font-semibold text-on-surface-variant">
                    Bölüm
                  </th>
                  <th className="py-base text-right font-body text-body-sm font-semibold text-on-surface-variant">
                    Fiyat
                  </th>
                </tr>
              </thead>
              <tbody>
                {aktifTurler.map((tur) => (
                  <tr key={tur.id} className="border-b border-outline-variant/20">
                    <td className="py-stack-sm font-body text-body-md text-on-surface">
                      {tur.name}
                      {tur.requiresVerification && (
                        <span className="ml-base rounded-full border border-tertiary/50 px-base py-[1px] font-body text-[11px] text-tertiary">
                          Belge gerekli
                        </span>
                      )}
                    </td>
                    <td className="py-stack-sm font-body text-body-md text-on-surface-variant">
                      {tur.seatSectionName ?? 'Tüm salon'}
                    </td>
                    <td className="py-stack-sm text-right font-body text-body-md tabular-nums text-on-surface">
                      {para(tur.price, tur.currency)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {data.cancellationPolicy && (
        <section>
          <h2 className="mb-base font-headline text-title-lg text-on-surface">İptal ve iade</h2>
          <p className="whitespace-pre-line font-body text-body-sm leading-relaxed text-on-surface-variant">
            {data.cancellationPolicy}
          </p>
        </section>
      )}
    </div>
  );
}

function OturumSatiri({ oturum, satilabilir }: { oturum: OturumOzeti; satilabilir: boolean }) {
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
      ? `Satış ${tarihBicimi.format(new Date(oturum.salesStartsAtUtc))} tarihinde başlıyor`
      : simdi >= satisSonu
        ? 'Satış kapandı'
        : 'Satışa kapalı';

  const govde = (
    <>
      <div className="min-w-0">
        <p className="font-body text-body-lg text-on-surface">
          {tarihBicimi.format(new Date(oturum.startsAtUtc))}
        </p>
        <p className="font-body text-body-sm text-on-surface-variant">
          {saatBicimi.format(new Date(oturum.startsAtUtc))} –{' '}
          {saatBicimi.format(new Date(oturum.endsAtUtc))} · {oturum.hallName}
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
    'flex flex-wrap items-center justify-between gap-stack-sm rounded-lg border p-stack-sm';

  return (
    <li>
      {acik ? (
        <Link
          to={`/oturumlar/${oturum.id}/koltuklar`}
          className={`${ortakSinif} border-outline-variant/40 bg-surface-variant/20 transition-colors hover:border-primary/60 hover:bg-surface-container-high`}
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
