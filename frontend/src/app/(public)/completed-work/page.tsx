import { PageHeader } from "@/components/public/PageHeader";
import { CompletedWorkPanel } from "@/components/public/CompletedWorkPanel";

export default function CompletedWork() {
  return <div className="space-y-6">
    <PageHeader title="Bajarilgan ishlar" subtitle="Fakultet yetakchilari yuborgan va universitet yetakchisi tasdiqlagan ishlar. Faqat tasdiqlangan ishlar ko'rsatiladi." />
    <CompletedWorkPanel />
  </div>;
}
