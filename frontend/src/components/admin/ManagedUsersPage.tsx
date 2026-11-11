"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api";
import {
  facultiesApi, facultyCoordinatorsApi, facultyLeadersApi, universityCoordinatorsApi,
  type FacultyDto, type ManagedUserDto, type ProfileContact,
} from "@/lib/adminApi";
import { DIRECTIONS } from "@/lib/data";
import { useT } from "@/lib/i18n";
import { ManagementMessage, ManagementPanel, TemporaryPassword, inputClass, primaryClass, secondaryClass } from "./ManagementUi";
import { PhotoUploader } from "./PhotoUploader";

/** Faculty leaders have a login (13 seats = faculties); coordinators are profiles without login (7 seats = directions). */
export type ManagementKind = "faculty-leaders" | "faculty-coordinators" | "university-coordinators";
interface Props { kind: ManagementKind; title: string; }
interface Editor { id: string | null; userName: string; fullName: string; seatId: string; phone: string; phoneIsPublic: boolean; version: number | null; }
interface Seat { id: number; name: string; }
const emptyEditor: Editor = { id: null, userName: "", fullName: "", seatId: "", phone: "", phoneIsPublic: false, version: null };

const SUBTITLES: Record<ManagementKind, string> = {
  "faculty-leaders": "13 fakultetning har biriga bitta yetakchi: login, vaqtinchalik parol, rasm va parolni tiklash.",
  "faculty-coordinators": "Fakultetingizdagi 7 yo'nalishning har biriga bitta koordinator. Koordinatorlar tizimga kirmaydi; F.I.Sh., rasm va ruxsat berilgan telefon raqami ommaviy profilingizda ko'rinadi.",
  "university-coordinators": "Universitet yetakchisining 7 yo'nalishdagi o'z koordinatorlari. Koordinatorlar tizimga kirmaydi; F.I.Sh., rasm va ruxsat berilgan telefon raqami bosh sahifadagi yetakchi profilida ko'rinadi.",
};

export function ManagedUsersPage({ kind, title }: Props) {
  const t = useT();
  const isLeaders = kind === "faculty-leaders";
  const coordinators = kind === "university-coordinators" ? universityCoordinatorsApi : facultyCoordinatorsApi;
  const photos = isLeaders ? facultyLeadersApi : coordinators;
  const [users, setUsers] = useState<ManagedUserDto[]>([]);
  const [faculties, setFaculties] = useState<FacultyDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [editor, setEditor] = useState<Editor>(emptyEditor);
  const [password, setPassword] = useState<{ userName: string; value: string } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, seats] = await Promise.all([
        (isLeaders ? facultyLeadersApi : coordinators).list(true),
        isLeaders ? facultiesApi.list() : Promise.resolve([] as FacultyDto[]),
      ]);
      setUsers(list);
      setFaculties(seats);
      setError(null);
    } catch (failure: unknown) { setError(failure); }
    finally { setLoading(false); }
  }, [isLeaders, coordinators]);

  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const seats: Seat[] = isLeaders ? faculties.map((f) => ({ id: f.id, name: f.name })) : DIRECTIONS.map((name, index) => ({ id: index + 1, name: t(name) }));
  const seatOf = (user: ManagedUserDto): number | null => (isLeaders ? user.facultyId : user.directionId);
  const seatName = (id: number | null): string => seats.find((seat) => seat.id === id)?.name ?? (id === null ? "—" : `#${id}`);
  const active: ManagedUserDto[] = users.filter((user) => user.isActive);
  const inactive: ManagedUserDto[] = users.filter((user) => !user.isActive);
  const occupiedByOther = (seatId: number): boolean => active.some((user) => seatOf(user) === seatId && user.id !== editor.id);

  const startEdit = (user: ManagedUserDto): void => {
    setEditor({
      id: user.id, userName: user.userName, fullName: user.fullName, seatId: String(seatOf(user) ?? ""),
      phone: user.phone ?? "", phoneIsPublic: user.phoneIsPublic, version: user.version,
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  };
  const startCreate = (seatId: number): void => { setEditor({ ...emptyEditor, seatId: String(seatId) }); window.scrollTo({ top: 0, behavior: "smooth" }); };

  // Shared by every mutation: a 409 (stale version, taken seat, already inactive) reloads the list and keeps the message.
  const runAction = async (action: () => Promise<void>): Promise<void> => {
    setBusy(true); setError(null);
    try { await action(); await load(); }
    catch (failure: unknown) {
      setError(failure);
      if (failure instanceof ApiError && failure.status === 409) { setEditor(emptyEditor); await load(); setError(failure); }
    } finally { setBusy(false); }
  };

  const save = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    const seatId = Number(editor.seatId);
    if (!Number.isInteger(seatId) || seatId < 1) return;
    if (occupiedByOther(seatId)) { setError(new ApiError(409, isLeaders ? "faculty_leader_exists" : "coordinator_slot_taken", "Seat occupied")); return; }
    const contact: ProfileContact = { phone: editor.phone.trim(), phoneIsPublic: editor.phoneIsPublic && editor.phone.trim() !== "" };
    void runAction(async () => {
      if (isLeaders && editor.id === null) {
        const result = await facultyLeadersApi.create(editor.userName.trim(), editor.fullName.trim(), seatId);
        setPassword({ userName: result.user.userName, value: result.temporaryPassword });
      } else if (isLeaders && editor.version !== null && editor.id !== null) {
        await facultyLeadersApi.update(editor.id, editor.fullName.trim(), seatId, editor.version);
      } else if (editor.id === null) {
        await coordinators.create(editor.fullName.trim(), seatId, contact);
      } else if (editor.version !== null) {
        await coordinators.update(editor.id, editor.fullName.trim(), seatId, contact, editor.version);
      }
      setEditor(emptyEditor);
    });
  };

  const deactivate = (user: ManagedUserDto): void => {
    if (!window.confirm(t("{name} profilini nofaol qilishni tasdiqlaysizmi? O'rin bo'shaydi, tarix saqlanadi.", { name: user.fullName }))) return;
    void runAction(async () => { await photos.deactivate(user.id, user.version); });
  };

  const resetPassword = (user: ManagedUserDto): void => {
    if (!window.confirm(t("{name} uchun yangi vaqtinchalik parol yaratilsinmi? Joriy parol va barcha ochiq sessiyalar bekor qilinadi.", { name: user.fullName }))) return;
    void runAction(async () => {
      const result = await facultyLeadersApi.resetPassword(user.id, user.version);
      setPassword({ userName: result.user.userName, value: result.temporaryPassword });
    });
  };

  const vacantLabel = t(isLeaders ? "Yetakchi tayinlanmagan" : "Koordinator tayinlanmagan");
  const assignLabel = t(isLeaders ? "Yetakchi tayinlash" : "Koordinator tayinlash");

  return <div className="space-y-6">
    <div><h1 className="text-2xl font-bold">{t(title)}</h1><p className="text-sm text-slate-500">{t(SUBTITLES[kind])}</p></div>
    {error !== null && <ManagementMessage error={error} onRetry={() => void load()} />}

    <ManagementPanel>
      <h2 className="mb-4 font-semibold">{t(editor.id ? "Profilni tahrirlash" : isLeaders ? "Yangi fakultet yetakchisi" : "Yangi koordinator")}</h2>
      <form onSubmit={save} className="grid gap-3 md:grid-cols-2">
        {isLeaders && editor.id === null && <label className="text-sm">{t("Login")}<input required minLength={3} maxLength={64} autoComplete="off" value={editor.userName} onChange={(event) => setEditor({ ...editor, userName: event.target.value })} className={`${inputClass} mt-1`} /></label>}
        <label className="text-sm">{t("F.I.Sh.")}<input required maxLength={200} value={editor.fullName} onChange={(event) => setEditor({ ...editor, fullName: event.target.value })} className={`${inputClass} mt-1`} /></label>
        <label className="text-sm">{t(isLeaders ? "Fakultet" : "Yo'nalish")}
          <select required value={editor.seatId} onChange={(event) => setEditor({ ...editor, seatId: event.target.value })} className={`${inputClass} mt-1`}>
            <option value="">{t("Tanlang")}</option>
            {seats.map((seat) => <option key={seat.id} value={seat.id} disabled={occupiedByOther(seat.id)}>{seat.name}{occupiedByOther(seat.id) ? ` (${t("band")})` : ""}</option>)}
          </select>
        </label>
        {!isLeaders && <>
          <label className="text-sm">{t("Telefon raqami (ixtiyoriy)")}<input type="tel" inputMode="tel" maxLength={20} placeholder="+998 90 123-45-67" value={editor.phone} onChange={(event) => setEditor({ ...editor, phone: event.target.value })} className={`${inputClass} mt-1`} /></label>
          <label className="flex items-center gap-2 text-sm md:col-span-2"><input type="checkbox" checked={editor.phoneIsPublic} disabled={editor.phone.trim() === ""} onChange={(event) => setEditor({ ...editor, phoneIsPublic: event.target.checked })} />{t("Telefon raqamini ommaviy saytda ko'rsatishga koordinator rozilik bergan")}</label>
        </>}
        <div className="flex items-end gap-2 md:col-span-2">
          <button disabled={busy || loading || occupiedByOther(Number(editor.seatId))} className={primaryClass}>{t(busy ? "Saqlanmoqda..." : editor.id ? "Saqlash" : "Yaratish")}</button>
          {(editor.id || editor.seatId) && <button type="button" onClick={() => setEditor(emptyEditor)} className={secondaryClass}>{t("Bekor qilish")}</button>}
        </div>
        {!editor.id && <p className="text-xs text-slate-500 md:col-span-2">{t("Rasmni profil yaratilgandan keyin ro'yxatdagi kartadan yuklang.")}</p>}
      </form>
    </ManagementPanel>

    <ManagementPanel>
      <h2 className="mb-3 font-semibold">{t(isLeaders ? "Fakultetlar" : "Yo'nalishlar")} ({active.length}/{isLeaders ? seats.length : 7} {t("band")})</h2>
      {loading ? <p role="status" className="text-sm text-slate-500">{t("Yuklanmoqda...")}</p> : error !== null && users.length === 0 && seats.length === 0 ? null
        : seats.length === 0 ? <p className="text-sm text-slate-500">{t("Avval fakultetlarni kiriting.")}</p>
        : <ol className="space-y-3">{seats.map((seat, index) => {
          const user = active.find((item) => seatOf(item) === seat.id);
          return <li key={seat.id} className="rounded-xl border border-slate-200 p-4">
            <h3 className="text-sm font-medium">{index + 1}. {seat.name}</h3>
            {user ? <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm">
              <PhotoUploader photoId={user.photoId} name={user.fullName} disabled={busy}
                upload={(photo) => photos.setPhoto(user.id, photo)} remove={() => photos.removePhoto(user.id)} onChanged={() => void load()} />
              <div className="min-w-0 flex-1">
                <p className="font-semibold">{user.fullName}</p>
                <p className="text-xs text-slate-500">
                  {user.hasLogin ? `${t("Login")}: ${user.userName}` : t("Loginsiz profil")}
                  {user.phone && ` · ${user.phone} (${t(user.phoneIsPublic ? "ommaviy" : "faqat ichki")})`}
                </p>
              </div>
              <div className="flex flex-wrap gap-3">
                <button type="button" disabled={busy} onClick={() => startEdit(user)} className="text-indigo-700 hover:underline">{t("Tahrirlash")}</button>
                {user.hasLogin && <button type="button" disabled={busy} onClick={() => resetPassword(user)} className="text-amber-700 hover:underline">{t("Parolni tiklash")}</button>}
                <button type="button" disabled={busy} onClick={() => deactivate(user)} className="text-red-700 hover:underline">{t(isLeaders ? "Nofaol qilish" : "Almashtirish uchun bo'shatish")}</button>
              </div>
            </div>
              : <div className="mt-2 flex flex-wrap items-center justify-between gap-2 text-sm"><span className="text-slate-500">{vacantLabel}</span><button type="button" disabled={busy} onClick={() => startCreate(seat.id)} className="text-indigo-700 hover:underline disabled:opacity-50">{assignLabel}</button></div>}
          </li>;
        })}</ol>}
    </ManagementPanel>

    <ManagementPanel>
      <h2 className="mb-3 font-semibold">{t("Nofaol profillar")} ({inactive.length})</h2>
      {loading ? <p role="status" className="text-sm text-slate-500">{t("Yuklanmoqda...")}</p> : inactive.length === 0 ? <p className="text-sm text-slate-500">{t("Nofaol profillar yo'q.")}</p>
        : <ul className="space-y-2">{inactive.map((user) => <li key={user.id} className="rounded-lg bg-slate-100 p-3 text-sm"><span className="font-medium">{user.fullName}</span> · {seatName(seatOf(user))} · <span className="text-slate-600">{t("Nofaol")}{user.deactivatedAt ? ` (${user.deactivatedAt.slice(0, 10)})` : ""}</span></li>)}</ul>}
    </ManagementPanel>
    {password && <TemporaryPassword userName={password.userName} password={password.value} onClose={() => setPassword(null)} />}
  </div>;
}
