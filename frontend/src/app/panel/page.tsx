"use client";

import Link from "next/link";
import { ReportCard } from "@/components/ReportCard";
import { ROLE_LABELS, canApprove, computeRanking, type Announcement, type Person, type RankRow, type Report, type Role } from "@/lib/data";
import { useStore } from "@/lib/store";

export default function Dashboard() {
  const { people, reports, announcements, current, setCurrentId } = useStore();
  const byId = (id: string): Person | undefined => people.find((p: Person) => p.id === id);

  // Scope: faculty roles see their faculty; university roles see everything.
  const inScope = (r: Report): boolean => current.facultyId === null || r.facultyId === current.facultyId;
  const completed: Report[] = reports.filter((r: Report) => r.status === "approved" && inScope(r));
  const toReview: number = reports.filter((r: Report) => {
    const a: Person | undefined = byId(r.authorId);
    return r.status === "pending" && a !== undefined && canApprove(current, a);
  }).length;
  const mine: Report[] = reports.filter((r: Report) => r.authorId === current.id);
  const rank: RankRow | undefined = computeRanking(reports).find((row: RankRow) => row.faculty.id === current.facultyId);

  const stats: { label: string; value: string | number }[] = [
    { label: "Tasdiqlash kutilmoqda", value: toReview },
    { label: "Mening hisobotlarim", value: mine.length },
    { label: "Bajarilgan ishlar", value: completed.length },
    { label: "Fakultet o'rni", value: rank ? `#${rank.position}` : "—" },
  ];

  return (
    <div className="space-y-6">
      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <div className="text-xs font-semibold uppercase text-amber-700">Demo rol almashtirgich</div>
        <div className="mt-2 flex flex-wrap gap-2">
          {(Object.keys(ROLE_LABELS) as Role[]).map((role: Role) => (
            <button
              key={role}
              onClick={() => setCurrentId(people.find((p: Person) => p.role === role)!.id)}
              className={`rounded-full px-4 py-1.5 text-sm font-medium ${
                current.role === role ? "bg-indigo-600 text-white" : "bg-slate-100 text-slate-700 hover:bg-slate-200"
              }`}
            >
              {ROLE_LABELS[role]}
            </button>
          ))}
        </div>
        <p className="mt-3 text-sm text-slate-500">
          Hozir: <b className="text-slate-800">{current.name}</b> — {ROLE_LABELS[current.role]}
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {stats.map((s) => (
          <div key={s.label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-2xl font-bold text-indigo-700">{s.value}</div>
            <div className="text-xs text-slate-500">{s.label}</div>
          </div>
        ))}
      </div>

      {current.role === "faculty_leader" && (
        <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="mb-3 font-semibold">📥 Kiruvchi e&apos;lonlar ({announcements.length})</h2>
          {announcements.length === 0 && <p className="text-sm text-slate-500">Hozircha e&apos;lon yo&apos;q. Admin sahifasidan yuboring.</p>}
          <ul className="space-y-2">
            {announcements.map((a: Announcement) => (
              <li key={a.id} className="rounded-lg bg-indigo-50 p-3">
                <div className="font-medium">{a.title}</div>
                <p className="text-sm text-slate-600">{a.body}</p>
                <div className="mt-1 text-xs text-slate-400">Admin · {a.createdAt}</div>
              </li>
            ))}
          </ul>
        </section>
      )}

      <section>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-semibold">Bajarilgan ishlar (tasdiqlangan)</h2>
          <Link href="/hisobotlar" className="text-sm text-indigo-600 hover:underline">
            Hisobotlarga o&apos;tish →
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
