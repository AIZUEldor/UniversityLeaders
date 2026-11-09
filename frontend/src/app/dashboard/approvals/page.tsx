import { ReportsCollection } from "@/components/reports/ReportsCollection";

export default function DashboardApprovals() {
  return <div className="space-y-6"><div><h1 className="text-2xl font-bold">Koordinator hisobotlarini tasdiqlash</h1><p className="text-sm text-slate-500">Faqat o&apos;z fakultetingiz koordinatorlari hisobotlari.</p></div><ReportsCollection source="review" detailBase="/dashboard/approvals" /></div>;
}