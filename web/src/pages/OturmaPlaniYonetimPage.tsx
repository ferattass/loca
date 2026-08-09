import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';

import { mekanlariGetir, salonlariGetir } from '../api/seatLayoutAdmin';
import { Secim } from '../components/ui/Secim';
import { PlanYonetimi } from './oturma-plani/PlanYonetimi';
import { PlanDetayi } from './oturma-plani/PlanDetayi';


/**
 * Oturma plani yonetim ekrani.
 *
 * Organizator sihirbazindaki (EtkinlikOlusturPage) salon secimi var olan bir
 * plani SECER; burasi ise plani var eden taraf: olusturma, silme, bolum
 * ekleme ve toplu koltuk uretme. Ikisi ayni domain nesnelerine bakiyor ama
 * yetkisi ve amaci farkli oldugu icin ayri sayfa ve ayri API dosyasinda.
 */
export function OturmaPlaniYonetimPage() {
  const [mekanId, setMekanId] = useState('');
  const [salonId, setSalonId] = useState('');
  const [planId, setPlanId] = useState('');
  const [hata, setHata] = useState<string | null>(null);

  const mekanlar = useQuery({ queryKey: ['admin-venues'], queryFn: mekanlariGetir });

  const salonlar = useQuery({
    queryKey: ['admin-halls', mekanId],
    queryFn: () => salonlariGetir(mekanId),
    enabled: mekanId !== '',
  });

  return (
    <main className="min-h-screen px-container-margin-mobile md:px-container-margin-desktop py-stack-lg">
      <div className="mx-auto max-w-5xl space-y-stack-md">
        <header>
          <h1 className="font-headline text-headline-md text-on-surface">
            Oturma planı yönetimi
          </h1>
          <p className="font-body text-body-sm text-on-surface-variant">
            Salon seç, plan oluştur veya sil, bölüm ekle ve koltukları toplu üret.
          </p>
        </header>

        {hata && (
          <p
            role="alert"
            className="rounded-md border border-error/40 bg-error-container/20 px-stack-sm py-base font-body text-body-sm text-error"
          >
            {hata}
          </p>
        )}

        <div className="grid gap-stack-sm md:grid-cols-2">
          <Secim
            etiket="Mekân"
            deger={mekanId}
            onDegis={(secilen) => {
              // Zincirin ustu degisince alttakiler sifirlanir; aksi halde
              // baska bir mekanin salonu secili kalir ve sunucu bunu
              // tutarsiz bulur.
              setMekanId(secilen);
              setSalonId('');
              setPlanId('');
            }}
            secenekler={(mekanlar.data ?? []).map((m) => ({
              id: m.id,
              ad: `${m.name} (${m.hallCount} salon)`,
            }))}
          />
          <Secim
            etiket="Salon"
            deger={salonId}
            devreDisi={mekanId === ''}
            onDegis={(secilen) => {
              setSalonId(secilen);
              setPlanId('');
            }}
            secenekler={(salonlar.data ?? []).map((s) => ({
              id: s.id,
              ad: `${s.name} — ${s.capacity} kişilik`,
            }))}
          />
        </div>

        {salonId !== '' && (
          <section className="space-y-stack-sm">
            <h2 className="font-body text-body-sm font-semibold text-on-surface">
              Oturma planları
            </h2>
            {/*
              key=salonId: salon degisince listedeki silme onayi ve yeni plan
              formu sifirlanir. Aksi halde bir onceki salonun formu doldurulmus
              halde yeni salona tasinirdi.
            */}
            <PlanYonetimi
              key={salonId}
              salonId={salonId}
              planId={planId}
              onPlanSec={setPlanId}
              onHata={setHata}
            />
          </section>
        )}

        {planId !== '' && (
          <section>
            <PlanDetayi key={planId} planId={planId} salonId={salonId} onHata={setHata} />
          </section>
        )}
      </div>
    </main>
  );
}

