"use client";

import { useState, type FormEvent } from "react";
import { ReportCard } from "@/components/ReportCard";
import { DIRECTIONS, FACULTIES, ROLE_LABELS, approverRoleFor, canApprove, type Person, type Report, type Role } from "@/lib/data";
import { useCurrent } from "@/lib/store";

/** Submit form + own reports. Only for roles that have an approver. */
export function SubmitReports() {
  const { reports, current, submitReport } = useCurrent();
  const [title, setTitle] = useState<string>("");
  const [score, setScore] = useState<number>(5);

  const approverRole: Role | null = approverRoleFor(current);
  const mine: Report[] = reports.filter((r: Report) => r.authorId === current.id);

  const onSubmit = (e: FormEvent<HTMLFormElement>): void => {
    e.preventDefault();
    if (!title.trim() || !approverRole) return;
    submitReport({ title: title.trim(), authorId: current.id, facultyId: current.facultyId, directionIdx: current.directionIdx, score });
    setTitle("");
  };

  if (!approverRole) return <p className="rounded-xl bg-slate-100 p-4 text-sm text-slate-600">Universitet rahbari hisobot yubormaydi — faqat tasdiqlaydi.</p>;

  return (
    <div className="space-y-8">
      <form onSubmit={onSubmit} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="font-semibold">Yangi hisobot yuborish</h2>
        <p className="text-xs text-slate-500">Tasdiqlovchi: {ROLE_LABELS[approverRole]}</p>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Bajarilgan ish nomi"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          required
        />
        <div className="flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-2 text-sm">
            Ball:
            <input
              type="number"
              min={1}
              max={10}
              value={score}
              onChange={(e) => setScore(Math.min(10, Math.max(1, Number(e.target.value))))}
              className="w-20 rounded-md border border-slate-300 px-2 py-1"
            />
          </label>
          <div className="rounded-md border border-dashed border-slate-300 px-3 py-1.5 text-xs text-slate-400">
            📎 Fayl yuklash — demo (hali ulanmagan)
          </div>
          <button className="ml-auto rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">
            Yuborish
          </button>
        </div>
      </form>

      <section>
        <h2 className="mb-3 font-semibold">Mening hisobotlarim ({mine.length})</h2>
        <div className="space-y-3">
          {mine.map((r: Report) => (
            <ReportCard key={r.id} report={r} author={current} />
          ))}
        </div>
      </section>
    </div>
  );
}

/** Pending reports the current user may approve (canApprove enforces role + faculty scope). */
export function ApprovalQueue() {
  const { people, reports, current, reviewReport } = useCurrent();
  const [comments, setComments] = useState<Record<string, string>>({});

  const byId = (id: string): Person | undefined => people.find((p: Person) => p.id === id);
  const queue: Report[] = reports.filter((r: Report) => {
    const author: Person | undefined = byId(r.authorId);
    return r.status === "pending" && author !== undefined && canApprove(current, author);
  });

  return (
    <section>
      <h2 className="mb-3 font-semibold">Tasdiqlash navbati ({queue.length})</h2>
      {queue.length === 0 && <p className="text-sm text-slate-500">Sizda ko&apos;rib chiqiladigan hisobot yo&apos;q.</p>}
      <div className="space-y-3">
        {queue.map((r: Report) => (
          <ReportCard key={r.id} report={r} author={byId(r.authorId)}>
            <div className="mt-3 flex flex-wrap gap-2">
              <input
                value={comments[r.id] ?? ""}
                onChange={(e) => setComments({ ...comments, [r.id]: e.target.value })}
                placeholder="Izoh (ixtiyoriy)"
                className="min-w-0 flex-1 rounded-md border border-slate-300 px-2 py-1 text-sm"
              />
              <button onClick={() => reviewReport(r.id, "approved", comments[r.id] ?? "")} className="rounded-md bg-emerald-600 px-3 py-1 text-sm text-white hover:bg-emerald-700">
                Tasdiqlash
              </button>
              <button onClick={() => reviewReport(r.id, "rejected", comments[r.id] ?? "")} className="rounded-md bg-rose-600 px-3 py-1 text-sm text-white hover:bg-rose-700">
                Rad etish
              </button>
            </div>
          </ReportCard>
        ))}
      </div>
    </section>
  );
}

/** Editable names of people (demo "management": rename only). */
export function PeopleEditor({ people }: { people: Person[] }) {
  const { renamePerson } = useCurrent();
  return (
    <ul className="grid gap-2 sm:grid-cols-2">
      {people.map((p: Person) => (
        <li key={p.id} className="rounded-lg bg-slate-50 p-3">
          <div className="mb-1 text-xs text-slate-500">
            {p.facultyId ? FACULTIES[p.facultyId - 1].name : "Universitet"}
            {p.directionIdx !== null && ` · ${DIRECTIONS[p.directionIdx]}`}
          </div>
          <input
            value={p.name}
            onChange={(e) => renamePerson(p.id, e.target.value)}
            aria-label={`${p.id} ismi`}
            className="w-full rounded-md border border-slate-300 bg-white px-2 py-1 text-sm"
          />
        </li>
      ))}
    </ul>
  );
}
