"use client";

import { computeRanking, type RankRow, type Report } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function AdminRanking() {
  const { reports } = useCurrent();
  const rows: RankRow[] = computeRanking(reports);
  const pending = (fid: number): number => reports.filter((r: Report) => r.facultyId === fid && r.status === "pending").length;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Reyting nazorati</h1>
        <p className="text-sm text-slate-500">Reytingga faqat tasdiqlangan hisobotlar kiradi; kutilayotganlar faqat shu yerda ko&apos;rinadi.</p>
      </div>
      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-slate-500">
            <tr>
              <th className="px-4 py-2">O&apos;rin</th>
              <th className="px-4 py-2">Fakultet</th>
              <th className="px-4 py-2 text-right">Tasdiqlangan</th>
              <th className="px-4 py-2 text-right">Ball</th>
              <th className="px-4 py-2 text-right">Kutilmoqda (hisobga olinmaydi)</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r: RankRow) => (
              <tr key={r.faculty.id} className="border-t">
                <td className="px-4 py-2 font-semibold">{r.position}</td>
                <td className="px-4 py-2">{r.faculty.name}</td>
                <td className="px-4 py-2 text-right tabular-nums">{r.approved}</td>
                <td className="px-4 py-2 text-right tabular-nums">{r.score}</td>
                <td className="px-4 py-2 text-right tabular-nums text-amber-700">{pending(r.faculty.id)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
