import { Outlet } from 'react-router-dom';

import { SiteAltbilgisi } from './SiteAltbilgisi';
import { SiteBasligi } from './SiteBasligi';

/**
 * Baslik ve alt bilgiyi tasiyan ortak kabuk.
 *
 * Rota duzeyinde uygulaniyor (<c>Outlet</c>): her sayfanin kendi icine
 * baslik koymasi gerekseydi biri unutulur ve o sayfada gezinme kaybolurdu.
 * Ayrica baslik sayfa degisiminde yeniden olusmuyor — acik menu ve yapiskan
 * konum korunuyor.
 *
 * <b>Giris ve kayit ekranlari bu kabugun DISINDA.</b> Oralarda gezinme
 * menusu gostermek, henuz erisemeyecegi sayfalari kullanicinin onune
 * koymak olurdu.
 */
export function SiteKabugu() {
  return (
    <div className="flex min-h-screen flex-col bg-background">
      <SiteBasligi />

      {/* Icerik esner, alt bilgi kisa sayfalarda bile en altta kalir. */}
      <div className="flex-1">
        <Outlet />
      </div>

      <SiteAltbilgisi />
    </div>
  );
}
