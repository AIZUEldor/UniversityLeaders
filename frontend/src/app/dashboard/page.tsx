"use client";

import Link from "next/link";
import { ReportCard } from "@/components/ReportCard";
import { ROLE_LABELS, canApprove, computeRanking, type Person, type RankRow, type Report } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function Dashboard() {
  const { people, reports, current } = useCurrent();
  const byId = (id: string): Person | undefined => people.find((p: Person) => p.id === id);

  // Scope: faculty roles see their faculty; university coordinators see everything approved.
  const inScope = (r: Report): boolean => current.facultyId === null || r.facultyId === current.facultyId;
  const completed: Report[] = reports.filter((r: Report) => r.status === "approved" && inScope(r));
  const toReview: number = reports.filter((r: Report) => {
    const a: Person | undefined = byId(r.authorId);
    return r.status === "pending" && a !== undefined && canApprove(current, a);
  }).length;
  const mine: Report[] = reports.filter((r: Report) => r.authorId === current.id);
  const rank: RankRow | undefined = computeRanking(reports).find((row: RankRow) => row.faculty.id === current.facultyId);

  const stats: { label: string; value: string | number }[] = [
    ...(current.role === "faculty_leader" ? [{ label: "Tasdiqlash kutilmoqda", value: toReview }] : []),
    { label: "Mening hisobotlarim", value: mine.length },
    { label: "Kutilayotgan hisobotlarim", value: mine.filter((r: Report) => r.status === "pending").length },
    { label: "Bajarilgan ishlar", value: completed.length },
    { label: "Fakultet o'rni", value: rank ? `#${rank.position}` : "—" },
  ];

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Xush kelibsiz, {current.name}</h1>
        <p className="text-sm text-slate-500">{ROLE_LABELS[current.role]}</p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {stats.map((s) => (
          <div key={s.label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-2xl font-bold text-indigo-700">{s.value}</div>
            <div className="text-xs text-slate-500">{s.label}</div>
          </div>
        ))}
      </div>

      <section>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-semibold">Bajarilgan ishlar (tasdiqlangan)</h2>
          <Link href="/dashboard/reports" className="text-sm text-indigo-600 hover:underline">
            Hisobot yuborish →
          </Link>
        </div>
        <div className="space-y-3">
          {completed.slice(0, 8).map((r: Report) => (
            <ReportCard key={r.id} report={r} author={byId(r.authorId)} />
          ))}
        </div>
      </section>
    </div>
  );
}
