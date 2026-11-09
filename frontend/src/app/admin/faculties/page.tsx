"use client";

import { FACULTIES, type Faculty, type Person, type Report, type ReportStatus } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function AdminFaculties() {
  const { people, reports, renamePerson } = useCurrent();
  const count = (fid: number, status: ReportStatus): number =>
    reports.filter((r: Report) => r.facultyId === fid && r.status === status).length;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Fakultetlar</h1>
        <p className="text-sm text-slate-500">13 ta fakultet, har birida 7 ta koordinator yo&apos;nalishi. Rahbar ismini shu yerda tahrirlang.</p>
      </div>
      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-slate-500">
            <tr>
              <th className="px-4 py-2">Fakultet</th>
              <th className="px-4 py-2">Fakultet rahbari</th>
              <th className="px-4 py-2 text-right">Koordinatorlar</th>
              <th className="px-4 py-2 text-right">Kutilmoqda</th>
              <th className="px-4 py-2 text-right">Tasdiqlangan</th>
              <th className="px-4 py-2 text-right">Rad etilgan</th>
            </tr>
          </thead>
          <tbody>
            {FACULTIES.map((f: Faculty) => {
              const leader: Person | undefined = people.find((p: Person) => p.role === "faculty_leader" && p.facultyId === f.id);
              const coords: number = people.filter((p: Person) => p.role === "faculty_coordinator" && p.facultyId === f.id).length;
              return (
                <tr key={f.id} className="border-t">
                  <td className="px-4 py-2 font-medium">{f.name}</td>
                  <td className="px-4 py-2">
                    {leader && (
                      <input
                        value={leader.name}
                        onChange={(e) => renamePerson(leader.id, e.target.value)}
                        aria-label={`${f.name} rahbari ismi`}
                        className="w-full min-w-40 rounded-md border border-slate-300 px-2 py-1"
                      />
                    )}
                  </td>
                  <td className="px-4 py-2 text-right tabular-nums">{coords}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{count(f.id, "pending")}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{count(f.id, "approved")}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{count(f.id, "rejected")}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
