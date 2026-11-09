import { MediaWorkspace } from "@/components/reports/MediaWorkspace";

export default function Uploads() {
  return <div className="space-y-6"><div><h1 className="text-2xl font-bold">Foto / hujjat yuklash</h1><p className="text-sm text-slate-500">Qoralama yoki rad etilgan hisobotga JPG, PNG, PDF va DOCX fayllarni biriktiring.</p></div><MediaWorkspace /></div>;
}