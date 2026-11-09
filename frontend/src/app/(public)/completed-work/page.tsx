"use client";

import { ReportCard } from "@/components/ReportCard";
import { type Person, type Report } from "@/lib/data";
import { useStore } from "@/lib/store";

export default function CompletedWork() {
  const { people, reports } = useStore();
  // Public: approved only, and the internal reviewer comment is stripped.
  const approved: Report[] = reports.filter((r: Report) => r.status === "approved").map((r: Report) => ({ ...r, comment: "" }));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Bajarilgan ishlar</h1>
        <p className="text-sm text-slate-500">Faqat tasdiqlangan ishlar ko&apos;rsatiladi (demo ma&apos;lumot). Jami: {approved.length}</p>
      </div>
      <div className="space-y-3">
        {approved.map((r: Report) => (
          <ReportCard key={r.id} report={r} author={people.find((p: Person) => p.id === r.authorId)} />
        ))}
      </div>
    </div>
  );
}
