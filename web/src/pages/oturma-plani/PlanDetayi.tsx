import { useQuery } from '@tanstack/react-query';
import { hataMesaji } from '../../api/client';
import { type OturmaPlani, planGetir } from '../../api/seatLayouts';
import { SeatMap } from '../../components/SeatMap';
import { BolumEkleFormu } from './BolumEkleFormu';
import { KoltukUretFormu } from './KoltukUretFormu';

// --- Plan detayi: bolumler, koltuk uretimi, gorsel plan ---------------------

export function PlanDetayi({
  planId,
  salonId,
  onHata,
}: {
  planId: string;
  salonId: string;
  onHata: (mesaj: string | null) => void;
}) {
  const { data, isPending, isError, error } = useQuery<OturmaPlani>({
    queryKey: ['seat-layout', planId],
    queryFn: () => planGetir(planId, true),
  });

  if (isPending) {
    return (
      <p role="status" className="font-body text-body-sm text-on-surface-variant">
        Plan yükleniyor…
      </p>
    );
  }

  if (isError) {
    return (
      <p role="alert" className="font-body text-body-sm text-error">
        {hataMesaji(error, 'Plan yüklenemedi.')}
      </p>
    );
  }

  const bolumlerSirali = [...data.sections].sort((a, b) => a.displayOrder - b.displayOrder);

  return (
    <div className="space-y-stack-md">
      <header>
        <p className="font-body text-label-caps uppercase tracking-widest text-primary">
          {data.hallName}
        </p>
        <h2 className="font-headline text-title-lg text-on-surface">{data.name}</h2>
        <p className="font-body text-body-sm text-on-surface-variant">
          {data.totalSeatCount} / {data.hallCapacity} koltuk
          {data.description ? ` — ${data.description}` : ''}
        </p>
      </header>

      <section className="space-y-stack-sm">
        <h3 className="font-body text-body-sm font-semibold text-on-surface">Bölümler</h3>

        {bolumlerSirali.length === 0 && (
          <p className="font-body text-body-sm text-on-surface-variant">
            Henüz bölüm eklenmemiş.
          </p>
        )}

        {bolumlerSirali.length > 0 && (
          <ul className="space-y-base rounded-md border border-outline-variant/40 bg-surface-variant/20 p-stack-sm">
            {bolumlerSirali.map((bolum) => (
              <li key={bolum.id} className="font-body text-body-sm text-on-surface">
                {bolum.name} — {bolum.seats.length} koltuk
              </li>
            ))}
          </ul>
        )}

        <BolumEkleFormu planId={planId} salonId={salonId} onHata={onHata} />
      </section>

      <section className="space-y-stack-sm">
        <h3 className="font-body text-body-sm font-semibold text-on-surface">Koltuk üret</h3>
        <KoltukUretFormu planId={planId} plan={data} onHata={onHata} />
      </section>

      <section>
        <h3 className="mb-stack-sm font-body text-body-sm font-semibold text-on-surface">
          Görsel plan
        </h3>
        <SeatMap bolumler={data.sections} />
      </section>
    </div>
  );
}
