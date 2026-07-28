import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';

export interface KullaniciOzeti {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface OturumCevabi {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: KullaniciOzeti;
}

interface AuthDurumu {
  accessToken: string | null;
  refreshToken: string | null;
  kullanici: KullaniciOzeti | null;

  /** Sayfa acilisinda yenileme denemesi bitti mi. */
  hazir: boolean;

  oturumAc: (cevap: OturumCevabi) => void;
  oturumKapat: () => void;
  kullaniciyiGuncelle: (kullanici: KullaniciOzeti) => void;
  hazirIsaretle: () => void;
  rolVarMi: (rol: string) => boolean;
}

/**
 * Oturum durumu.
 *
 * Saklama karari: refresh token localStorage'a yazilir, access token yalnizca
 * bellekte tutulur. Boylece sayfa yenilendiginde oturum kaybolmaz ama kisa
 * omurlu access token diske hic inmez.
 *
 * Bunun bilinen zayifligi XSS: sayfaya kod enjekte edebilen bir saldirgan
 * localStorage'i okuyabilir. Tam cozum refresh token'i httpOnly cookie'de
 * tutmaktir; sunucu tarafinda cookie destegi gerektirdigi icin simdilik
 * bu tercih yapildi ve sinirinin bilincindeyiz.
 */
export const useAuthStore = create<AuthDurumu>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      kullanici: null,
      hazir: false,

      oturumAc: (cevap) =>
        set({
          accessToken: cevap.accessToken,
          refreshToken: cevap.refreshToken,
          kullanici: cevap.user,
        }),

      oturumKapat: () =>
        set({ accessToken: null, refreshToken: null, kullanici: null }),

      kullaniciyiGuncelle: (kullanici) => set({ kullanici }),

      hazirIsaretle: () => set({ hazir: true }),

      rolVarMi: (rol) => get().kullanici?.roles.includes(rol) ?? false,
    }),
    {
      name: 'loca-oturum',
      storage: createJSONStorage(() => localStorage),
      // Access token bilerek disarida birakildi — diske yazilmaz.
      partialize: (durum) => ({
        refreshToken: durum.refreshToken,
        kullanici: durum.kullanici,
      }),
    },
  ),
);
