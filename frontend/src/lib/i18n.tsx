"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { TRANSLATIONS } from "./translations";

// The Uzbek text is the key: uz needs no dictionary and a missing ru/en entry falls back to Uzbek, never to a blank.
// `{name}` placeholders are filled from `vars` in every language.

export type Lang = "uz" | "ru" | "en";
export const LANGS: { value: Lang; label: string }[] = [
  { value: "uz", label: "O'zb" },
  { value: "ru", label: "Рус" },
  { value: "en", label: "Eng" },
];
const STORAGE_KEY = "lang";

export type Translate = (uz: string, vars?: Record<string, string | number>) => string;

interface LanguageContextValue { lang: Lang; setLang: (lang: Lang) => void; t: Translate; }

export function translate(lang: Lang, uz: string, vars?: Record<string, string | number>): string {
  const text: string = lang === "uz" ? uz : TRANSLATIONS[lang][uz] ?? uz;
  return vars ? text.replace(/\{(\w+)\}/g, (match: string, name: string) => (name in vars ? String(vars[name]) : match)) : text;
}

const LanguageContext = createContext<LanguageContextValue>({ lang: "uz", setLang: () => {}, t: (uz, vars) => translate("uz", uz, vars) });

function isLang(value: string | null): value is Lang {
  return value === "uz" || value === "ru" || value === "en";
}

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>("uz");

  // shortcut: the server always renders Uzbek and the saved language is applied after hydration (a brief flash for
  // ru/en readers); move the choice to a cookie read on the server if that flash becomes a problem.
  useEffect(() => {
    let saved: string | null = null;
    try { saved = window.localStorage.getItem(STORAGE_KEY); } catch { /* storage blocked: stay on Uzbek */ }
    if (isLang(saved) && saved !== "uz") {
      const timer = window.setTimeout(() => setLangState(saved), 0);
      return () => window.clearTimeout(timer);
    }
  }, []);

  useEffect(() => { document.documentElement.lang = lang; }, [lang]);

  const setLang = useCallback((next: Lang): void => {
    setLangState(next);
    try { window.localStorage.setItem(STORAGE_KEY, next); } catch { /* storage blocked: choice lasts for this visit */ }
  }, []);

  const t: Translate = useCallback((uz: string, vars?: Record<string, string | number>) => translate(lang, uz, vars), [lang]);
  const value: LanguageContextValue = useMemo(() => ({ lang, setLang, t }), [lang, setLang, t]);
  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useI18n(): LanguageContextValue {
  return useContext(LanguageContext);
}

export function useT(): Translate {
  return useContext(LanguageContext).t;
}

/** Translated text for server components, which cannot call hooks. */
export function T({ children, vars }: { children: string; vars?: Record<string, string | number> }) {
  return <>{useT()(children, vars)}</>;
}
