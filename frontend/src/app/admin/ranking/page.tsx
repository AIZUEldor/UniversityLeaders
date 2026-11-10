"use client";

import { RankingBoard } from "@/components/RankingBoard";
import { T } from "@/lib/i18n";

export default function AdminRanking() {
  return <div className="space-y-6">
    <div>
      <h1 className="text-2xl font-bold"><T>Reyting nazorati</T></h1>
      <p className="text-sm text-slate-500"><T>{"13 fakultet. Reytingga faqat siz tasdiqlagan fakultet hisobotlari va ularning ballari kiradi. Kutilayotgan hisobotlar «Hisobotlarni tasdiqlash» bo'limida."}</T></p>
    </div>
    <RankingBoard />
  </div>;
}
