import type { Metadata } from "next";
import "./globals.css";
import { StoreProvider } from "@/lib/store";
import { Nav } from "@/components/Nav";

export const metadata: Metadata = {
  title: "Universitet Yetakchilari (demo)",
  description: "University Leaders Platform — interaktiv demo prototip",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="uz" className="h-full antialiased">
      <body className="min-h-full bg-slate-50 font-sans text-slate-900">
        <StoreProvider>
          <Nav />
          <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>
        </StoreProvider>
      </body>
    </html>
  );
}
