"use client";

import { useCallback, useEffect, useState } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { useT } from "@/lib/i18n";
import { reportsApi, type ReportStatus, type ReportSummary } from "@/lib/reportsApi";
import { ReportList } from "./ReportList";

type ReviewStatus = Exclude<ReportStatus, "Draft">;

export function ReportsCollection({ source, detailBase }: { source: "mine" | "review"; detailBase: string }) {
  const t = useT();
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [status, setStatus] = useState<ReviewStatus>("Submitted");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try { setReports(source === "mine" ? await reportsApi.mine() : await reportsApi.reviewQueue(status)); setError(null); }
    catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, [source, status]);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  return <div className="space-y-4">
    {source === "review" && <label className="block text-sm">{t("Holat")}
      <select value={status} onChange={(event) => setStatus(event.target.value as ReviewStatus)} className="ml-2 rounded-md border border-slate-300 bg-white px-3 py-2">
        <option value="Submitted">{t("Ko'rib chiqilmoqda")}</option><option value="Approved">{t("Tasdiqlangan")}</option><option value="Rejected">{t("Rad etilgan")}</option>
      </select>
    </label>}
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">{t("Hisobotlar yuklanmoqda...")}</p>
      : error !== null && reports.length === 0 ? null
      : reports.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">{t(source === "mine" ? "Hozircha hisobotlaringiz yo'q." : "Bu holatda ko'rib chiqiladigan hisobot yo'q.")}</p>
      : <ReportList reports={reports} detailBase={detailBase} showAuthor={source === "review"} />}
  </div>;
}
