"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useState } from "react";
import { ROLE_LABELS, type Role } from "@/lib/data";
import { useStore } from "@/lib/store";

export interface NavItem {
  href: string;
  label: string;
  roles: Role[];
}

// shortcut: client-side DEMO guard only (hides UI, protects nothing); real authorization comes from the API later.
export function PrivateShell({ title, items, children }: { title: string; items: NavItem[]; children: React.ReactNode }) {
  const pathname: string = usePathname();
  const router = useRouter();
  const { current, setCurrentId } = useStore();
  const [open, setOpen] = useState<boolean>(false);

  const visible: NavItem[] = current ? items.filter((i: NavItem) => i.roles.includes(current.role)) : [];
  const page: NavItem | undefined = items.find((i: NavItem) => i.href === pathname);
  const allowed: boolean = current !== null && page !== undefined && page.roles.includes(current.role);
  const home: string = current?.role === "university_leader" ? "/admin" : "/dashboard";

  const signOut = (): void => {
    setCurrentId(null);
    router.push("/login");
  };

  return (
    <div className="min-h-screen md:flex">
      <aside className="border-b border-slate-800 bg-slate-900 text-slate-200 md:sticky md:top-0 md:h-screen md:w-64 md:shrink-0 md:border-b-0">
        <div className="flex items-center gap-2 px-4 py-4">
          <div>
            <div className="font-bold text-white">{title}</div>
            <span className="rounded bg-amber-400 px-1.5 py-0.5 text-[10px] font-bold uppercase text-amber-950">Demo</span>
          </div>
          <button
            className="ml-auto rounded-md border border-slate-600 px-3 py-1 text-sm md:hidden"
            onClick={() => setOpen(!open)}
            aria-expanded={open}
            aria-label="Menyu"
          >
            ☰
          </button>
        </div>
        <nav className={`${open ? "flex" : "hidden"} flex-col gap-1 px-3 pb-4 md:flex`}>
          {visible.map((i: NavItem) => (
            <Link
              key={i.href}
              href={i.href}
              onClick={() => setOpen(false)}
              className={`rounded-md px-3 py-2 text-sm ${pathname === i.href ? "bg-indigo-600 text-white" : "hover:bg-slate-800"}`}
            >
              {i.label}
            </Link>
          ))}
          {current && (
            <div className="mt-4 border-t border-slate-700 pt-4 text-xs text-slate-400">
              <div className="font-medium text-slate-200">{current.name}</div>
              <div>{ROLE_LABELS[current.role]}</div>
              <div className="mt-3 flex gap-3">
                <Link href="/" className="hover:text-white">
                  Ommaviy sayt
                </Link>
                <button onClick={signOut} className="hover:text-white">
                  Chiqish
                </button>
              </div>
            </div>
          )}
        </nav>
      </aside>

      <div className="min-w-0 flex-1">
        <div className="bg-amber-100 px-4 py-1 text-center text-xs font-medium text-amber-900">
          DEMO yopiq bo&apos;lim — haqiqiy avtorizatsiya yo&apos;q, xavfsiz emas. Ma&apos;lumotlar sahifa yangilanganda tiklanadi.
        </div>
        <main className="mx-auto max-w-5xl px-4 py-6">
          {allowed ? (
            children
          ) : (
            <div className="rounded-2xl border border-slate-200 bg-white p-6 text-sm shadow-sm">
              <p className="font-semibold">{current ? "Bu bo'lim sizning rolingiz uchun emas." : "Avval demo rejimida kiring."}</p>
              <Link href={current ? home : "/login"} className="mt-3 inline-block text-indigo-600 hover:underline">
                {current ? "Mening bo'limimga o'tish →" : "Kirish sahifasi →"}
              </Link>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
