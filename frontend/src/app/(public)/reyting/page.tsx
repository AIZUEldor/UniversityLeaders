"use client";

import { useState } from "react";
import { computeRanking, type RankRow } from "@/lib/data";
import { useStore } from "@/lib/store";

type Metric = "score" | "approved";

export default function Ranking() {
  const { reports } = useStore();
  const [metric, setMetric] = useState<Metric>("score");
  const [hover, setHover] = useState<number | null>(null);

  const rows: RankRow[] = computeRanking(reports);
  const max: number = Math.max(1, ...rows.map((r: RankRow) => r[metric]));

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Fakultetlar reytingi</h1>
          <p className="text-sm text-slate-500">Faqat tasdiqlangan hisobotlar hisoblanadi (demo ma&apos;lumot).</p>
        </div>
        <div className="flex rounded-lg border border-slate-300 bg-white p-1 text-sm">
          {(["score", "approved"] as Metric[]).map((m: Metric) => (
            <button
              key={m}
              onClick={() => setMetric(m)}
              className={`rounded-md px-3 py-1 ${metric === m ? "bg-indigo-600 text-white" : "text-slate-600"}`}
            >
              {m === "score" ? "Umumiy ball" : "Tasdiqlangan ishlar"}
            </button>
          ))}
        </div>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm md:p-6">
        <ul className="space-y-2">
          {rows.map((r: RankRow) => (
            <li
              key={r.faculty.id}
              onMouseEnter={() => setHover(r.faculty.id)}
              onMouseLeave={() => setHover(null)}
              className={`grid grid-cols-[2rem_7rem_1fr_3rem] items-center gap-2 rounded-md px-1 py-1 text-sm sm:grid-cols-[2.5rem_10rem_1fr_4rem] ${hover === r.faculty.id ? "bg-indigo-50" : ""}`}
            >
              <span className={`font-bold ${r.position <= 3 ? "text-amber-600" : "text-slate-400"}`}>#{r.position}</span>
              <span className="truncate font-medium">{r.faculty.name}</span>
              <div className="h-6 overflow-hidden rounded bg-slate-100">
                <div
                  className="h-full rounded bg-gradient-to-r from-indigo-500 to-violet-500 transition-all duration-500"
                  style={{ width: `${(r[metric] / max) * 100}%` }}
                />
              </div>
              <span className="text-right font-semibold tabular-nums">{r[metric]}</span>
            </li>
          ))}
        </ul>
      </div>

      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-slate-500">
            <tr>
              <th className="px-4 py-2">O&apos;rin</th>
              <th className="px-4 py-2">Fakultet</th>
              <th className="px-4 py-2 text-right">Tasdiqlangan ishlar</th>
              <th className="px-4 py-2 text-right">Umumiy ball</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r: RankRow) => (
              <tr key={r.faculty.id} className={`border-t ${hover === r.faculty.id ? "bg-indigo-50" : ""}`}>
                <td className="px-4 py-2 font-semibold">{r.position}</td>
                <td className="px-4 py-2">{r.faculty.name}</td>
                <td className="px-4 py-2 text-right tabular-nums">{r.approved}</td>
                <td className="px-4 py-2 text-right tabular-nums">{r.score}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
