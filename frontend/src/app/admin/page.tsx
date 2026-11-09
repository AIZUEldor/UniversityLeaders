"use client";

import { useState, type FormEvent } from "react";
import { DIRECTIONS, FACULTIES, ROLE_LABELS, type Announcement, type Person, type Role } from "@/lib/data";
import { useStore } from "@/lib/store";

export default function Admin() {
  const { people, announcements, renamePerson, sendAnnouncement } = useStore();
  const [role, setRole] = useState<Role>("faculty_leader");
  const [title, setTitle] = useState<string>("");
  const [body, setBody] = useState<string>("");
  const [sent, setSent] = useState<boolean>(false);

  const leaders: number = people.filter((p: Person) => p.role === "faculty_leader").length;

  const onSend = (e: FormEvent<HTMLFormElement>): void => {
    e.preventDefault();
    if (!title.trim() || !body.trim()) return;
    sendAnnouncement(title.trim(), body.trim());
    setTitle("");
    setBody("");
    setSent(true);
  };

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-bold">Admin paneli</h1>
        <p className="text-sm text-slate-500">Demo rejim — avtorizatsiya yo&apos;q, o&apos;zgarishlar sahifa yangilanganda tiklanadi.</p>
      </div>

      <form onSubmit={onSend} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="font-semibold">E&apos;lon yuborish → {leaders} ta fakultet rahbariga</h2>
        <input value={title} onChange={(e) => { setTitle(e.target.value); setSent(false); }} placeholder="Sarlavha" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <textarea value={body} onChange={(e) => { setBody(e.target.value); setSent(false); }} placeholder="Matn" rows={3} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <div className="flex items-center gap-3">
          <button className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">Yuborish</button>
          {sent && <span className="text-sm text-emerald-700">✓ {leaders} ta rahbar qutisiga yuborildi (Panel → Fakultet rahbari).</span>}
        </div>
        {announcements.length > 0 && (
          <ul className="space-y-1 border-t pt-3 text-sm text-slate-600">
            {announcements.map((a: Announcement) => (
              <li key={a.id}>• {a.title} <span className="text-xs text-slate-400">({a.createdAt})</span></li>
            ))}
          </ul>
        )}
      </form>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="mb-3 font-semibold">Demo rahbar va koordinatorlarni boshqarish</h2>
        <div className="mb-4 flex flex-wrap gap-2">
          {(Object.keys(ROLE_LABELS) as Role[]).map((r: Role) => (
            <button
              key={r}
              onClick={() => setRole(r)}
              className={`rounded-full px-3 py-1 text-sm ${role === r ? "bg-indigo-600 text-white" : "bg-slate-100 text-slate-700"}`}
            >
              {ROLE_LABELS[r]} ({people.filter((p: Person) => p.role === r).length})
            </button>
          ))}
        </div>
        <ul className="grid gap-2 sm:grid-cols-2">
          {people
            .filter((p: Person) => p.role === role)
            .map((p: Person) => (
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
      </section>
    </div>
  );
}
