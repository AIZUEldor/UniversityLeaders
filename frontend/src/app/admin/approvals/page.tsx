import { ReportsCollection } from "@/components/reports/ReportsCollection";

export default function AdminApprovals() {
  return <div className="space-y-6"><div><h1 className="text-2xl font-bold">Hisobotlarni tasdiqlash</h1><p className="text-sm text-slate-500">Fakultet rahbarlari va universitet koordinatorlari hisobotlari.</p></div><ReportsCollection source="review" detailBase="/admin/approvals" /></div>;
}