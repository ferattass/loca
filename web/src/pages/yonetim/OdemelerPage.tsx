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
import { HavaleKarariOnayi } from './odemeler/HavaleKarariOnayi';
import { IadeOnayi } from './odemeler/IadeOnayi';
import { Baslik, FiltreDugmesi, Hucre, SayfaDugmesi } from './odemeler/tablo';

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

