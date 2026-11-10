"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api";
import { facultiesApi, type FacultyDto } from "@/lib/adminApi";
import { ManagementMessage, ManagementPanel, inputClass, primaryClass, secondaryClass } from "@/components/admin/ManagementUi";

export default function AdminFaculties() {
  const [faculties, setFaculties] = useState<FacultyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [editing, setEditing] = useState<FacultyDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try { setFaculties(await facultiesApi.list()); setError(null); }
    catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const save = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault(); setBusy(true); setError(null);
    try {
      if (editing) await facultiesApi.update(editing.id, name.trim(), description.trim(), editing.version);
      else await facultiesApi.create(name.trim(), description.trim());
      setEditing(null); setName(""); setDescription(""); await load();
    } catch (failure: unknown) {
      setError(failure);
      if (failure instanceof ApiError && failure.status === 409) { setEditing(null); setName(""); setDescription(""); await load(); setError(failure); }
    } finally { setBusy(false); }
  };

  return <div className="space-y-6">
    <div><h1 className="text-2xl font-bold">Fakultetlar</h1><p className="text-sm text-slate-500">13 ta tasdiqlangan fakultet. Nomi va qo&apos;shimcha tavsifi ommaviy saytdagi kartada ko&apos;rinadi.</p></div>
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    <ManagementPanel>
      <h2 className="mb-4 font-semibold">{editing ? "Fakultetni tahrirlash" : "Yangi fakultet"}</h2>
      <form onSubmit={(event) => void save(event)} className="flex flex-wrap items-end gap-3">
        <label className="min-w-0 flex-1 basis-full text-sm sm:min-w-64">Nomi<input required maxLength={200} value={name} onChange={(event) => setName(event.target.value)} className={`${inputClass} mt-1`} /></label>
        <label className="min-w-0 flex-1 basis-full text-sm sm:min-w-64">Qo&apos;shimcha tavsif (ixtiyoriy)<input maxLength={500} value={description} onChange={(event) => setDescription(event.target.value)} className={`${inputClass} mt-1`} /></label>
        <button disabled={busy || loading || (!editing && faculties.length >= 13)} className={primaryClass}>{busy ? "Saqlanmoqda..." : editing ? "Saqlash" : "Yaratish"}</button>
        {editing && <button type="button" onClick={() => { setEditing(null); setName(""); setDescription(""); }} className={secondaryClass}>Bekor qilish</button>}
      </form>
    </ManagementPanel>
    <ManagementPanel>
      <h2 className="mb-4 font-semibold">Ro&apos;yxat ({faculties.length}/13)</h2>
      {loading ? <p role="status" className="text-sm text-slate-500">Yuklanmoqda...</p> : faculties.length === 0 ? <p className="text-sm text-slate-500">Hozircha fakultetlar yo&apos;q.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50 text-slate-500"><tr><th className="px-3 py-2">Fakultet</th><th className="px-3 py-2">Amallar</th></tr></thead><tbody>{faculties.map((faculty) => <tr key={faculty.id} className="border-t border-slate-200"><td className="px-3 py-2"><div className="font-medium">{faculty.name}</div>{faculty.description && <div className="text-xs text-slate-500">{faculty.description}</div>}</td><td className="px-3 py-2"><button type="button" disabled={busy} onClick={() => { setEditing(faculty); setName(faculty.name); setDescription(faculty.description ?? ""); window.scrollTo({ top: 0, behavior: "smooth" }); }} className="text-indigo-700 hover:underline">Tahrirlash</button></td></tr>)}</tbody></table></div>}
    </ManagementPanel>
  </div>;
}
