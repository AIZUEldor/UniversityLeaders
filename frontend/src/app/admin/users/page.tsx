"use client";

import { useState } from "react";
import { PeopleEditor } from "@/components/ReportPanels";
import { ROLE_LABELS, type Person, type Role } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function AdminUsers() {
  const { people } = useCurrent();
  const [role, setRole] = useState<Role>("faculty_leader");

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Foydalanuvchilar va koordinatorlar</h1>
        <p className="text-sm text-slate-500">Demo: faqat ismlarni tahrirlash. O&apos;zgarishlar sahifa yangilanganda tiklanadi.</p>
      </div>
      <div className="flex flex-wrap gap-2">
        {(Object.keys(ROLE_LABELS) as Role[]).map((r: Role) => (
          <button
            key={r}
            onClick={() => setRole(r)}
            className={`rounded-full px-3 py-1 text-sm ${role === r ? "bg-indigo-600 text-white" : "bg-white text-slate-700 ring-1 ring-slate-200"}`}
          >
            {ROLE_LABELS[r]} ({people.filter((p: Person) => p.role === r).length})
          </button>
        ))}
      </div>
      <PeopleEditor people={people.filter((p: Person) => p.role === role)} />
    </div>
  );
}
