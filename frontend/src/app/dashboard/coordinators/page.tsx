"use client";

import { PeopleEditor } from "@/components/ReportPanels";
import { FACULTIES, type Person } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function DashboardCoordinators() {
  const { people, current } = useCurrent();
  const mine: Person[] = people.filter((p: Person) => p.role === "faculty_coordinator" && p.facultyId === current.facultyId);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Mening koordinatorlarim</h1>
        <p className="text-sm text-slate-500">
          {current.facultyId ? FACULTIES[current.facultyId - 1].name : ""} — 7 yo&apos;nalish. Demo: faqat ismlarni tahrirlash.
        </p>
      </div>
      <PeopleEditor people={mine} />
    </div>
  );
}
