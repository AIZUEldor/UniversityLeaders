"use client";

import { useCallback, useEffect, useState } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { reportsApi, type ReportSummary } from "@/lib/reportsApi";
import { ReportDetailView } from "./ReportDetailView";

export function MediaWorkspace() {
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [selected, setSelected] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try { setReports(await reportsApi.mine()); setError(null); }
    catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  return <div className="space-y-5">
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">Hisobotlar yuklanmoqda...</p> : <>
      {error !== null && reports.length === 0 ? null : reports.length === 0 ? <p className="text-sm text-slate-500">Avval hisobot qoralamasini yarating.</p> : <label className="block text-sm font-medium">Hisobotni tanlang
        <select value={selected} onChange={(event) => setSelected(event.target.value)} className="mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2">
          <option value="">Tanlang</option>{reports.map((report) => <option key={report.id} value={report.id}>{report.title} — {report.status}</option>)}
        </select>
      </label>}
      {selected && <ReportDetailView key={selected} id={selected} mode="author" backHref="/dashboard/my-work" />}
    </>}
  </div>;
}
