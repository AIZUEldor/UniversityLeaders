import { ReportsCollection } from "@/components/reports/ReportsCollection";
import { T } from "@/lib/i18n";

export default function AdminApprovals() {
  return <div className="space-y-6"><div><h1 className="text-2xl font-bold"><T>Hisobotlarni tasdiqlash</T></h1><p className="text-sm text-slate-500"><T>Fakultet yetakchilaridan kelgan hisobotlar: tasdiqlash (0–10 ball) yoki rad etish.</T></p></div><ReportsCollection source="review" detailBase="/admin/approvals" /></div>;
}