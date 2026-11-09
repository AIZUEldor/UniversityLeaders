"use client";

import { useState } from "react";
import { DIRECTIONS, FACULTIES, type Faculty, type Person } from "@/lib/data";
import { useStore } from "@/lib/store";

export default function Home() {
  const { people } = useStore();
  const [selected, setSelected] = useState<Faculty | null>(null);

  const leaderOf = (fid: number): Person | undefined =>
    people.find((p: Person) => p.role === "faculty_leader" && p.facultyId === fid);
  const coordinators: Person[] = selected
    ? people.filter((p: Person) => p.role === "faculty_coordinator" && p.facultyId === selected.id)
    : [];
  const uniCoordinators: Person[] = people.filter((p: Person) => p.role === "university_coordinator");
  const uniLeader: Person | undefined = people.find((p: Person) => p.role === "university_leader");

  return (
    <div className="space-y-8">
      <section className="rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 p-6 text-white shadow">
        <h1 className="text-2xl font-bold md:text-3xl">Universitet yetakchilari platformasi</h1>
        <p className="mt-2 text-indigo-100">
          Universitet rahbari: <b>{uniLeader?.name}</b> · 13 fakultet · 7 universitet koordinatori · 91 fakultet koordinatori
        </p>
      </section>

      <section>
        <h2 className="mb-3 text-xl font-semibold">Fakultetlar</h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {FACULTIES.map((f: Faculty) => (
            <button
              key={f.id}
              onClick={() => setSelected(f)}
              className="rounded-xl border border-slate-200 bg-white p-5 text-left shadow-sm transition hover:-translate-y-0.5 hover:border-indigo-400 hover:shadow-md"
            >
              <div className="text-xs font-semibold uppercase tracking-wide text-indigo-600">Fakultet #{f.id}</div>
              <div className="mt-1 text-lg font-semibold">{f.name}</div>
              <div className="mt-2 text-sm text-slate-500">Rahbar: {leaderOf(f.id)?.name}</div>
              <div className="mt-3 text-xs text-indigo-600">7 koordinator lavozimini ko&apos;rish →</div>
            </button>
          ))}
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-xl font-semibold">Universitet koordinatorlari (7 o&apos;rin)</h2>
        <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {uniCoordinators.map((p: Person) => (
            <li key={p.id} className="rounded-lg border border-slate-200 bg-white p-4 text-sm shadow-sm">
              <div className="font-medium text-indigo-700">{DIRECTIONS[p.directionIdx ?? 0]}</div>
              <div className="text-slate-600">{p.name}</div>
            </li>
          ))}
        </ul>
      </section>

      {selected && (
        <div
          className="fixed inset-0 z-30 flex items-end justify-center bg-black/40 p-4 sm:items-center"
          onClick={() => setSelected(null)}
        >
          <div
            role="dialog"
            aria-modal="true"
            aria-label={selected.name}
            className="max-h-[85vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <h3 className="text-xl font-semibold">{selected.name}</h3>
                <p className="text-sm text-slate-500">Rahbar: {leaderOf(selected.id)?.name}</p>
              </div>
              <button onClick={() => setSelected(null)} className="rounded-md px-2 text-2xl leading-none text-slate-400 hover:text-slate-700" aria-label="Yopish">
                ×
              </button>
            </div>
            <ol className="mt-4 space-y-2">
              {coordinators.map((p: Person, i: number) => (
                <li key={p.id} className="flex items-center gap-3 rounded-lg bg-slate-50 p-3">
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-sm font-semibold text-indigo-700">
                    {i + 1}
                  </span>
                  <div>
                    <div className="text-sm font-medium">{DIRECTIONS[p.directionIdx ?? 0]}</div>
                    <div className="text-xs text-slate-500">{p.name}</div>
                  </div>
                </li>
              ))}
            </ol>
          </div>
        </div>
      )}
    </div>
  );
}
