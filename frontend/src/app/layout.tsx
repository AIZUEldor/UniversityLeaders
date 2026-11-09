import type { Metadata } from "next";
import "./globals.css";
import { StoreProvider } from "@/lib/store";

export const metadata: Metadata = {
  title: "Universitet Yetakchilari (demo)",
  description: "University Leaders Platform — interaktiv demo prototip",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="uz" className="h-full antialiased">
      <body className="min-h-full bg-slate-50 font-sans text-slate-900">
        <StoreProvider>{children}</StoreProvider>
      </body>
    </html>
  );
}
