"use client";

import { useState, type FormEvent } from "react";
import { type Announcement, type Person } from "@/lib/data";
import { useCurrent } from "@/lib/store";

export default function AdminAnnouncements() {
  const { people, announcements, sendAnnouncement } = useCurrent();
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
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">E&apos;lonlar</h1>
      <form onSubmit={onSend} className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h2 className="font-semibold">E&apos;lon yuborish → {leaders} ta fakultet rahbariga</h2>
        <input value={title} onChange={(e) => { setTitle(e.target.value); setSent(false); }} placeholder="Sarlavha" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <textarea value={body} onChange={(e) => { setBody(e.target.value); setSent(false); }} placeholder="Matn" rows={3} className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" required />
        <div className="flex items-center gap-3">
          <button className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-700">Yuborish</button>
          {sent && <span className="text-sm text-emerald-700">✓ {leaders} ta rahbar qutisiga yuborildi (Kabinet → E&apos;lonlar).</span>}
        </div>
      </form>
      <section>
        <h2 className="mb-3 font-semibold">Yuborilganlar ({announcements.length})</h2>
        <ul className="space-y-2">
          {announcements.map((a: Announcement) => (
            <li key={a.id} className="rounded-lg border border-slate-200 bg-white p-3">
              <div className="font-medium">{a.title}</div>
              <p className="text-sm text-slate-600">{a.body}</p>
              <div className="mt-1 text-xs text-slate-400">{a.createdAt}</div>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
