"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api";
import { facultiesApi, facultyCoordinatorsApi, managedUsersApi, type FacultyDto, type ManagedKind, type ManagedUserDto } from "@/lib/adminApi";
import { DIRECTIONS } from "@/lib/data";
import { ManagementMessage, ManagementPanel, TemporaryPassword, inputClass, primaryClass, secondaryClass } from "./ManagementUi";

type ManagementKind = ManagedKind | "faculty-coordinators";
interface Props { kind: ManagementKind; title: string; }
interface Editor { id: string | null; userName: string; fullName: string; seatId: string; version: number | null; }
const emptyEditor: Editor = { id: null, userName: "", fullName: "", seatId: "", version: null };

export function ManagedUsersPage({ kind, title }: Props) {
  const isFacultyCoordinators = kind === "faculty-coordinators";
  const [users, setUsers] = useState<ManagedUserDto[]>([]);
  const [faculties, setFaculties] = useState<FacultyDto[]>([]);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [editor, setEditor] = useState<Editor>(emptyEditor);
  const [password, setPassword] = useState<{ userName: string; value: string } | null>(null);
  const api = isFacultyCoordinators ? facultyCoordinatorsApi : managedUsersApi(kind);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, seats] = await Promise.all([
        kind === "faculty-coordinators" ? facultyCoordinatorsApi.list(true) : managedUsersApi(kind).list(includeInactive),
        kind === "faculty-leaders" ? facultiesApi.list() : Promise.resolve([] as FacultyDto[]),
      ]);
      setUsers(list);
      setFaculties(seats);
      setError(null);
    } catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, [kind, includeInactive]);

  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const seatLabel = (id: number | null): string => {
    if (id === null) return "—";
    return kind === "faculty-leaders" ? faculties.find((f) => f.id === id)?.name ?? `#${id}` : DIRECTIONS[id - 1] ?? `#${id}`;
  };

  const active: ManagedUserDto[] = users.filter((user) => user.isActive);
  const inactive: ManagedUserDto[] = users.filter((user) => !user.isActive);
  const occupiedByOther = (directionId: number): boolean => isFacultyCoordinators && active.some(
    (user) => user.directionId === directionId && user.id !== editor.id,
  );

  const startEdit = (user: ManagedUserDto): void => {
    setEditor({
      id: user.id,
      userName: user.userName,
      fullName: user.fullName,
      seatId: String(kind === "faculty-leaders" ? user.facultyId ?? "" : user.directionId ?? ""),
      version: user.version,
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const save = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    const seatId = Number(editor.seatId);
    if (!Number.isInteger(seatId) || seatId < 1) return;
    if (occupiedByOther(seatId)) { setError(new ApiError(409, "coordinator_slot_taken", "Direction occupied")); return; }
    setBusy(true); setError(null);
    try {
      if (editor.id === null) {
        const result = await api.create(editor.userName.trim(), editor.fullName.trim(), seatId);
        setEditor(emptyEditor);
        setPassword({ userName: result.user.userName, value: result.temporaryPassword });
      } else if (editor.version !== null) {
        await api.update(editor.id, editor.fullName.trim(), seatId, editor.version);
        setEditor(emptyEditor);
      }
      await load();
    } catch (failure: unknown) {
      setError(failure);
      if (failure instanceof ApiError && failure.status === 409) { setEditor(emptyEditor); await load(); setError(failure); }
    } finally { setBusy(false); }
  };

  // Shared by deactivation and password reset: a 409 (stale version, already inactive) reloads the list and keeps the message.
  const runUserAction = async (action: () => Promise<void>): Promise<void> => {
    setBusy(true); setError(null);
    try { await action(); await load(); }
    catch (failure: unknown) {
      setError(failure);
      if (failure instanceof ApiError && failure.status === 409) { setEditor(emptyEditor); await load(); setError(failure); }
    } finally { setBusy(false); }
  };

  const deactivate = (user: ManagedUserDto): Promise<void> => {
    if (!window.confirm(`${user.fullName} hisobini nofaol qilishni tasdiqlaysizmi?`)) return Promise.resolve();
    return runUserAction(async () => { await api.deactivate(user.id, user.version); });
  };

  const resetPassword = (user: ManagedUserDto): Promise<void> => {
    if (!window.confirm(`${user.fullName} uchun yangi vaqtinchalik parol yaratilsinmi? Joriy parol va barcha ochiq sessiyalar bekor qilinadi.`)) return Promise.resolve();
    return runUserAction(async () => {
      const result = await api.resetPassword(user.id, user.version);
      setPassword({ userName: result.user.userName, value: result.temporaryPassword });
    });
  };

  const actions = (user: ManagedUserDto) => user.isActive && <div className="flex flex-wrap gap-3">
    <button type="button" disabled={busy} onClick={() => startEdit(user)} className="text-indigo-700 hover:underline">Tahrirlash</button>
    <button type="button" disabled={busy} onClick={() => void resetPassword(user)} className="text-amber-700 hover:underline">Parolni tiklash</button>
    <button type="button" disabled={busy} onClick={() => void deactivate(user)} className="text-red-700 hover:underline">Nofaol qilish</button>
  </div>;

  return <div className="space-y-6">
    <div><h1 className="text-2xl font-bold">{title}</h1><p className="text-sm text-slate-500">{isFacultyCoordinators ? "Har bir yo'nalishda bittadan faol koordinator. Fakultet hisobingiz orqali aniqlanadi." : "Hisoblarni yaratish, tahrirlash, parolni tiklash va nofaol qilish"}</p></div>
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}
    <ManagementPanel>
      <h2 className="mb-4 font-semibold">{editor.id ? "Hisobni tahrirlash" : "Yangi hisob"}</h2>
      <form onSubmit={(event) => void save(event)} className="grid gap-3 md:grid-cols-2">
        {editor.id === null && <label className="text-sm">Login<input required minLength={3} maxLength={64} autoComplete="off" value={editor.userName} onChange={(event) => setEditor({ ...editor, userName: event.target.value })} className={`${inputClass} mt-1`} /></label>}
        <label className="text-sm">To&apos;liq ism<input required maxLength={200} value={editor.fullName} onChange={(event) => setEditor({ ...editor, fullName: event.target.value })} className={`${inputClass} mt-1`} /></label>
        <label className="text-sm">{kind === "faculty-leaders" ? "Fakultet" : "Yo'nalish"}
          <select required value={editor.seatId} onChange={(event) => setEditor({ ...editor, seatId: event.target.value })} className={`${inputClass} mt-1`}>
            <option value="">Tanlang</option>
            {(kind === "faculty-leaders" ? faculties.map((faculty) => ({ id: faculty.id, name: faculty.name })) : DIRECTIONS.map((name, index) => ({ id: index + 1, name }))).map((seat) => <option key={seat.id} value={seat.id} disabled={occupiedByOther(seat.id)}>{seat.name}{occupiedByOther(seat.id) ? " (band)" : ""}</option>)}
          </select>
        </label>
        <div className="flex items-end gap-2"><button disabled={busy || loading || occupiedByOther(Number(editor.seatId))} className={primaryClass}>{busy ? "Saqlanmoqda..." : editor.id ? "Saqlash" : "Yaratish"}</button>{editor.id && <button type="button" onClick={() => setEditor(emptyEditor)} className={secondaryClass}>Bekor qilish</button>}</div>
      </form>
    </ManagementPanel>
    {isFacultyCoordinators ? <>
      <ManagementPanel>
        <h2 className="mb-1 font-semibold">Yo&apos;nalishlar ({active.length}/7 band)</h2>
        {loading ? <p role="status" className="text-sm text-slate-500">Yuklanmoqda...</p> : error !== null && users.length === 0 ? null : <>
          {active.length === 0 && <p className="mb-3 text-sm text-slate-500">Hozircha faol koordinator yo&apos;q.</p>}
          <ol className="space-y-3">{DIRECTIONS.map((direction, index) => {
            const directionId = index + 1;
            const user = active.find((item) => item.directionId === directionId);
            return <li key={directionId} className="rounded-xl border border-slate-200 p-4">
              <h3 className="text-sm font-medium">{directionId}. {direction}</h3>
              {user ? <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-sm"><p><strong>{user.fullName}</strong> · {user.userName} · <span className="text-green-700">Faol</span></p>{actions(user)}</div>
                : <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-sm"><span className="text-slate-500">Bo&apos;sh o&apos;rin</span><button type="button" disabled={busy} onClick={() => { setEditor({ ...emptyEditor, seatId: String(directionId) }); window.scrollTo({ top: 0, behavior: "smooth" }); }} className="text-indigo-700 hover:underline disabled:opacity-50">Koordinator qo&apos;shish</button></div>}
            </li>;
          })}</ol>
        </>}
      </ManagementPanel>
      <ManagementPanel>
        <h2 className="mb-3 font-semibold">Nofaol koordinatorlar ({inactive.length})</h2>
        {loading ? <p role="status" className="text-sm text-slate-500">Yuklanmoqda...</p> : error !== null && users.length === 0 ? null : inactive.length === 0 ? <p className="text-sm text-slate-500">Nofaol koordinatorlar yo&apos;q.</p> : <ul className="space-y-2">{inactive.map((user) => <li key={user.id} className="rounded-lg bg-slate-100 p-3 text-sm"><span className="font-medium">{user.fullName}</span> · {user.userName} · {seatLabel(user.directionId)} · <span className="text-slate-600">Nofaol{user.deactivatedAt ? ` (${user.deactivatedAt.slice(0, 10)})` : ""}</span></li>)}</ul>}
      </ManagementPanel>
    </> : <ManagementPanel>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">Hisoblar</h2><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={includeInactive} onChange={(event) => setIncludeInactive(event.target.checked)} />Nofaollarni ko&apos;rsatish</label></div>
      {loading ? <p role="status" className="text-sm text-slate-500">Yuklanmoqda...</p> : users.length === 0 ? <p className="text-sm text-slate-500">Hozircha hisoblar yo&apos;q.</p> : <div className="overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50 text-slate-500"><tr><th className="px-3 py-2">Ism</th><th className="px-3 py-2">Login</th><th className="px-3 py-2">{kind === "faculty-leaders" ? "Fakultet" : "Yo'nalish"}</th><th className="px-3 py-2">Holat</th><th className="px-3 py-2">Amallar</th></tr></thead><tbody>{users.map((user) => <tr key={user.id} className="border-t border-slate-200"><td className="px-3 py-2 font-medium">{user.fullName}</td><td className="px-3 py-2">{user.userName}</td><td className="px-3 py-2">{seatLabel(kind === "faculty-leaders" ? user.facultyId : user.directionId)}</td><td className="px-3 py-2">{user.isActive ? "Faol" : "Nofaol"}</td><td className="px-3 py-2">{actions(user)}</td></tr>)}</tbody></table></div>}
    </ManagementPanel>}
    {password && <TemporaryPassword userName={password.userName} password={password.value} onClose={() => setPassword(null)} />}
  </div>;
}
