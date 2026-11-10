import { MediaWorkspace } from "@/components/reports/MediaWorkspace";
import { T } from "@/lib/i18n";

export default function Uploads() {
  return <div className="space-y-6"><div><h1 className="text-2xl font-bold"><T>Foto / hujjat yuklash</T></h1><p className="text-sm text-slate-500"><T>Qoralama yoki rad etilgan hisobotga JPG, PNG, PDF va DOCX fayllarni biriktiring.</T></p></div><MediaWorkspace /></div>;
}