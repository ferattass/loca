/** @type {import('tailwindcss').Config} */
// Tasarim sistemi: stitch_loca_event_ticketing_platform/premium_high_tech_event_system/DESIGN.md
// Material 3 token yapisi — koyu tema, "glassmorphism + neon" yonu.
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        // --- Yuzeyler (Deep Space) ---
        background: '#15121b',
        'on-background': '#e7e0ed',
        surface: '#15121b',
        'surface-dim': '#15121b',
        'surface-bright': '#3b3742',
        'surface-container-lowest': '#0f0d15',
        'surface-container-low': '#1d1a23',
        'surface-container': '#211e27',
        'surface-container-high': '#2c2832',
        'surface-container-highest': '#37333d',
        'surface-variant': '#37333d',
        'surface-tint': '#d0bcff',
        'on-surface': '#e7e0ed',
        'on-surface-variant': '#cbc3d7',
        'inverse-surface': '#e7e0ed',
        'inverse-on-surface': '#322f39',

        // --- Primary (Electric Violet) — ana aksiyon, secili koltuk ---
        primary: '#d0bcff',
        'on-primary': '#3c0091',
        'primary-container': '#a078ff',
        'on-primary-container': '#340080',
        'inverse-primary': '#6d3bd7',
        'primary-fixed': '#e9ddff',
        'primary-fixed-dim': '#d0bcff',
        'on-primary-fixed': '#23005c',
        'on-primary-fixed-variant': '#5516be',

        // --- Secondary (Cyan) — bilgi, veri gorsellestirme ---
        secondary: '#4cd7f6',
        'on-secondary': '#003640',
        'secondary-container': '#03b5d3',
        'on-secondary-container': '#00424e',
        'secondary-fixed': '#acedff',
        'secondary-fixed-dim': '#4cd7f6',
        'on-secondary-fixed': '#001f26',
        'on-secondary-fixed-variant': '#004e5c',

        // --- Tertiary (Amber) — gecici kilit / uyari ---
        tertiary: '#ffb869',
        'on-tertiary': '#482900',
        'tertiary-container': '#ca801e',
        'on-tertiary-container': '#3f2300',
        'tertiary-fixed': '#ffdcbb',
        'tertiary-fixed-dim': '#ffb869',
        'on-tertiary-fixed': '#2c1700',
        'on-tertiary-fixed-variant': '#673d00',

        // --- Error ---
        error: '#ffb4ab',
        'on-error': '#690005',
        'error-container': '#93000a',
        'on-error-container': '#ffdad6',

        // --- Cizgiler ---
        outline: '#958ea0',
        'outline-variant': '#494454',

        // --- Koltuk durumlari (5 durum — sartname geregi) ---
        // DESIGN.md'de 4 durum vardi; "disabled" (pasif koltuk) eklendi.
        seat: {
          available: '#3b3742', // bos — secilebilir
          selected: '#d0bcff', // senin sectigin
          locked: '#ffb869', // baskasi gecici kilitlemis
          sold: '#494454', // satilmis
          disabled: '#211e27', // devre disi (bozuk koltuk / kolon arkasi)
        },
      },

      fontFamily: {
        display: ['Montserrat', 'system-ui', 'sans-serif'],
        headline: ['Montserrat', 'system-ui', 'sans-serif'],
        body: ['Inter', 'system-ui', 'sans-serif'],
      },

      fontSize: {
        'display-lg': ['48px', { lineHeight: '56px', letterSpacing: '-0.02em', fontWeight: '800' }],
        'display-lg-mobile': ['32px', { lineHeight: '40px', letterSpacing: '-0.01em', fontWeight: '800' }],
        'headline-md': ['24px', { lineHeight: '32px', fontWeight: '700' }],
        'title-lg': ['20px', { lineHeight: '28px', fontWeight: '600' }],
        'body-md': ['16px', { lineHeight: '24px', fontWeight: '400' }],
        'body-sm': ['14px', { lineHeight: '20px', fontWeight: '400' }],
        'label-caps': ['12px', { lineHeight: '16px', letterSpacing: '0.1em', fontWeight: '700' }],
      },

      // 8px tabanli ritim
      spacing: {
        base: '8px',
        'stack-sm': '12px',
        'stack-md': '24px',
        'stack-lg': '48px',
        gutter: '24px',
        'container-margin-mobile': '20px',
        'container-margin-desktop': '64px',
      },

      borderRadius: {
        sm: '0.25rem',
        DEFAULT: '0.5rem',
        md: '0.75rem',
        lg: '1rem',
        xl: '1.5rem',
        full: '9999px',
      },

      // Neon glow — DESIGN.md "Elevation & Depth"
      boxShadow: {
        'glow-primary': '0 0 15px rgba(208, 188, 255, 0.35)',
        'glow-primary-lg': '0 0 28px rgba(208, 188, 255, 0.5)',
        'glow-secondary': '0 0 15px rgba(76, 215, 246, 0.35)',
        'glow-tertiary': '0 0 12px rgba(255, 184, 105, 0.45)',
        'glow-error': '0 0 15px rgba(255, 180, 171, 0.35)',
      },

      backdropBlur: {
        glass: '16px',
      },

      // Geri sayim son 60 saniye
      keyframes: {
        'pulse-urgent': {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.55' },
        },
      },
      animation: {
        'pulse-urgent': 'pulse-urgent 1s ease-in-out infinite',
      },
    },
  },
  plugins: [],
};
