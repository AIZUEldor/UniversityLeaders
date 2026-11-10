"use client";

import { useCallback, useState } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { useT } from "@/lib/i18n";
import { publicApi } from "@/lib/publicApi";
import { orderRanking, type RankedRow, type RankingMetric } from "@/lib/ranking";
import { usePublicRequest } from "@/lib/usePublicRequest";

const METRICS: { value: RankingMetric; label: string }[] = [
  { value: "totalScore", label: "Umumiy ball (rasmiy)" },
  { value: "approvedReports", label: "Tasdiqlangan ishlar soni" },
];

/** Faculty ranking from GET /api/public/faculty-rankings; shared by the public site and the admin area. */
export function RankingBoard() {
  const t = useT();
  const [metric, setMetric] = useState<RankingMetric>("totalScore");
  const request = useCallback(() => publicApi.rankings(), []);
  const { data, loading, error, reload } = usePublicRequest(request);
  const rows: RankedRow[] = orderRanking(data ?? [], metric);
  const max: number = Math.max(1, ...rows.map((row: RankedRow) => row[metric]));
  const official: boolean = metric === "totalScore";

  return <div className="space-y-4">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <p className="text-sm text-slate-600">{official
        ? t("Rasmiy reyting: tasdiqlangan hisobotlarning umumiy bali bo'yicha.")
        : t("Qo'shimcha ko'rinish: tasdiqlangan ishlar soni bo'yicha tartib. Rasmiy o'rin alohida ustunda ko'rsatilgan.")}</p>
      <div role="group" aria-label={t("Reyting ko'rsatkichi")} className="flex w-full rounded-lg border border-slate-300 bg-white p-1 text-sm sm:w-auto">
        {METRICS.map((item) => <button key={item.value} type="button" onClick={() => setMetric(item.value)} aria-pressed={metric === item.value}
          className={`min-w-0 flex-1 rounded-md px-2 py-1 sm:flex-none sm:px-3 ${metric === item.value ? "bg-indigo-600 text-white" : "text-slate-600"}`}>{t(item.label)}</button>)}
      </div>
    </div>
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">{t("Reyting yuklanmoqda...")}</p>
      : error !== null && data === null ? null
      : rows.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">{t("Hozircha fakultetlar yo'q.")}</p>
      : <>
        {rows.every((row: RankedRow) => row.totalScore === 0) && <p className="fade-up rounded-xl border border-indigo-100 bg-indigo-50/80 px-4 py-3 text-sm text-indigo-900">{t("Hali tasdiqlangan hisobot yo'q: barcha fakultetlar 0 ball bilan teng turibdi. Birinchi tasdiqlangan ish reytingni boshlab beradi.")}</p>}
        <ol className="fade-up space-y-2 rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur md:p-6">{rows.map((row: RankedRow) =>
          <li key={row.facultyId} className="grid grid-cols-[2rem_minmax(0,1fr)_2.5rem] items-center gap-x-2 gap-y-1 rounded-md border-b border-slate-100 py-2 text-sm last:border-0 sm:grid-cols-[2.5rem_minmax(5rem,1fr)_minmax(4rem,2fr)_3rem] sm:border-0 sm:py-1">
            <span className={`font-bold ${row.shownPosition <= 3 && row[metric] > 0 ? "text-amber-600" : "text-slate-400"}`}>{row.shownPosition <= 3 && row[metric] > 0 ? ["🥇", "🥈", "🥉"][row.shownPosition - 1] : `#${row.shownPosition}`}</span>
            <span className="min-w-0 break-words font-medium sm:truncate" title={row.facultyName}>{row.facultyName}</span>
            <div className="col-span-2 col-start-2 row-start-2 h-2 overflow-hidden rounded bg-slate-100 sm:col-span-1 sm:col-start-3 sm:row-start-1 sm:h-6" aria-hidden="true"><div className="h-full rounded bg-gradient-to-r from-indigo-500 to-violet-500" style={{ width: `${row[metric] / max * 100}%` }} /></div>
            <span className="col-start-3 row-start-1 text-right font-semibold tabular-nums sm:col-start-4">{row[metric]}</span>
          </li>)}
        </ol>
        <div className="fade-up hidden overflow-x-auto rounded-2xl border border-slate-200/80 bg-white/85 shadow-sm backdrop-blur sm:block"><table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-slate-500"><tr>
            <th className="px-4 py-2">{t(official ? "Rasmiy o'rin" : "O'rin (ishlar soni)")}</th>
            <th className="px-4 py-2">{t("Fakultet")}</th>
            <th className="px-4 py-2 text-right">{t("Tasdiqlangan ishlar")}</th>
            <th className="px-4 py-2 text-right">{t("Umumiy ball")}</th>
            {!official && <th className="px-4 py-2 text-right">{t("Rasmiy o'rin")}</th>}
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
