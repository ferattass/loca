import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';

import { hataMesaji } from '../api/client';
import { sehirleriGetir } from '../api/catalog';
import { mekanListesiGetir } from '../api/venues';
import { Button } from '../components/ui/Button';
import { TextField } from '../components/ui/TextField';
import { Secim } from '../components/ui/Secim';
import { YeniMekanFormu } from './mekan/YeniMekanFormu';
import { MekanDetayPaneli } from './mekan/MekanDetayi';

const SAYFA_BOYUTU = 10;

/**
 * Yonetici paneli: mekan ve salon yonetimi.
 *
 * Iki sutunlu duzen — solda filtreli mekan listesi ve yeni mekan formu,
 * sagda (mobilde altta) secili mekanin duzenleme formu ve salon listesi.
 * Silme islemleri (hem mekan hem salon) tek tiklamayla tetiklenmiyor;
 * oturma plani ekraninda oldugu gibi once onay isteniyor, cunku sunucu
 * tarafinda geri alinmasi mumkun degil.
 */
export function MekanYonetimPage() {
  const [sehirId, setSehirId] = useState('');
  const [aramaTaslak, setAramaTaslak] = useState('');
  const [arama, setArama] = useState('');
  const [sayfaNo, setSayfaNo] = useState(1);
  const [seciliMekanId, setSeciliMekanId] = useState<string | null>(null);

  const sehirler = useQuery({ queryKey: ['cities'], queryFn: sehirleriGetir });

  const mekanlar = useQuery({
    queryKey: ['mekan-listesi', sehirId, arama, sayfaNo],
    queryFn: () => mekanListesiGetir({ sehirId, arama, sayfaNo, sayfaBoyutu: SAYFA_BOYUTU }),
    // Sayfa degistiginde onceki liste ekranda kalir; aksi halde her sayfa
    // gecisinde bos bir iskelet gorunur ve liste "zipliyor" gibi hissettirir.
    placeholderData: keepPreviousData,
  });

  const toplamSayfa = Math.max(1, Math.ceil((mekanlar.data?.totalCount ?? 0) / SAYFA_BOYUTU));

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-6xl">
        <h1 className="mb-stack-md font-headline text-headline-md text-on-surface">
          Mekân ve salon yönetimi
        </h1>

        <div className="grid gap-stack-lg md:grid-cols-2">
          <section className="space-y-stack-sm">
            <div className="flex flex-wrap items-end gap-stack-sm">
              <Secim
                etiket="Şehir"
                deger={sehirId}
                gerekli={false}
                bosMetin="Tüm şehirler"
                onDegis={(deger) => {
                  setSehirId(deger);
                  setSayfaNo(1);
                }}
                secenekler={(sehirler.data ?? []).map((s) => ({ id: s.id, ad: s.name }))}
              />

              <form
                className="flex flex-1 items-end gap-stack-sm"
                onSubmit={(olay) => {
                  olay.preventDefault();
                  setArama(aramaTaslak);
                  setSayfaNo(1);
                }}
              >
                <div className="flex-1">
                  <TextField
                    etiket="Mekân ara"
                    value={aramaTaslak}
                    placeholder="Mekân adıyla ara"
                    onChange={(olay) => setAramaTaslak(olay.target.value)}
                  />
                </div>
                <Button type="submit" gorunum="cizgili">
                  Ara
                </Button>
              </form>
            </div>

            {mekanlar.isPending && (
              <div className="animate-pulse space-y-base" aria-hidden="true">
                <div className="h-16 rounded-md bg-surface-variant/40" />
                <div className="h-16 rounded-md bg-surface-variant/40" />
                <div className="h-16 rounded-md bg-surface-variant/40" />
                <span className="sr-only" role="status">
                  Mekânlar yükleniyor
                </span>
              </div>
            )}

            {mekanlar.isError && (
              <p
                role="alert"
                className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
              >
                {hataMesaji(mekanlar.error, 'Mekânlar yüklenemedi.')}
              </p>
            )}

            {mekanlar.data && mekanlar.data.items.length === 0 && (
              <p className="rounded-md border border-outline-variant/40 bg-surface-variant/20 px-stack-sm py-stack-md font-body text-body-sm text-on-surface-variant">
                Kayıtlı mekân bulunamadı.
              </p>
            )}

            {mekanlar.data && mekanlar.data.items.length > 0 && (
              <>
                <ul className="space-y-base">
                  {mekanlar.data.items.map((mekan) => {
                    const secili = mekan.id === seciliMekanId;

                    return (
                      <li key={mekan.id}>
                        <button
                          type="button"
                          aria-pressed={secili}
                          onClick={() => setSeciliMekanId(mekan.id)}
                          className={`w-full rounded-md border px-stack-sm py-base text-left transition-colors ${
                            secili
                              ? 'border-primary bg-surface-variant/40'
                              : 'border-outline-variant/40 bg-surface-variant/10 hover:border-outline'
                          }`}
                        >
                          <div className="flex items-center justify-between gap-stack-sm">
                            <span className="font-body text-body-md text-on-surface">
                              {mekan.name}
                            </span>
                            <span
                              className={`font-body text-body-sm ${
                                mekan.isActive ? 'text-primary' : 'text-on-surface-variant/60'
                              }`}
                            >
                              {mekan.isActive ? 'Aktif' : 'Pasif'}
                            </span>
                          </div>
                          <p className="font-body text-body-sm text-on-surface-variant">
                            {mekan.cityName} · {mekan.hallCount} salon
                          </p>
                        </button>
                      </li>
                    );
                  })}
                </ul>

                <div className="flex items-center justify-between gap-stack-sm">
                  <Button
                    type="button"
                    gorunum="sade"
                    disabled={sayfaNo <= 1}
                    onClick={() => setSayfaNo((s) => s - 1)}
                  >
                    Önceki
                  </Button>
                  <span className="font-body text-body-sm text-on-surface-variant">
                    Sayfa {sayfaNo} / {toplamSayfa}
                  </span>
                  <Button
                    type="button"
                    gorunum="sade"
                    disabled={sayfaNo >= toplamSayfa}
                    onClick={() => setSayfaNo((s) => s + 1)}
                  >
                    Sonraki
                  </Button>
                </div>
              </>
            )}

            <YeniMekanFormu
              sehirler={sehirler.data ?? []}
              onOlusturuldu={(id) => setSeciliMekanId(id)}
            />
          </section>

          <section>
            {seciliMekanId ? (
              <MekanDetayPaneli
                key={seciliMekanId}
                mekanId={seciliMekanId}
                onKapat={() => setSeciliMekanId(null)}
                onSilindi={() => setSeciliMekanId(null)}
              />
            ) : (
              <p className="rounded-lg border border-outline-variant/40 bg-surface-variant/10 px-stack-sm py-stack-lg text-center font-body text-body-sm text-on-surface-variant">
                Detaylarını görmek için soldaki listeden bir mekân seç.
              </p>
            )}
          </section>
        </div>
      </div>
    </main>
  );
}

