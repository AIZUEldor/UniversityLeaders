"use client";

import { DIRECTIONS, FACULTIES, type Person, type Report, type ReportStatus } from "@/lib/data";

const STATUS: Record<ReportStatus, { label: string; cls: string }> = {
  pending: { label: "Kutilmoqda", cls: "bg-amber-100 text-amber-800" },
  approved: { label: "Tasdiqlangan", cls: "bg-emerald-100 text-emerald-800" },
  rejected: { label: "Rad etilgan", cls: "bg-rose-100 text-rose-800" },
};

export function ReportCard({ report, author, children }: { report: Report; author?: Person; children?: React.ReactNode }) {
  const s = STATUS[report.status];
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <div className="font-medium">{report.title}</div>
          <div className="text-xs text-slate-500">
            {author?.name} · {report.facultyId ? FACULTIES[report.facultyId - 1].name : "Universitet"}
            {report.directionIdx !== null && ` · ${DIRECTIONS[report.directionIdx]}`} · {report.createdAt}
          </div>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold text-slate-600">{report.score} ball</span>
          <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${s.cls}`}>{s.label}</span>
        </div>
      </div>
      {report.comment && <p className="mt-2 text-sm text-slate-600">Izoh: {report.comment}</p>}
      {children}
    </div>
  );
}
