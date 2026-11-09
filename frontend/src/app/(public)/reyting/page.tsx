import { RankingBoard } from "@/components/RankingBoard";

export default function Ranking() {
  return <div className="space-y-6">
    <div><h1 className="text-2xl font-bold">Fakultetlar reytingi</h1><p className="text-sm text-slate-500">Faqat fakultetlarga tegishli tasdiqlangan hisobotlar hisoblanadi.</p></div>
    <RankingBoard />
  </div>;
}
