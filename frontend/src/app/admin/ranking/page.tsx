"use client";

import { RankingBoard } from "@/components/RankingBoard";

export default function AdminRanking() {
  return <div className="space-y-6">
    <div>
      <h1 className="text-2xl font-bold">Reyting nazorati</h1>
      <p className="text-sm text-slate-500">13 fakultet. Reytingga faqat siz tasdiqlagan fakultet hisobotlari va ularning ballari kiradi. Kutilayotgan hisobotlar &quot;Hisobotlarni tasdiqlash&quot; bo&apos;limida.</p>
    </div>
    <RankingBoard />
  </div>;
}
