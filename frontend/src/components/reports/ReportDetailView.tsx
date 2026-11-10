"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ManagementMessage, ManagementPanel } from "@/components/admin/ManagementUi";
import { ReportAttachments } from "./ReportAttachments";
import { ReportForm } from "./ReportForm";
import { ReportStatusBadge } from "./ReportList";
import { ReviewActions } from "./ReviewActions";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { reportsApi, type ReportContent, type ReportDetail, type ScoreBreakdownItem } from "@/lib/reportsApi";

export function ReportDetailView({ id, mode, backHref }: { id: string; mode: "author" | "reviewer"; backHref: string }) {
  const { state } = useAuth();
  const [detail, setDetail] = useState<ReportDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(async (): Promise<void> => {
    setLoading(true);
    try { setDetail(await reportsApi.get(id)); setError(null); }
    catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, [id]);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const run = async (action: () => Promise<ReportDetail>): Promise<void> => {
    setBusy(true); setError(null);
    try { setDetail(await action()); }
    catch (failure: unknown) {
      if (failure instanceof ApiError && failure.status === 409) await load();
      setError(failure);
    } finally { setBusy(false); }
  };

  if (loading && detail === null) return <p role="status" className="text-sm text-slate-500">Hisobot yuklanmoqda...</p>;
  if (detail === null) return <ManagementMessage error={error} onRetry={() => void load()} />;

  const summary = detail.summary;
  const isOwn = state.status === "authenticated" && state.user.id === summary.authorId;
  const editable = isOwn && (summary.status === "Draft" || summary.status === "Rejected");
  // Only the University Leader reviews (the API enforces it); older coordinator reports stay reviewable as history.
  const canReview = state.status === "authenticated" && state.user.role === "university_leader";
  const save = (content: ReportContent, submit: boolean): Promise<void> => run(async () => {
    const updated = await reportsApi.update(id, content, summary.version);
    if (!submit) return updated;
    try { return await reportsApi.submit(id, updated.summary.version); }
    catch (failure: unknown) { setDetail(updated); throw failure; }
  });

  return <div className="space-y-6">
    <Link href={backHref} className="text-sm text-indigo-700 hover:underline">← Ro&apos;yxatga qaytish</Link>
    <div className="flex flex-wrap items-start justify-between gap-3"><div><h1 className="text-2xl font-bold">{summary.title}</h1><p className="text-sm text-slate-500">{summary.authorName} · {summary.updatedAt.slice(0, 10)}</p></div><ReportStatusBadge value={summary.status} /></div>
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    {mode === "author" && editable ? <ManagementPanel>
      <h2 className="mb-4 font-semibold">{summary.status === "Rejected" ? "Rad etilgan hisobotni tuzatish" : "Qoralamani tahrirlash"}</h2>
      <ReportForm key={`${id}-${summary.version}`} initial={detail} busy={busy} onSave={save} />
    </ManagementPanel> : <ManagementPanel>
      <h2 className="mb-3 font-semibold">Hisobot tafsilotlari</h2>
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <div><dt className="text-slate-500">Faoliyat sanasi</dt><dd>{summary.activityDate ?? "—"}</dd></div>
        <div><dt className="text-slate-500">Ishtirokchilar</dt><dd>{detail.participantCount ?? "—"}</dd></div>
        <div className="sm:col-span-2"><dt className="text-slate-500">Tavsif</dt><dd className="whitespace-pre-wrap">{detail.description ?? "—"}</dd></div>
        <div className="sm:col-span-2"><dt className="text-slate-500">Natija</dt><dd className="whitespace-pre-wrap">{detail.outcome ?? "—"}</dd></div>
        {summary.score !== null && <div><dt className="text-slate-500">Ball</dt><dd>{summary.score}/10</dd></div>}
      </dl>
    </ManagementPanel>}
    <ManagementPanel><ReportAttachments detail={detail} canEdit={mode === "author" && editable} onChanged={load} /></ManagementPanel>
    {detail.reviews.length > 0 && <ManagementPanel>
      <h2 className="mb-3 font-semibold">Ko&apos;rib chiqish tarixi</h2>
      <ol className="space-y-3">{detail.reviews.map((review) => <li key={review.id} className="rounded-lg border border-slate-200 p-3 text-sm">
        <p className="font-medium">{review.action === "Approved" ? "Tasdiqlangan" : "Rad etilgan"} · {review.reviewerName} · {review.createdAt.slice(0, 10)}{review.score !== null && ` · ${review.score}/10`}</p>
        {review.comment && <p className="mt-1 whitespace-pre-wrap text-slate-700">{review.comment}</p>}
        {review.scoreBreakdown.length > 0 && <ul className="mt-2 list-inside list-disc text-slate-600">{review.scoreBreakdown.map((item, index) => <li key={index}>{item.criterion}: {item.points}</li>)}</ul>}
      </li>)}</ol>
    </ManagementPanel>}
    {mode === "reviewer" && canReview && summary.status === "Submitted" && <ManagementPanel>
      <ReviewActions busy={busy} onApprove={(score: number, items: ScoreBreakdownItem[], comment: string | null) => run(() => reportsApi.approve(id, summary.version, score, items, comment))} onReject={(reason: string) => run(() => reportsApi.reject(id, summary.version, reason))} />
    </ManagementPanel>}
  </div>;
}
