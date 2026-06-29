import type { Metadata } from "next";
import { Outfit, Cairo, JetBrains_Mono } from "next/font/google";
import "./globals.css";
import Providers from "@/components/Providers";

const outfit = Outfit({
  subsets: ["latin"],
  variable: "--font-outfit",
  display: "swap",
});

const cairo = Cairo({
  subsets: ["arabic", "latin"],
  variable: "--font-cairo",
  display: "swap",
});

const jetbrainsMono = JetBrains_Mono({
  subsets: ["latin"],
  variable: "--font-jetbrains",
  display: "swap",
});

export const metadata: Metadata = {
  title: "AlphaZero Learning Academy",
  description: "High-performance Tenant-Based SaaS e-learning platform",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    // 'dir="auto"' allows the browser to switch RTL/LTR based on text, 
    // but in a real i18n setup, this would be dynamically set to "rtl" or "ltr" based on locale.
    <html lang="ar" dir="rtl" className={`${outfit.variable} ${cairo.variable} ${jetbrainsMono.variable}`}>
      <body className="antialiased font-sans">
        <Providers>
          {children}
        </Providers>
      </body>
    </html>
  );
}
