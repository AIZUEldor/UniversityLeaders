"use client";

import { ApprovalQueue } from "@/components/ReportPanels";

export default function DashboardApprovals() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Koordinator hisobotlarini tasdiqlash</h1>
        <p className="text-sm text-slate-500">Faqat o&apos;z fakultetingiz koordinatorlari hisobotlari.</p>
      </div>
      <ApprovalQueue />
    </div>
  );
}
