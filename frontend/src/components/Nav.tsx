"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { ROLE_LABELS, type Person } from "@/lib/data";
import { useStore } from "@/lib/store";

const LINKS: { href: string; label: string }[] = [
  { href: "/", label: "Bosh sahifa" },
  { href: "/reyting", label: "Reyting" },
  { href: "/panel", label: "Panel" },
  { href: "/hisobotlar", label: "Hisobotlar" },
  { href: "/admin", label: "Admin" },
];

export function Nav() {
  const pathname: string = usePathname();
  const { people, currentId, setCurrentId } = useStore();
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
        <nav className={`${open ? "flex" : "hidden"} w-full flex-col gap-1 md:flex md:w-auto md:flex-row`}>
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
        </nav>
        <label className="flex w-full items-center gap-2 text-sm md:ml-auto md:w-auto">
          <span className="whitespace-nowrap text-slate-500">Demo foydalanuvchi:</span>
          <select
            value={currentId}
            onChange={(e) => setCurrentId(e.target.value)}
            className="w-full rounded-md border border-slate-300 bg-white px-2 py-1 md:w-64"
          >
            {(Object.keys(ROLE_LABELS) as (keyof typeof ROLE_LABELS)[]).map((role) => (
              <optgroup key={role} label={ROLE_LABELS[role]}>
                {people
                  .filter((p: Person) => p.role === role)
                  .map((p: Person) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
              </optgroup>
            ))}
          </select>
        </label>
      </div>
    </header>
  );
}
