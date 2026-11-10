"use client";

import { useEffect, useState } from "react";
import { LANGS, useI18n, type Lang } from "@/lib/i18n";
import { THEME_KEY, type Theme } from "@/lib/theme";

function currentTheme(): Theme {
  return document.documentElement.dataset.theme === "dark" ? "dark" : "light";
}

/** Language picker and light/dark switch, shared by the public header, the private sidebar and the login page. */
export function Preferences({ className = "" }: { className?: string }) {
  const { lang, setLang, t } = useI18n();
  const [theme, setTheme] = useState<Theme | null>(null); // null until mounted: the server cannot know the theme

  useEffect(() => {
    const timer = window.setTimeout(() => setTheme(currentTheme()), 0);
    return () => window.clearTimeout(timer);
  }, []);

  const toggleTheme = (): void => {
    const next: Theme = currentTheme() === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    try { window.localStorage.setItem(THEME_KEY, next); } catch { /* storage blocked: choice lasts for this visit */ }
    setTheme(next);
  };

  const dark: boolean = theme === "dark";
  return <div className={`flex items-center gap-2 ${className}`}>
    <select value={lang} onChange={(event) => setLang(event.target.value as Lang)} aria-label={t("Til")}
      className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm text-slate-700">
      {LANGS.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
    </select>
    <button type="button" onClick={toggleTheme} aria-pressed={dark}
      aria-label={dark ? t("Kunduzgi rejimga o'tish") : t("Qorong'i rejimga o'tish")} title={dark ? t("Kunduzgi rejim") : t("Qorong'i rejim")}
      className="rounded-md border border-slate-300 bg-white px-2 py-1 text-sm leading-none text-slate-700">
      <span aria-hidden="true">{dark ? "☀️" : "🌙"}</span>
    </button>
  </div>;
}
