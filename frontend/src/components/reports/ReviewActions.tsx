"use client";

import { useState, type FormEvent } from "react";
import { inputClass, primaryClass, secondaryClass } from "@/components/admin/ManagementUi";
import { useT } from "@/lib/i18n";
import { approvalError, type ScoreBreakdownItem } from "@/lib/reportsApi";

export function ReviewActions({ busy, onApprove, onReject }: {
  busy: boolean;
  onApprove: (score: number, items: ScoreBreakdownItem[], comment: string | null) => Promise<void>;
  onReject: (reason: string) => Promise<void>;
}) {
  const t = useT();
  const [score, setScore] = useState("0");
  const [items, setItems] = useState<{ criterion: string; points: string }[]>([{ criterion: "", points: "0" }]);
  const [comment, setComment] = useState("");
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");

  const approve = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    const parsed = items.map((item) => ({ criterion: item.criterion.trim(), points: Number(item.points) }));
    const message = approvalError(Number(score), parsed);
    if (message) { setError(message); return; }
    setError("");
    void onApprove(Number(score), parsed, comment.trim() || null);
  };

  const reject = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    if (!reason.trim()) { setError("Rad etish sababini kiriting."); return; }
    setError("");
    void onReject(reason.trim());
  };

  return <section className="grid gap-5 lg:grid-cols-2">
    <form onSubmit={approve} className="space-y-3 rounded-xl border border-emerald-200 p-4">
      <h2 className="font-semibold">{t("Tasdiqlash")}</h2>
      <label className="block text-sm">{t("Umumiy ball (0–10)")}<input type="number" min={0} max={10} step={1} required value={score} onChange={(event) => setScore(event.target.value)} className={`${inputClass} mt-1`} /></label>
      <div className="space-y-2"><p className="text-sm">{t("Baholash mezonlari (1–10 ta)")}</p>{items.map((item, index) => <div key={index} className="flex flex-wrap gap-2">
        <input aria-label={t("{n}-mezon", { n: index + 1 })} placeholder={t("Mezon")} required maxLength={100} value={item.criterion} onChange={(event) => setItems(items.map((row, i) => i === index ? { ...row, criterion: event.target.value } : row))} className={`${inputClass} min-w-40 flex-1`} />
        <input aria-label={t("{n}-mezon balli", { n: index + 1 })} type="number" min={0} max={10} step={1} required value={item.points} onChange={(event) => setItems(items.map((row, i) => i === index ? { ...row, points: event.target.value } : row))} className={`${inputClass} w-24`} />
        {items.length > 1 && <button type="button" onClick={() => setItems(items.filter((_, i) => i !== index))} className={secondaryClass} aria-label={t("{n}-mezonni olib tashlash", { n: index + 1 })}>−</button>}
      </div>)}</div>
      <button type="button" disabled={items.length >= 10} onClick={() => setItems([...items, { criterion: "", points: "0" }])} className={secondaryClass}>{t("Mezon qo'shish")}</button>
      <label className="block text-sm">{t("Izoh (ixtiyoriy)")}<textarea maxLength={2000} rows={2} value={comment} onChange={(event) => setComment(event.target.value)} className={`${inputClass} mt-1`} /></label>
      <button type="submit" disabled={busy} className={primaryClass}>{t("Tasdiqlash")}</button>
    </form>
    <form onSubmit={reject} className="space-y-3 rounded-xl border border-rose-200 p-4">
      <h2 className="font-semibold">{t("Rad etish")}</h2>
      <label className="block text-sm">{t("Sabab")} <span className="text-red-700">*</span><textarea required maxLength={2000} rows={5} value={reason} onChange={(event) => setReason(event.target.value)} className={`${inputClass} mt-1`} /></label>
      <button type="submit" disabled={busy} className="rounded-md bg-rose-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">{t("Rad etish")}</button>
    </form>
    {error && <p role="alert" className="text-sm text-rose-700 lg:col-span-2">{t(error)}</p>}
  </section>;
}
