import { cn } from '@repo/design-system/lib/utils';
import { Cairo, Inter, JetBrains_Mono } from 'next/font/google';

export const fontSans = Inter({
  subsets: ['latin'],
  variable: '--font-sans',
  display: 'swap',
});

export const fontArabic = Cairo({
  subsets: ['arabic'],
  variable: '--font-arabic',
  display: 'swap',
});

export const fontMono = JetBrains_Mono({
  subsets: ['latin'],
  variable: '--font-mono',
  display: 'swap',
});

export const fonts = cn(
  fontSans.variable,
  fontArabic.variable,
  fontMono.variable,
  'touch-manipulation font-sans antialiased'
);
