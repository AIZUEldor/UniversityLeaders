"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

const LINKS: { href: string; label: string }[] = [
  { href: "/", label: "Bosh sahifa" },
  { href: "/reyting", label: "Reyting" },
  { href: "/completed-work", label: "Bajarilgan ishlar" },
];

export function PublicNav() {
  const pathname: string = usePathname();
  const [open, setOpen] = useState<boolean>(false);

  return (
    <header className="sticky top-0 z-20 border-b border-slate-200 bg-white/90 backdrop-blur">
      <div className="bg-amber-100 px-4 py-1 text-center text-xs font-medium text-amber-900">
        DEMO prototip — barcha ism va ma&apos;lumotlar namunaviy. Avtorizatsiya va server ulanmagan.
      </div>
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-3">
        <Link href="/" className="text-lg font-bold text-indigo-700">
          Universitet Yetakchilari
        </Link>
        <button
          className="ml-auto rounded-md border px-3 py-1 text-sm md:hidden"
          onClick={() => setOpen(!open)}
          aria-expanded={open}
          aria-label="Menyu"
        >
          ☰
        </button>
        <nav className={`${open ? "flex" : "hidden"} w-full flex-col gap-1 md:ml-auto md:flex md:w-auto md:flex-row md:items-center`}>
          {LINKS.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              onClick={() => setOpen(false)}
              className={`rounded-md px-3 py-1.5 text-sm font-medium ${
                pathname === l.href ? "bg-indigo-600 text-white" : "text-slate-700 hover:bg-slate-100"
              }`}
            >
              {l.label}
            </Link>
          ))}
          <Link href="/login" className="rounded-md border border-indigo-600 px-3 py-1.5 text-sm font-medium text-indigo-700 hover:bg-indigo-50 md:ml-2">
            Kirish
          </Link>
        </nav>
      </div>
    </header>
  );
}
