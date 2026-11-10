import { RankingBoard } from "@/components/RankingBoard";
import { PageHeader } from "@/components/public/PageHeader";

export default function Ranking() {
  return <div className="space-y-6">
    <PageHeader title="Fakultetlar reytingi" subtitle="13 fakultet. Faqat universitet yetakchisi tasdiqlagan hisobotlar va ularning ballari (0–10) hisoblanadi." />
    <RankingBoard />
  </div>;
}
