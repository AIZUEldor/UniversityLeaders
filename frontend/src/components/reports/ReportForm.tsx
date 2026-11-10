"use client";

import { useState, type FormEvent } from "react";
import { inputClass, primaryClass } from "@/components/admin/ManagementUi";
import { useT } from "@/lib/i18n";
import type { ReportContent, ReportDetail } from "@/lib/reportsApi";

export function ReportForm({ initial, busy, onSave }: { initial?: ReportDetail; busy: boolean; onSave: (content: ReportContent, submit: boolean) => Promise<void> }) {
  const t = useT();
  const [title, setTitle] = useState(initial?.summary.title ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [activityDate, setActivityDate] = useState(initial?.summary.activityDate ?? "");
  const [outcome, setOutcome] = useState(initial?.outcome ?? "");
  const [participantCount, setParticipantCount] = useState(initial?.participantCount?.toString() ?? "");

  const save = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    const submitter = (event.nativeEvent as SubmitEvent).submitter as HTMLButtonElement | null;
    const submit = submitter?.value === "submit";
    void onSave({
      title: title.trim(),
      description: description.trim() || null,
      activityDate: activityDate || null,
      outcome: outcome.trim() || null,
      participantCount: participantCount === "" ? null : Number(participantCount),
    }, submit);
  };

  return <form onSubmit={save} className="space-y-4">
    <label className="block text-sm font-medium">{t("Ish nomi")} <span className="text-red-700">*</span><input required maxLength={300} value={title} onChange={(event) => setTitle(event.target.value)} className={`${inputClass} mt-1`} /></label>
    <label className="block text-sm font-medium">{t("Tavsif")}<textarea maxLength={4000} rows={4} value={description} onChange={(event) => setDescription(event.target.value)} className={`${inputClass} mt-1`} /></label>
    <div className="grid gap-4 sm:grid-cols-2">
      <label className="block text-sm font-medium">{t("Faoliyat sanasi")}<input type="date" value={activityDate} onChange={(event) => setActivityDate(event.target.value)} className={`${inputClass} mt-1`} /></label>
      <label className="block text-sm font-medium">{t("Ishtirokchilar soni")}<input type="number" min={0} max={1000000} step={1} value={participantCount} onChange={(event) => setParticipantCount(event.target.value)} className={`${inputClass} mt-1`} /></label>
    </div>
    <label className="block text-sm font-medium">{t("Natija")}<textarea maxLength={4000} rows={3} value={outcome} onChange={(event) => setOutcome(event.target.value)} className={`${inputClass} mt-1`} /></label>
    <p className="text-xs text-slate-500">{t("Yuborishdan oldin tavsif, faoliyat sanasi va natija to'ldirilishi kerak. Qoralamani oldin saqlang.")}</p>
    <div className="flex flex-wrap gap-3">
      <button type="submit" value="save" disabled={busy} className={primaryClass}>{t(busy ? "Saqlanmoqda..." : initial ? "O'zgarishlarni saqlash" : "Qoralama yaratish")}</button>
      {initial && <button type="submit" value="submit" disabled={busy || !description.trim() || !outcome.trim() || !activityDate} className="rounded-md bg-emerald-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">{t(initial.summary.status === "Rejected" ? "Saqlash va qayta yuborish" : "Saqlash va yuborish")}</button>}
    </div>
  </form>;
}
