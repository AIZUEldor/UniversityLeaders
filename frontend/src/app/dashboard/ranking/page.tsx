"use client";

import { RankingBoard } from "@/components/RankingBoard";
import { T } from "@/lib/i18n";

export default function DashboardRanking() {
  return <div className="space-y-6">
    <div>
      <h1 className="text-2xl font-bold"><T>Fakultetlar reytingi</T></h1>
      <p className="text-sm text-slate-500"><T>13 fakultet. Faqat universitet yetakchisi tasdiqlagan hisobotlar va ularning ballari (0–10) hisoblanadi.</T></p>
    </div>
    <RankingBoard />
  </div>;
}
