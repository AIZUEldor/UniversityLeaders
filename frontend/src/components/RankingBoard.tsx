"use client";

import { useCallback, useState } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { publicApi } from "@/lib/publicApi";
import { orderRanking, type RankedRow, type RankingMetric } from "@/lib/ranking";
import { usePublicRequest } from "@/lib/usePublicRequest";

const METRICS: { value: RankingMetric; label: string }[] = [
  { value: "totalScore", label: "Umumiy ball (rasmiy)" },
  { value: "approvedReports", label: "Tasdiqlangan ishlar soni" },
];

/** Faculty ranking from GET /api/public/faculty-rankings; shared by the public site and the admin area. */
export function RankingBoard() {
  const [metric, setMetric] = useState<RankingMetric>("totalScore");
  const request = useCallback(() => publicApi.rankings(), []);
  const { data, loading, error, reload } = usePublicRequest(request);
  const rows: RankedRow[] = orderRanking(data ?? [], metric);
  const max: number = Math.max(1, ...rows.map((row: RankedRow) => row[metric]));
  const official: boolean = metric === "totalScore";

  return <div className="space-y-4">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <p className="text-sm text-slate-600">{official
        ? "Rasmiy reyting: tasdiqlangan hisobotlarning umumiy bali bo'yicha."
        : "Qo'shimcha ko'rinish: tasdiqlangan ishlar soni bo'yicha tartib. Rasmiy o'rin alohida ustunda ko'rsatilgan."}</p>
      <div role="group" aria-label="Reyting ko'rsatkichi" className="flex rounded-lg border border-slate-300 bg-white p-1 text-sm">
        {METRICS.map((item) => <button key={item.value} type="button" onClick={() => setMetric(item.value)} aria-pressed={metric === item.value}
          className={`rounded-md px-3 py-1 ${metric === item.value ? "bg-indigo-600 text-white" : "text-slate-600"}`}>{item.label}</button>)}
      </div>
    </div>
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">Reyting yuklanmoqda...</p>
      : error !== null && data === null ? null
      : rows.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">Hozircha fakultetlar yo&apos;q.</p>
      : <>
        <ol className="space-y-2 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm md:p-6">{rows.map((row: RankedRow) =>
          <li key={row.facultyId} className="grid grid-cols-[2.5rem_minmax(5rem,1fr)_minmax(4rem,2fr)_3rem] items-center gap-2 rounded-md py-1 text-sm">
            <span className={`font-bold ${row.shownPosition <= 3 ? "text-amber-600" : "text-slate-400"}`}>#{row.shownPosition}</span>
            <span className="truncate font-medium" title={row.facultyName}>{row.facultyName}</span>
            <div className="h-6 overflow-hidden rounded bg-slate-100" aria-hidden="true"><div className="h-full rounded bg-gradient-to-r from-indigo-500 to-violet-500" style={{ width: `${row[metric] / max * 100}%` }} /></div>
            <span className="text-right font-semibold tabular-nums">{row[metric]}</span>
          </li>)}
        </ol>
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm"><table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-slate-500"><tr>
            <th className="px-4 py-2">{official ? "Rasmiy o'rin" : "O'rin (ishlar soni)"}</th>
            <th className="px-4 py-2">Fakultet</th>
            <th className="px-4 py-2 text-right">Tasdiqlangan ishlar</th>
            <th className="px-4 py-2 text-right">Umumiy ball</th>
            {!official && <th className="px-4 py-2 text-right">Rasmiy o&apos;rin</th>}
          </tr></thead>
          <tbody>{rows.map((row: RankedRow) => <tr key={row.facultyId} className="border-t">
            <td className="px-4 py-2 font-semibold">{row.shownPosition}</td>
            <td className="px-4 py-2">{row.facultyName}</td>
            <td className="px-4 py-2 text-right tabular-nums">{row.approvedReports}</td>
            <td className="px-4 py-2 text-right tabular-nums">{row.totalScore}</td>
            {!official && <td className="px-4 py-2 text-right tabular-nums">{row.position}</td>}
          </tr>)}</tbody>
        </table></div>
      </>}
  </div>;
}
