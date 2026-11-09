import { CompletedWorkPanel } from "@/components/public/CompletedWorkPanel";

export default function CompletedWork() {
  return <div className="space-y-6"><div><h1 className="text-2xl font-bold">Bajarilgan ishlar</h1><p className="text-sm text-slate-500">Faqat tasdiqlangan ishlar ko&apos;rsatiladi.</p></div><CompletedWorkPanel /></div>;
}