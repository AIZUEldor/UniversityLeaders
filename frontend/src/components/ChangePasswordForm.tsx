"use client";

import { useState, type FormEvent } from "react";
import { ApiError } from "@/lib/api";
import { describeError, useAuth } from "@/lib/auth";

const MIN_LENGTH: number = 12; // mirrors the API rule; the API is authoritative

/** Real password change (POST /api/auth/change-password). Values live only in this component's state. */
export function ChangePasswordForm({ forced }: { forced: boolean }) {
  const { changePassword } = useAuth();
  const [current, setCurrent] = useState<string>("");
  const [next, setNext] = useState<string>("");
  const [confirm, setConfirm] = useState<string>("");
  const [error, setError] = useState<string>("");
  const [busy, setBusy] = useState<boolean>(false);
  const [done, setDone] = useState<boolean>(false);

  const onSubmit = async (e: FormEvent<HTMLFormElement>): Promise<void> => {
    e.preventDefault();
    setError("");
    if (next.length < MIN_LENGTH) return setError(`Yangi parol kamida ${MIN_LENGTH} belgidan iborat bo'lsin.`);
    if (next !== confirm) return setError("Yangi parollar bir xil emas.");

    setBusy(true);
    try {
      await changePassword(current, next);
      setDone(true);
    } catch (err: unknown) {
      const field: string | undefined = err instanceof ApiError ? (err.fieldErrors.currentPassword ?? err.fieldErrors.newPassword)?.[0] : undefined;
      setError(field ?? describeError(err));
    } finally {
      setCurrent("");
      setNext("");
      setConfirm("");
      setBusy(false);
    }
  };

  return (
    <form onSubmit={onSubmit} className="mx-auto max-w-md space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
      <div>
        <h1 className="text-xl font-bold">{forced ? "Vaqtinchalik parolni almashtiring" : "Parolni almashtirish"}</h1>
        <p className="mt-1 text-sm text-slate-500">
          {forced
            ? "Birinchi kirishda o'zingizning doimiy parolingizni o'rnating. Shundan keyingina boshqa bo'limlar ochiladi."
            : "Parol almashtirilgach boshqa qurilmalardagi sessiyalar yopiladi."}
        </p>
      </div>
      {done && <p className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-700">Parol almashtirildi.</p>}
      {error && (
        <p role="alert" className="rounded-md bg-rose-50 px-3 py-2 text-sm text-rose-700">
          {error}
        </p>
      )}
      <label className="block text-sm">
        Joriy (vaqtinchalik) parol
        <input type="password" autoComplete="current-password" required value={current} onChange={(e) => setCurrent(e.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" />
      </label>
      <label className="block text-sm">
        Yangi parol (kamida {MIN_LENGTH} belgi)
        <input type="password" autoComplete="new-password" required minLength={MIN_LENGTH} value={next} onChange={(e) => setNext(e.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" />
      </label>
      <label className="block text-sm">
        Yangi parolni takrorlang
        <input type="password" autoComplete="new-password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2" />
      </label>
      <button disabled={busy} className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-60">
        {busy ? "Saqlanmoqda..." : "Parolni saqlash"}
      </button>
    </form>
  );
}
