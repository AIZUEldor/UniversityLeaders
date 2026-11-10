"use client";

import { RankingBoard } from "@/components/RankingBoard";

export default function DashboardRanking() {
  return <div className="space-y-6">
    <div>
      <h1 className="text-2xl font-bold">Fakultetlar reytingi</h1>
      <p className="text-sm text-slate-500">13 fakultet. Faqat universitet yetakchisi tasdiqlagan hisobotlar va ularning ballari (0–10) hisoblanadi.</p>
    </div>
    <RankingBoard />
  </div>;
}
