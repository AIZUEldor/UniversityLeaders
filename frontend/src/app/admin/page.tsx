"use client";

import { canApprove, type Person, type Report } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function AdminHome() {
  const { people, reports, announcements, current } = useCurrent();
  const pending: number = reports.filter((r: Report) => {
    const a: Person | undefined = people.find((p: Person) => p.id === r.authorId);
    return r.status === "pending" && a !== undefined && canApprove(current, a);
  }).length;

  const stats: { label: string; value: number }[] = [
    { label: "Fakultetlar", value: 13 },
    { label: "Foydalanuvchilar", value: people.length },
    { label: "Mening tasdiqlashim kutilmoqda", value: pending },
    { label: "Barcha kutilayotgan hisobotlar", value: reports.filter((r: Report) => r.status === "pending").length },
    { label: "Tasdiqlangan hisobotlar", value: reports.filter((r: Report) => r.status === "approved").length },
    { label: "Yuborilgan e'lonlar", value: announcements.length },
  ];

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Umumiy ko&apos;rinish</h1>
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        {stats.map((s) => (
          <div key={s.label} className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div className="text-2xl font-bold text-indigo-700">{s.value}</div>
            <div className="text-xs text-slate-500">{s.label}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
