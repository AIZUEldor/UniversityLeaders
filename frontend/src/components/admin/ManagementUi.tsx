"use client";

import { useState, type ReactNode } from "react";
import { describeError } from "@/lib/auth";
import { useT } from "@/lib/i18n";

export const inputClass = "w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none";
export const primaryClass = "rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white disabled:opacity-50";
export const secondaryClass = "rounded-md border border-slate-300 bg-white px-4 py-2 text-sm text-slate-700 disabled:opacity-50";

export function ManagementPanel({ children }: { children: ReactNode }) {
  return <div className="rounded-2xl border border-slate-200/80 bg-white/90 p-5 shadow-sm backdrop-blur">{children}</div>;
}

export function ManagementMessage({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const t = useT();
  return <div role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-800">
    {t(describeError(error))}
    {onRetry && <button type="button" onClick={onRetry} className="ml-3 font-medium underline">{t("Qayta urinish")}</button>}
  </div>;
}

export function TemporaryPassword({ userName, password, onClose }: { userName: string; password: string; onClose: () => void }) {
  const t = useT();
  const [copyStatus, setCopyStatus] = useState<"idle" | "copied" | "failed">("idle");
  const copy = async (): Promise<void> => {
    try { await navigator.clipboard.writeText(password); setCopyStatus("copied"); }
    catch { setCopyStatus("failed"); }
  };
  return <div role="dialog" aria-modal="true" aria-label={t("Vaqtinchalik parol")} className="fixed inset-0 z-50 flex items-center justify-center overflow-y-auto bg-black/60 p-4">
    <div className="max-h-[calc(100dvh-2rem)] w-full max-w-md overflow-y-auto rounded-2xl bg-white p-6 shadow-xl">
      <h2 className="text-lg font-bold">{t("Vaqtinchalik parol")}</h2>
      <p className="mt-2 text-sm text-slate-600">{t("Bu parol faqat hozir ko'rsatiladi. Uni foydalanuvchiga xavfsiz tarzda yetkazing. Birinchi kirishda parolni almashtirish talab qilinadi.")}</p>
      <p className="mt-4 text-sm">{t("Login")}: <strong>{userName}</strong></p>
      <p className="mt-2 break-all rounded-md bg-slate-100 p-3 font-mono text-base select-all">{password}</p>
      <p className="mt-2 text-xs text-amber-800">{t("Nusxalangan parol buferda qolishi mumkin. Ishlatgandan keyin buferni tozalang.")}</p>
      <div className="mt-5 flex flex-wrap gap-2"><button type="button" onClick={() => void copy()} className={secondaryClass}>{t("Nusxalash")}</button><button type="button" onClick={onClose} className={primaryClass}>{t("Yopish")}</button></div>
      {copyStatus !== "idle" && <p role="status" className="mt-2 text-sm">{copyStatus === "copied" ? t("Parol nusxalandi.") : t("Nusxalab bo'lmadi. Parolni qo'lda belgilang.")}</p>}
    </div>
  </div>;
}

/** Previous/next paging for API PagedResponse lists; renders nothing for a single page. */
export function Pager({ page, total, pageSize, label, onPage }: { page: number; total: number; pageSize: number; label: string; onPage: (page: number) => void }) {
  const t = useT();
  const totalPages: number = Math.ceil(total / pageSize);
  if (totalPages <= 1) return null;
  return <nav aria-label={t(label)} className="flex items-center justify-center gap-3 text-sm">
    <button type="button" disabled={page <= 1} onClick={() => onPage(page - 1)} className="rounded-md border border-slate-300 px-3 py-2 disabled:opacity-50">{t("Oldingi")}</button>
    <span>{page} / {totalPages}</span>
    <button type="button" disabled={page >= totalPages} onClick={() => onPage(page + 1)} className="rounded-md border border-slate-300 px-3 py-2 disabled:opacity-50">{t("Keyingi")}</button>
  </nav>;
}
