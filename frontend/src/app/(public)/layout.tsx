import { Suspense } from "react";
import { PublicNav } from "@/components/PublicNav";

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <Suspense fallback={<div className="h-14 border-b border-slate-200 bg-white" />}><PublicNav /></Suspense>
      <main className="mx-auto max-w-6xl px-4 py-6">{children}</main>
    </>
  );
}
