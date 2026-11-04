import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router-dom';

import { dosyaAdresi, hataMesaji } from '../../api/client';
import type { SayfaliSonuc } from '../../api/admin';
import type { EtkinlikOzeti } from '../../api/eventCatalog';
import {
  BELGE_TURU_METNI,
  etkinlikBelgeleriniGetir,
  etkinligiYayinla,
  onayBekleyenleriGetir,
  type EtkinlikBelgesi,
} from '../../api/onay';
import { BelgeIkonu, OnayIkonu, UyariIkonu } from '../../components/ui/Ikon';
import { BosDurum } from '../../components/ui/BosDurum';
import { Iskelet } from '../../components/ui/Iskelet';
import { Uyari } from '../../components/ui/Uyari';
import { tarihSaatBicimi } from '../../lib/bicim';


function boyutBicimle(bayt: number): string {
  return bayt < 1024 * 1024
    ? `${Math.round(bayt / 1024)} KB`
    : `${(bayt / (1024 * 1024)).toFixed(1)} MB`;
}

/**
 * Onay kuyrugu.
 *
 * <b>Onay ekibinin gunluk ekrani.</b> Organizator etkinligi onaya
 * gonderdiginde buraya dusuyor; ekip sahne sozlesmesini acip okuyor ve
 * yayina aliyor.
 *
 * <para>
 * Belgeler etkinlik satirinin ICINDE, ayri bir sayfada degil: onay karari
 * "belgeye baktim mi" sorusuna bagli ve o belgeyi gormek icin baska bir
 * ekrana gitmek gerekseydi pratikte bakilmadan onaylanirdi.
 * </para>
 */
export function OnayKuyruguPage() {
  const queryClient = useQueryClient();
  const [acikEtkinlik, setAcikEtkinlik] = useState<string | null>(null);

  const { data, isPending, isError, error } = useQuery<SayfaliSonuc<EtkinlikOzeti>>({
    queryKey: ['onay-kuyrugu'],
    queryFn: onayBekleyenleriGetir,
  });

  const yayinla = useMutation({
    mutationFn: etkinligiYayinla,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['onay-kuyrugu'] });
      // Vitrindeki liste de degisti: yayinlanan etkinlik artik gorunuyor.
      await queryClient.invalidateQueries({ queryKey: ['discover-events'] });
    },
  });

  return (
    <div className="mx-auto max-w-4xl">
      <header className="mb-stack-md">
        <h1 className="font-headline text-headline-md text-on-surface">Onay kuyruğu</h1>
        <p className="font-body text-body-sm text-on-surface-variant">
          {data
            ? `${data.totalCount} etkinlik onay bekliyor.`
            : 'Organizatörlerin onaya gönderdiği etkinlikler.'}
        </p>
      </header>

      {isPending && (
        <Iskelet adet={3} cubuk="h-24 rounded-lg bg-surface-variant/40" duzen="space-y-stack-sm" />
      )}

      {isError && <Uyari role="alert">{hataMesaji(error, 'Onay kuyruğu yüklenemedi.')}</Uyari>}

      {data?.items.length === 0 && (
        <BosDurum ikon={<OnayIkonu className="h-5 w-5 shrink-0 text-primary" />}>
          Onay bekleyen etkinlik yok.
        </BosDurum>
      )}

      <ul className="space-y-stack-sm">
        {data?.items.map((etkinlik) => (
          <li
            key={etkinlik.id}
            className="rounded-lg border border-outline-variant/40 bg-surface-container-low p-stack-sm"
          >
            <div className="flex flex-wrap items-start justify-between gap-stack-sm">
              <div className="min-w-0">
                <h2 className="font-headline text-title-lg text-on-surface">
                  <Link
                    to={`/etkinlikler/${etkinlik.id}`}
                    className="hover:text-primary hover:underline"
                  >
                    {etkinlik.title}
                  </Link>
                </h2>

                <p className="font-body text-body-sm text-on-surface-variant">
                  {tarihSaatBicimi.format(new Date(etkinlik.eventDateUtc))} ·{' '}
                  {etkinlik.venueName}, {etkinlik.cityName}
                </p>

                <p className="font-body text-body-sm text-on-surface-variant/70">
                  {etkinlik.sessionCount} oturum · {etkinlik.categoryName}
                </p>
              </div>

              <div className="flex flex-wrap gap-base">
                <button
                  type="button"
                  onClick={() =>
                    setAcikEtkinlik((acik) => (acik === etkinlik.id ? null : etkinlik.id))
                  }
                  aria-expanded={acikEtkinlik === etkinlik.id}
                  className="inline-flex items-center gap-base whitespace-nowrap rounded-md border border-outline px-stack-sm py-base font-body text-body-sm text-on-surface transition-colors hover:bg-surface-container-high"
                >
                  <BelgeIkonu className="h-4 w-4" />
                  Belgeler
                </button>

                <button
                  type="button"
                  onClick={() => yayinla.mutate(etkinlik.id)}
                  disabled={yayinla.isPending}
                  className="whitespace-nowrap rounded-md bg-primary px-stack-md py-base font-body text-body-sm font-semibold text-on-primary disabled:opacity-60"
                >
                  {yayinla.isPending && yayinla.variables === etkinlik.id
                    ? 'Yayınlanıyor'
                    : 'Yayına al'}
                </button>
              </div>
            </div>

            {yayinla.isError && yayinla.variables === etkinlik.id && (
              <Uyari role="alert" dolgu="dar" className="mt-stack-sm">
                {hataMesaji(yayinla.error, 'Etkinlik yayına alınamadı.')}
              </Uyari>
            )}

            {acikEtkinlik === etkinlik.id && <Belgeler etkinlikId={etkinlik.id} />}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Etkinligin belgeleri.
 *
 * Yalnizca acildiginda cekiliyor: kuyrukta yirmi etkinlik varken hepsinin
 * belgesini onden istemek yirmi gereksiz sorgu olurdu.
 */
function Belgeler({ etkinlikId }: { etkinlikId: string }) {
  const { data, isPending, isError, error } = useQuery<EtkinlikBelgesi[]>({
    queryKey: ['etkinlik-belgeleri', etkinlikId],
    queryFn: () => etkinlikBelgeleriniGetir(etkinlikId),
  });

  if (isPending) {
    return (
      <p className="mt-stack-sm font-body text-body-sm text-on-surface-variant" role="status">
        Belgeler yükleniyor…
      </p>
    );
  }

  if (isError) {
    return (
      <p role="alert" className="mt-stack-sm font-body text-body-sm text-error">
        {hataMesaji(error, 'Belgeler yüklenemedi.')}
      </p>
    );
  }

  if (data.length === 0) {
    return (
      <Uyari
        ton="dikkat"
        dolgu="dar"
        hizala="orta"
        className="mt-stack-sm"
        ikon={<UyariIkonu className="h-4 w-4 shrink-0" />}
      >
        Belge yok. Onaya gönderme sahne sözleşmesi istediği için bu beklenmeyen bir durum.
      </Uyari>
    );
  }

  return (
    <ul className="mt-stack-sm space-y-base border-t border-outline-variant/30 pt-stack-sm">
      {data.map((belge) => (
        <li key={belge.id} className="flex flex-wrap items-baseline gap-base">
          <span className="rounded-full border border-outline-variant px-base py-[2px] font-body text-[11px] text-on-surface-variant">
            {BELGE_TURU_METNI[belge.kind]}
          </span>

          {/* Yeni sekmede: onay ekibi belgeyi okurken kuyrugu kaybetmemeli.
              rel="noreferrer" — acilan sayfa window.opener uzerinden bu
              sayfayi yonlendirebilirdi. */}
          <a
            href={dosyaAdresi(belge.uploadedFileId) ?? '#'}
            target="_blank"
            rel="noreferrer"
            className="font-body text-body-sm text-primary underline underline-offset-2"
          >
            {belge.originalFileName}
          </a>

          <span className="font-body text-body-sm text-on-surface-variant/70">
            {boyutBicimle(belge.sizeInBytes)}
          </span>

          {belge.note && (
            <span className="font-body text-body-sm text-on-surface-variant">— {belge.note}</span>
          )}
        </li>
      ))}
    </ul>
  );
}
