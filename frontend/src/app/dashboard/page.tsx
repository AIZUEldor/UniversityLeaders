"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { useAuth } from "@/lib/auth";
import { ROLE_LABELS } from "@/lib/data";
import { reportsApi, type ReportSummary } from "@/lib/reportsApi";

export default function Dashboard() {
  const { state } = useAuth();
  const [mine, setMine] = useState<ReportSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try { setMine(await reportsApi.mine()); setError(null); }
    catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const user = state.status === "authenticated" ? state.user : null;
  const stats = [
    { label: "Mening hisobotlarim", value: mine.length },
    { label: "Qoralamalarim", value: mine.filter((report) => report.status === "Draft").length },
    { label: "Ko'rib chiqilmoqda", value: mine.filter((report) => report.status === "Submitted").length },
    { label: "Tasdiqlangan ishlar", value: mine.filter((report) => report.status === "Approved").length },
    { label: "Rad etilgan (tuzatish kerak)", value: mine.filter((report) => report.status === "Rejected").length },
  ];
  return <div className="space-y-6">
    <div><h1 className="text-2xl font-bold">Xush kelibsiz{user ? `, ${user.fullName}` : ""}</h1>{user && <p className="text-sm text-slate-500">{ROLE_LABELS[user.role]}</p>}</div>
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">Hisobotlar yuklanmoqda...</p> : <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{stats.map((stat) => <div key={stat.label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><div className="text-2xl font-bold text-indigo-700">{stat.value}</div><div className="text-xs text-slate-500">{stat.label}</div></div>)}</div>}
    <div className="flex flex-wrap gap-4 text-sm"><Link href="/dashboard/coordinators" className="text-indigo-700 hover:underline">Koordinatorlar →</Link><Link href="/dashboard/reports" className="text-indigo-700 hover:underline">Yangi hisobot →</Link><Link href="/dashboard/my-work" className="text-indigo-700 hover:underline">Mening hisobotlarim →</Link><Link href="/dashboard/ranking" className="text-indigo-700 hover:underline">Reyting →</Link></div>
  </div>;
}
