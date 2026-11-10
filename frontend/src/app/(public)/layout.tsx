import Image from "next/image";
import { Suspense } from "react";
import { AnimatedBackground } from "@/components/AnimatedBackground";
import { PublicNav } from "@/components/PublicNav";
import { T } from "@/lib/i18n";

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <AnimatedBackground />
      <Suspense fallback={<div className="h-14 border-b border-slate-200 bg-white" />}><PublicNav /></Suspense>
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">{children}</main>
      <footer className="mt-10 border-t border-slate-200/70 bg-white/70 backdrop-blur">
        <div className="flag-ribbon" aria-hidden="true" />
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-4 py-5 text-sm text-slate-500">
          <div className="flex items-center gap-3">
            <Image src="/ozmu-logo.png" alt="" width={291} height={128} className="logo-img h-8 w-auto mix-blend-multiply" />
            <span><T>{"Mirzo Ulug'bek nomidagi O'zbekiston Milliy universiteti · Yoshlar ittifoqi"}</T></span>
          </div>
          <span>© <T>Universitet Yetakchilari platformasi</T></span>
        </div>
      </footer>
    </div>
  );
}
