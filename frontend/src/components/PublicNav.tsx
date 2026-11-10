"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { Preferences } from "@/components/Preferences";
import { homeFor, useAuth } from "@/lib/auth";
import { useT } from "@/lib/i18n";

const LINKS: { href: string; label: string }[] = [
  { href: "/", label: "Bosh sahifa" },
  { href: "/reyting", label: "Reyting" },
  { href: "/completed-work", label: "Bajarilgan ishlar" },
];

export function PublicNav() {
  const pathname: string = usePathname();
  const [open, setOpen] = useState<boolean>(false);
  const { state } = useAuth();
  const t = useT();
  const area: string = state.status === "authenticated" ? homeFor(state.user.role) : "/login";

  return (
    <header className="sticky top-0 z-20 border-b border-slate-200/70 bg-white/80 shadow-sm backdrop-blur-md">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-3 px-4 py-3">
        <Link href="/" className="flex min-w-0 items-center gap-2 text-base font-bold text-indigo-700 sm:text-lg">
          {/* Height is fixed per breakpoint and width follows the file's 291x128 ratio, so the logo never stretches. */}
          <Image src="/ozmu-logo.png" alt={t("O'zMU Yoshlar ittifoqi logotipi")} width={291} height={128} priority className="logo-img h-8 w-auto shrink-0 mix-blend-multiply sm:h-10" />
          <span className="truncate">{t("Universitet Yetakchilari")}</span>
        </Link>
        <button
          className="ml-auto rounded-md border px-3 py-1 text-sm md:hidden"
          onClick={() => setOpen(!open)}
          aria-expanded={open}
          aria-label={t("Menyu")}
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
              {t(l.label)}
            </Link>
          ))}
          <Link href={area} className="rounded-md border border-indigo-600 px-3 py-1.5 text-sm font-medium text-indigo-700 hover:bg-indigo-50 md:ml-2">
            {state.status === "authenticated" ? t("Kabinet") : t("Kirish")}
          </Link>
          <Preferences className="mt-2 md:mt-0 md:ml-2" />
        </nav>
      </div>
      <div className="flag-ribbon" aria-hidden="true" />
    </header>
  );
}
