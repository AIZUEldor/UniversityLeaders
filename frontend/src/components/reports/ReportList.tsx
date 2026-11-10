"use client";

import Link from "next/link";
import { DIRECTIONS } from "@/lib/data";
import { useT } from "@/lib/i18n";
import type { ReportStatus, ReportSummary } from "@/lib/reportsApi";

const status: Record<ReportStatus, { label: string; className: string }> = {
  Draft: { label: "Qoralama", className: "bg-slate-100 text-slate-700" },
  Submitted: { label: "Ko'rib chiqilmoqda", className: "bg-amber-100 text-amber-800" },
  Approved: { label: "Tasdiqlangan", className: "bg-emerald-100 text-emerald-800" },
  Rejected: { label: "Rad etilgan", className: "bg-rose-100 text-rose-800" },
};

export const STATUS_LABELS: Record<ReportStatus, string> = {
  Draft: status.Draft.label, Submitted: status.Submitted.label, Approved: status.Approved.label, Rejected: status.Rejected.label,
};

export function ReportStatusBadge({ value }: { value: ReportStatus }) {
  const t = useT();
  const item = status[value];
  return <span className={`rounded-full px-2 py-1 text-xs font-medium ${item.className}`}>{t(item.label)}</span>;
}

export function ReportList({ reports, detailBase, showAuthor = false }: { reports: ReportSummary[]; detailBase: string; showAuthor?: boolean }) {
  const t = useT();
  return <ul className="space-y-3">{reports.map((report) => <li key={report.id}>
    <Link href={`${detailBase}/${report.id}`} className="block rounded-2xl border border-slate-200 bg-white p-4 shadow-sm hover:border-indigo-300 focus:outline-none focus:ring-2 focus:ring-indigo-500">
      <div className="flex flex-wrap items-start justify-between gap-2"><h2 className="font-semibold text-slate-900">{report.title}</h2><ReportStatusBadge value={report.status} /></div>
      <p className="mt-2 text-sm text-slate-600">
        {showAuthor && <>{report.authorName}{!report.authorIsActive && ` (${t("nofaol")})`} · </>}
        {report.facultyId !== null ? t("Fakultet #{id}", { id: report.facultyId }) : t("Universitet")}
        {report.directionId !== null && ` · ${t(DIRECTIONS[report.directionId - 1] ?? "Yo'nalish #{id}", { id: report.directionId })}`}
      </p>
      <p className="mt-1 text-xs text-slate-500">{t("Yangilangan")}: {report.updatedAt.slice(0, 10)}{report.score !== null && ` · ${report.score} ${t("ball")}`}</p>
    </Link>
  </li>)}</ul>;
}
