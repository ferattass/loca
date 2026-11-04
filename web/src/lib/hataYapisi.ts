import { dogrulamaHatalari, hataMesaji } from '../api/client';

/**
 * Bir sunucu hatasının kullanıcıya gösterilecek iki parçası.
 *
 * Özet her zaman var; liste yalnızca alan bazlı doğrulama hatalarında
 * doluyor. İkisi birlikte taşınıyor çünkü kutuyu çizen taraf hangisinin
 * geldiğini önceden bilemiyor.
 */
export interface HataYapisi {
  ozet: string;
  liste: string[];
}

export function hataYapisiOlustur(hata: unknown, varsayilan: string): HataYapisi {
  return { ozet: hataMesaji(hata, varsayilan), liste: dogrulamaHatalari(hata) };
}
