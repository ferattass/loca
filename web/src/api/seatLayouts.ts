import { api } from './client';
import type { BolumVerisi } from '../components/SeatMap';

export interface OturmaPlani {
  id: string;
  hallId: string;
  hallName: string;
  hallCapacity: number;
  name: string;
  description: string | null;
  isActive: boolean;
  totalSeatCount: number;
  sections: BolumVerisi[];
}

/**
 * Oturma planini getirir.
 *
 * Koltuklar varsayilan olarak DONMEZ; gorsel plan cizilecekse acikca
 * istenir. Alti yuz koltuklu bir plan her liste isteginde gereksiz yere
 * tasinmasin diye.
 */
export async function planGetir(
  id: string,
  koltuklarlaBirlikte = false,
): Promise<OturmaPlani> {
  const { data } = await api.get<OturmaPlani>(`/seat-layouts/${id}`, {
    params: { koltuklarlaBirlikte },
  });

  return data;
}

export async function koltukDurumuDegistir(koltukId: string): Promise<boolean> {
  const { data } = await api.patch<boolean>(`/seats/${koltukId}/toggle-active`);
  return data;
}
