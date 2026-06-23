/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        background: "var(--bg-color)",
        surface: "var(--surface-color)",
        textPrimary: "var(--text-primary)",
        textMuted: "var(--text-muted)",
        accent: "var(--accent)",
        border: "var(--border-color)",
        success: "var(--success)",
        error: "var(--error)",
        warning: "var(--warning)",
      },
      fontFamily: {
        sans: ['var(--font-outfit)', 'var(--font-cairo)', 'sans-serif'],
        mono: ['var(--font-jetbrains)', 'monospace'],
      },
      borderRadius: {
        sm: '4px',
        md: '8px',
      },
      spacing: {
        'xs': '4px',
        'sm': '8px',
        'md': '16px',
        'lg': '24px',
        'xl': '32px',
        '2xl': '48px',
      }
    },
  },
  plugins: [],
};
