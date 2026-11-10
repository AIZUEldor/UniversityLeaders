"use client";

import { useState, type ChangeEvent } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { ApiError } from "@/lib/api";
import { useT } from "@/lib/i18n";
import { attachmentsApi, type ReportDetail } from "@/lib/reportsApi";

const maxBytes = 10 * 1024 * 1024;
const allowedName = /\.(jpe?g|png|pdf|docx)$/i;

export function ReportAttachments({ detail, canEdit, onChanged }: { detail: ReportDetail; canEdit: boolean; onChanged: () => Promise<void> }) {
  const t = useT();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const reportId = detail.summary.id;

  const upload = async (event: ChangeEvent<HTMLInputElement>): Promise<void> => {
    const files = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (files.length === 0) return;
    if (files.length + detail.attachments.length > 10) { setError(new ApiError(409, "attachment_limit_reached", "Too many files")); return; }
    if (files.some((file) => file.size < 1 || file.size > maxBytes || file.name.length > 255 || !allowedName.test(file.name))) {
      setError(new ApiError(400, "validation_failed", "Invalid file", { file: ["JPG, PNG, PDF yoki DOCX fayl tanlang; har biri 1 baytdan 10 MB gacha bo'lishi kerak."] }));
      return;
    }
    setBusy(true); setError(null);
    try {
      for (const file of files) await attachmentsApi.upload(reportId, file);
    } catch (failure: unknown) { setError(failure); }
    finally { await onChanged(); setBusy(false); }
  };

  const remove = async (attachmentId: string): Promise<void> => {
    if (!window.confirm(t("Faylni olib tashlashni tasdiqlaysizmi?"))) return;
    setBusy(true); setError(null);
    try { await attachmentsApi.remove(reportId, attachmentId); }
    catch (failure: unknown) { setError(failure); }
    finally { await onChanged(); setBusy(false); }
  };

  const download = async (attachmentId: string, fileName: string): Promise<void> => {
    setBusy(true); setError(null);
    try {
      const blob = await attachmentsApi.download(reportId, attachmentId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (failure: unknown) { setError(failure); }
    finally { setBusy(false); }
  };

  return <section id="attachments" className="space-y-3">
    <div><h2 className="font-semibold">{t("Biriktirilgan fayllar")} ({detail.attachments.length}/10)</h2><p className="text-xs text-slate-500">{t("Yuklab olish faqat muallif va mas'ul tasdiqlovchiga ruxsat etiladi.")}</p></div>
    {error !== null && <ManagementMessage error={error} />}
    {detail.attachments.length === 0 ? <p className="text-sm text-slate-500">{t("Fayllar yo'q.")}</p> : <ul className="space-y-2">{detail.attachments.map((file) => <li key={file.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-slate-50 p-3 text-sm">
      <span className="min-w-0 break-all">{file.fileName} <span className="text-xs text-slate-500">({(file.sizeBytes / 1024).toFixed(1)} KB)</span></span>
      <span className="flex gap-3"><button type="button" disabled={busy} onClick={() => void download(file.id, file.fileName)} className="text-indigo-700 hover:underline">{t("Yuklab olish")}</button>{canEdit && <button type="button" disabled={busy} onClick={() => void remove(file.id)} className="text-red-700 hover:underline">{t("Olib tashlash")}</button>}</span>
    </li>)}</ul>}
    {canEdit && <label className="block rounded-xl border-2 border-dashed border-slate-300 p-4 text-center text-sm text-slate-600">
      <span>{t("JPG, PNG, PDF yoki DOCX fayl tanlang (har biri 10 MB gacha)")}</span>
      <input type="file" multiple accept=".jpg,.jpeg,.png,.pdf,.docx" disabled={busy || detail.attachments.length >= 10} onChange={(event) => void upload(event)} className="mt-2 block w-full text-sm" />
    </label>}
    {busy && <p role="status" className="text-sm text-slate-500">{t("Fayl amali bajarilmoqda...")}</p>}
  </section>;
}
