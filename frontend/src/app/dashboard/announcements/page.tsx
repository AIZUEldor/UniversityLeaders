"use client";

import { type Announcement } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function DashboardAnnouncements() {
  const { announcements } = useCurrent();
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">📥 Kiruvchi e&apos;lonlar ({announcements.length})</h1>
      {announcements.length === 0 && <p className="text-sm text-slate-500">Hozircha e&apos;lon yo&apos;q.</p>}
      <ul className="space-y-2">
        {announcements.map((a: Announcement) => (
          <li key={a.id} className="rounded-lg border border-slate-200 bg-white p-3">
            <div className="font-medium">{a.title}</div>
            <p className="text-sm text-slate-600">{a.body}</p>
            <div className="mt-1 text-xs text-slate-400">Universitet rahbari · {a.createdAt}</div>
          </li>
        ))}
      </ul>
    </div>
  );
}
