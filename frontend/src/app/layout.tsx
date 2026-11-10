import type { Metadata } from "next";
import "./globals.css";
import { AuthProvider } from "@/lib/auth";
import { LanguageProvider } from "@/lib/i18n";
import { THEME_SCRIPT } from "@/lib/theme";

export const metadata: Metadata = {
  title: "Universitet Yetakchilari",
  description: "University Leaders Platform",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    // data-theme and lang are set on the client (theme script, LanguageProvider), so html attributes may differ.
    <html lang="uz" className="h-full antialiased" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: THEME_SCRIPT }} />
      </head>
      <body className="min-h-full bg-slate-50 font-sans text-slate-900">
        <LanguageProvider>
          <AuthProvider>
            {children}
          </AuthProvider>
        </LanguageProvider>
      </body>
    </html>
  );
}
