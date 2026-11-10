"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { facultiesApi, facultyLeadersApi } from "@/lib/adminApi";
import { useT } from "@/lib/i18n";
import { reportsApi } from "@/lib/reportsApi";

export default function AdminHome() {
  const t = useT();
  const [counts, setCounts] = useState({ faculties: 0, facultyLeaders: 0, pending: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [faculties, leaders, queue] = await Promise.all([facultiesApi.list(), facultyLeadersApi.list(false), reportsApi.reviewQueue()]);
      setCounts({ faculties: faculties.length, facultyLeaders: leaders.length, pending: queue.length });
      setError(null);
    } catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const stats = [
    { label: "Fakultetlar", value: counts.faculties },
    { label: "Faol fakultet yetakchilari", value: counts.facultyLeaders },
    { label: "Tasdiqlash navbati", value: counts.pending },
  ];
  return <div className="space-y-6"><h1 className="text-2xl font-bold">{t("Umumiy ko'rinish")}</h1>
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">{t("Ma'lumotlar yuklanmoqda...")}</p> : <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{stats.map((stat) => <div key={stat.label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><div className="text-2xl font-bold text-indigo-700">{stat.value}</div><div className="text-xs text-slate-500">{t(stat.label)}</div></div>)}</div>}
    <Link href="/admin/approvals" className="inline-block text-sm text-indigo-700 hover:underline">{t("Hisobotlarni ko'rib chiqish →")}</Link>
  </div>;
}
