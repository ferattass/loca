import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { hataMesaji } from '../../api/client';
import {
  adminOdemeleriGetir,
  odemeIadeEt,
  type AdminOdeme,
  type SayfaliSonuc,
} from '../../api/admin';
import { havaleOnayla, havaleReddet, type OdemeDurumu } from '../../api/payments';
import { SolOkIkonu, SagOkIkonu } from '../../components/ui/Ikon';
import { para } from '../../lib/bicim';

const tarihBicimi = new Intl.DateTimeFormat('tr-TR', {
  dateStyle: 'short',
  timeStyle: 'short',
});

const DURUM_METNI: Record<OdemeDurumu, string> = {
  Pending: 'Bekliyor',
  Succeeded: 'Başarılı',
  Failed: 'Başarısız',
  Refunded: 'İade edildi',
  Cancelled: 'İptal',
};

const DURUM_RENGI: Record<OdemeDurumu, string> = {
  Pending: 'border-outline text-on-surface-variant',
  Succeeded: 'border-primary/50 text-primary',
  Failed: 'border-error/50 text-error',
  Refunded: 'border-tertiary/50 text-tertiary',
  Cancelled: 'border-outline text-on-surface-variant',
};

const DURUMLAR: OdemeDurumu[] = ['Pending', 'Succeeded', 'Failed', 'Refunded', 'Cancelled'];

/**
 * Odeme yonetimi.
 *
 * <b>Arama kutusu ad, e-posta ve saglayici referansinda birlikte ariyor.</b>
 * Ayri alanlar konsaydi yonetici her seferinde "bu elimdeki ne" diye karar
 * vermek zorunda kalirdi; pratikte elinde ya bir isim ya bir referans
 * oluyor ve nereye yazacagini dusunmemesi gerekiyor.
 */
export function OdemelerPage() {
  const queryClient = useQueryClient();

  const [durum, setDurum] = useState<OdemeDurumu | undefined>();
  const [arama, setArama] = useState('');
  const [aktifArama, setAktifArama] = useState('');
  const [sayfa, setSayfa] = useState(1);
  const [iadeEdilen, setIadeEdilen] = useState<AdminOdeme | null>(null);

  /**
   * "Onay bekleyen havaleler" tek dugmeye bagli.
   *
   * Yoneticinin gunluk isi bu liste: bankaya bakip gelen parayi
   * isaretlemek. Her seferinde durum + yontem suzgeclerini elle kurmasi
   * gerekseydi is her gun bir kac tiklama pahalilasirdi.
   */
  const [yalnizBekleyenHavale, setYalnizBekleyenHavale] = useState(false);

  const etkinDurum = yalnizBekleyenHavale ? 'Pending' : durum;
  const etkinYontem = yalnizBekleyenHavale ? ('BankTransfer' as const) : undefined;

  const { data, isPending, isError, error } = useQuery<SayfaliSonuc<AdminOdeme>>({
    queryKey: ['admin-payments', etkinDurum, etkinYontem, aktifArama, sayfa],
    queryFn: () =>
      adminOdemeleriGetir({
        status: etkinDurum,
        method: etkinYontem,
        search: aktifArama,
        pageNumber: sayfa,
      }),
  });

  const [havaleKarari, setHavaleKarari] = useState<{
    odeme: AdminOdeme;
    onay: boolean;
  } | null>(null);

  const havale = useMutation({
    mutationFn: ({ id, onay, metin }: { id: string; onay: boolean; metin: string }) =>
      onay ? havaleOnayla(id, metin) : havaleReddet(id, metin),
    onSuccess: async () => {
      setHavaleKarari(null);
      // Ozet de guncelleniyor: onaylanan havale gunluk satisa giriyor.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin-payments'] }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
    },
  });

  const iade = useMutation({
    mutationFn: ({ id, sebep }: { id: string; sebep: string }) => odemeIadeEt(id, sebep),
    onSuccess: async () => {
      setIadeEdilen(null);
      // Ozet de guncelleniyor: iade tutari orada da gorunuyor.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['admin-payments'] }),
        queryClient.invalidateQueries({ queryKey: ['admin-overview'] }),
      ]);
    },
  });

  const filtreDegistir = (yeni: OdemeDurumu | undefined) => {
    setYalnizBekleyenHavale(false);
    setDurum(yeni);
    // Sayfa basa aliniyor: uc numarali sayfadayken filtre degistiginde
    // yeni sonuc kumesinde uc numarali sayfa olmayabilir ve ekran bos
    // gorunurdu.
    setSayfa(1);
  };

  const aramaGonder = (olay: React.FormEvent) => {
    olay.preventDefault();
    setAktifArama(arama);
    setSayfa(1);
  };

  return (
    <div className="mx-auto max-w-6xl">
      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-md text-on-surface">Ödemeler</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          {data ? `${data.totalCount} kayıt` : 'Tüm ödemeler, en yeniden eskiye.'}
        </p>
      </header>

      <div className="mb-stack-sm flex flex-wrap items-center gap-base">
        <FiltreDugmesi
          secili={durum === undefined && !yalnizBekleyenHavale}
          onClick={() => filtreDegistir(undefined)}
        >
          Hepsi
        </FiltreDugmesi>

        {DURUMLAR.map((secenek) => (
          <FiltreDugmesi
            key={secenek}
            secili={!yalnizBekleyenHavale && durum === secenek}
            onClick={() => filtreDegistir(secenek)}
          >
            {DURUM_METNI[secenek]}
          </FiltreDugmesi>
        ))}

        <FiltreDugmesi
          secili={yalnizBekleyenHavale}
          onClick={() => {
            setYalnizBekleyenHavale((acik) => !acik);
            setDurum(undefined);
            setSayfa(1);
          }}
        >
          Onay bekleyen havaleler
        </FiltreDugmesi>

        <form onSubmit={aramaGonder} className="ml-auto flex gap-base">
          <input
            value={arama}
            onChange={(olay) => setArama(olay.target.value)}
            placeholder="Ad, e-posta veya referans"
            aria-label="Ödeme ara"
            className="w-64 rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface"
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
          {[0, 1, 2, 3, 4].map((sira) => (
            <div key={sira} className="h-14 rounded-md bg-surface-variant/40" />
          ))}
        </div>
      )}

      {isError && (
        <p
          role="alert"
          className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-stack-sm font-body text-body-sm text-error"
        >
          {hataMesaji(error, 'Ödemeler yüklenemedi.')}
        </p>
      )}

      {data && data.items.length === 0 && (
        <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/20 px-stack-md py-stack-md font-body text-body-md text-on-surface-variant">
          Bu filtreyle eşleşen ödeme yok.
        </p>
      )}

      {data && data.items.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-outline-variant/40">
          <table className="w-full min-w-[900px] border-collapse text-left">
            <thead>
              <tr className="border-b border-outline-variant/40 bg-surface-variant/30">
                <Baslik>Tarih</Baslik>
                <Baslik>Kullanıcı</Baslik>
                <Baslik>Etkinlik</Baslik>
                <Baslik>Tutar</Baslik>
                <Baslik>Durum</Baslik>
                <Baslik>Sağlayıcı</Baslik>
                <Baslik>{''}</Baslik>
              </tr>
            </thead>

            <tbody>
              {data.items.map((odeme) => (
                <tr
                  key={odeme.id}
                  className="border-b border-outline-variant/20 last:border-b-0 hover:bg-surface-container-high/40"
                >
                  <Hucre>
                    <span className="tabular-nums">
                      {tarihBicimi.format(new Date(odeme.createdAt))}
                    </span>
                  </Hucre>

                  <Hucre>
                    <span className="block text-on-surface">{odeme.userFullName}</span>
                    <span className="block text-body-sm text-on-surface-variant">
                      {odeme.userEmail}
                    </span>
                  </Hucre>

                  <Hucre>
                    <span className="block text-on-surface">{odeme.eventTitle}</span>
                    <span className="block text-body-sm text-on-surface-variant">
                      {odeme.seatCount} koltuk
                    </span>
                  </Hucre>

                  <Hucre>
                    <span className="tabular-nums font-semibold text-on-surface">
                      {para(odeme.amount, odeme.currency)}
                    </span>
                  </Hucre>

                  <Hucre>
                    <span
                      className={`inline-block rounded-full border px-base py-[2px] text-[11px] ${DURUM_RENGI[odeme.status]}`}
                    >
                      {DURUM_METNI[odeme.status]}
                    </span>
                    {odeme.failureReason && (
                      <span className="mt-[2px] block text-body-sm text-on-surface-variant">
                        {odeme.failureReason}
                      </span>
                    )}
                  </Hucre>

                  <Hucre>
                    <span className="block text-on-surface-variant">
                      {odeme.method === 'BankTransfer' ? 'Havale / EFT' : odeme.provider}
                    </span>
                    {/* Saglayici referansi mutabakat sirasinda saglayicinin
                        panelindeki kayitla eslestirmenin tek yolu. */}
                    {odeme.providerReference && (
                      <span className="block break-all font-mono text-[11px] text-on-surface-variant">
                        {odeme.providerReference}
                      </span>
                    )}
                  </Hucre>

                  <Hucre>
                    {odeme.status === 'Succeeded' && (
                      <button
                        type="button"
                        onClick={() => setIadeEdilen(odeme)}
                        className="whitespace-nowrap rounded-md border border-outline px-stack-sm py-base text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
                      >
                        İade et
                      </button>
                    )}

                    {/* Onay/ret YALNIZCA bekleyen havalede. Kart odemesinde
                        gorunseydi panele erisen biri parasi hic cekilmemis
                        bir karti "odendi" yapabilirdi; sunucu da bu yuzden
                        ayrica reddediyor. */}
                    {odeme.method === 'BankTransfer' && odeme.status === 'Pending' && (
                      <div className="flex flex-wrap gap-base">
                        <button
                          type="button"
                          onClick={() => setHavaleKarari({ odeme, onay: true })}
                          className="whitespace-nowrap rounded-md bg-primary px-stack-sm py-base text-body-sm font-semibold text-on-primary"
                        >
                          Ödeme geldi
                        </button>
                        <button
                          type="button"
                          onClick={() => setHavaleKarari({ odeme, onay: false })}
                          className="whitespace-nowrap rounded-md border border-outline px-stack-sm py-base text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
                        >
                          Gelmedi
                        </button>
                      </div>
                    )}
                  </Hucre>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && data.totalPages > 1 && (
        <nav aria-label="Sayfalar" className="mt-stack-sm flex items-center justify-center gap-stack-sm">
          <SayfaDugmesi
            onClick={() => setSayfa((s) => s - 1)}
            devreDisi={!data.hasPreviousPage}
            etiket="Önceki sayfa"
          >
            <SolOkIkonu className="h-4 w-4" />
          </SayfaDugmesi>

          <span className="font-body text-body-sm tabular-nums text-on-surface-variant">
            {data.pageNumber} / {data.totalPages}
          </span>

          <SayfaDugmesi
            onClick={() => setSayfa((s) => s + 1)}
            devreDisi={!data.hasNextPage}
            etiket="Sonraki sayfa"
          >
            <SagOkIkonu className="h-4 w-4" />
          </SayfaDugmesi>
        </nav>
      )}

      {iadeEdilen && (
        <IadeOnayi
          odeme={iadeEdilen}
          bekliyor={iade.isPending}
          hata={iade.isError ? hataMesaji(iade.error, 'İade yapılamadı.') : null}
          onIptal={() => {
            iade.reset();
            setIadeEdilen(null);
          }}
          onOnay={(sebep) => iade.mutate({ id: iadeEdilen.id, sebep })}
        />
      )}

      {havaleKarari && (
        <HavaleKarariOnayi
          odeme={havaleKarari.odeme}
          onay={havaleKarari.onay}
          bekliyor={havale.isPending}
          hata={
            havale.isError
              ? hataMesaji(havale.error, 'Havale kararı işlenemedi.')
              : null
          }
          onIptal={() => {
            havale.reset();
            setHavaleKarari(null);
          }}
          onGonder={(metin) =>
            havale.mutate({ id: havaleKarari.odeme.id, onay: havaleKarari.onay, metin })
          }
        />
      )}
    </div>
  );
}

/**
 * Havale onay/ret penceresi.
 *
 * <b>Iki karar tek bilesende</b> cunku ikisi de ayni kaydin ayni alanina
 * bakip aciklama kodunu dogruluyor; ayri pencereler yazilsaydi o kod iki
 * yerde gosterilir ve biri degistiginde digeri unutulurdu.
 *
 * <para>
 * Onayda ekstre numarasi ISTEGE BAGLI, redde sebep ZORUNLU. Fark bilincli:
 * onay parayi gordugunun beyani, ret ise koltuklari geri alan ve musteriye
 * bildirim gonderen bir karar — gerekcesiz kayda gecmemeli.
 * </para>
 */
function HavaleKarariOnayi({
  odeme,
  onay,
  bekliyor,
  hata,
  onIptal,
  onGonder,
}: {
  odeme: AdminOdeme;
  onay: boolean;
  bekliyor: boolean;
  hata: string | null;
  onIptal: () => void;
  onGonder: (metin: string) => void;
}) {
  const [metin, setMetin] = useState('');

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/60 px-container-margin-mobile">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="havale-basligi"
        className="w-full max-w-md rounded-lg border border-outline-variant/40 bg-surface-container p-stack-md"
      >
        <h2 id="havale-basligi" className="font-headline text-title-lg text-on-surface">
          {onay ? 'Havale onayı' : 'Havale reddi'}
        </h2>

        <p className="mt-base font-body text-body-md text-on-surface">
          {odeme.userFullName} · {para(odeme.amount, odeme.currency)}
        </p>
        <p className="font-body text-body-sm text-on-surface-variant">{odeme.eventTitle}</p>

        {/* Aciklama kodu one cikiyor: yonetici ekstredeki hareketi bununla
            esliyor ve onaylamadan once gozuyle dogrulamasi gereken tek sey. */}
        {odeme.providerReference && (
          <p className="mt-stack-sm rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface-variant">
            Ekstrede aranacak açıklama:{' '}
            <code className="font-mono font-semibold text-primary">
              {odeme.providerReference}
            </code>
          </p>
        )}

        <p
          className={`mt-stack-sm rounded-md border px-stack-sm py-base font-body text-body-sm ${
            onay
              ? 'border-primary/40 bg-primary-container/15 text-primary'
              : 'border-tertiary/40 bg-tertiary-container/10 text-tertiary'
          }`}
        >
          {onay
            ? 'Biletler üretilecek ve koltuklar satılmış sayılacak. Geri almanın yolu iade akışı.'
            : 'Rezervasyon iptal edilecek ve koltuklar hemen satışa dönecek.'}
        </p>

        <label
          htmlFor="havale-metin"
          className="mt-stack-sm block font-body text-body-sm text-on-surface-variant"
        >
          {onay ? 'Ekstredeki işlem numarası (isteğe bağlı)' : 'Ret sebebi'}
        </label>
        <textarea
          id="havale-metin"
          value={metin}
          onChange={(olay) => setMetin(olay.target.value)}
          rows={onay ? 2 : 3}
          className="mt-base w-full rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface"
          placeholder={onay ? 'Örn. FT2026080712345' : 'Örn. süre doldu, ödeme ulaşmadı'}
        />

        {hata && (
          <p role="alert" className="mt-base font-body text-body-sm text-error">
            {hata}
          </p>
        )}

        <div className="mt-stack-sm flex justify-end gap-base">
          <button
            type="button"
            onClick={onIptal}
            className="rounded-md border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface"
          >
            Vazgeç
          </button>
          <button
            type="button"
            onClick={() => onGonder(metin.trim())}
            disabled={bekliyor || (!onay && metin.trim().length === 0)}
            className={`rounded-md px-stack-md py-base font-body text-body-sm font-semibold disabled:opacity-50 ${
              onay ? 'bg-primary text-on-primary' : 'bg-error text-on-error'
            }`}
          >
            {bekliyor ? 'İşleniyor' : onay ? 'Onayla' : 'Reddet'}
          </button>
        </div>
      </div>
    </div>
  );
}

/**
 * Iade onayi.
 *
 * <b>Sebep zorunlu.</b> Iade geri alinamayan bir islem: biletler iptal
 * oluyor ve koltuklar satisa donuyor. Sebep yazilmadan yapilabilseydi
 * aylar sonra "bu neden iade edilmis" sorusunun cevabi hicbir yerde
 * olmazdi.
 */
function IadeOnayi({
  odeme,
  bekliyor,
  hata,
  onIptal,
  onOnay,
}: {
  odeme: AdminOdeme;
  bekliyor: boolean;
  hata: string | null;
  onIptal: () => void;
  onOnay: (sebep: string) => void;
}) {
  const [sebep, setSebep] = useState('');

  return (
    <div className="fixed inset-0 z-50 grid place-items-center bg-black/60 px-container-margin-mobile">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="iade-basligi"
        className="w-full max-w-md rounded-lg border border-outline-variant/40 bg-surface-container p-stack-md"
      >
        <h2 id="iade-basligi" className="font-headline text-title-lg text-on-surface">
          İade onayı
        </h2>

        <p className="mt-base font-body text-body-md text-on-surface">
          {odeme.userFullName} · {para(odeme.amount, odeme.currency)}
        </p>
        <p className="font-body text-body-sm text-on-surface-variant">{odeme.eventTitle}</p>

        <p className="mt-stack-sm rounded-md border border-tertiary/40 bg-tertiary-container/10 px-stack-sm py-base font-body text-body-sm text-tertiary">
          Biletler iptal edilecek ve koltuklar satışa dönecek. Bu işlem geri alınamaz.
        </p>

        <label htmlFor="iade-sebebi" className="mt-stack-sm block font-body text-body-sm text-on-surface-variant">
          İade sebebi
        </label>
        <textarea
          id="iade-sebebi"
          value={sebep}
          onChange={(olay) => setSebep(olay.target.value)}
          rows={3}
          className="mt-base w-full rounded-md border border-outline-variant bg-surface-container-low px-stack-sm py-base font-body text-body-sm text-on-surface"
          placeholder="Örn. etkinlik iptal edildi"
        />

        {hata && (
          <p role="alert" className="mt-base font-body text-body-sm text-error">
            {hata}
          </p>
        )}

        <div className="mt-stack-sm flex justify-end gap-base">
          <button
            type="button"
            onClick={onIptal}
            className="rounded-md border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface"
          >
            Vazgeç
          </button>
          <button
            type="button"
            onClick={() => onOnay(sebep.trim())}
            disabled={bekliyor || sebep.trim().length === 0}
            className="rounded-md bg-error px-stack-md py-base font-body text-body-sm font-semibold text-on-error disabled:opacity-50"
          >
            {bekliyor ? 'İade ediliyor' : 'İade et'}
          </button>
        </div>
      </div>
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

function SayfaDugmesi({
  onClick,
  devreDisi,
  etiket,
  children,
}: {
  onClick: () => void;
  devreDisi: boolean;
  etiket: string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={devreDisi}
      aria-label={etiket}
      className="rounded-md border border-outline p-base text-on-surface transition-colors hover:bg-surface-container-high disabled:opacity-40"
    >
      {children}
    </button>
  );
}

function Baslik({ children }: { children: React.ReactNode }) {
  return (
    <th className="px-stack-sm py-base font-body text-[10px] font-bold uppercase tracking-[0.14em] text-on-surface-variant">
      {children}
    </th>
  );
}

function Hucre({ children }: { children: React.ReactNode }) {
  return <td className="px-stack-sm py-stack-sm align-top font-body text-body-sm">{children}</td>;
}
