"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { ROLE_LABELS, type Person, type Role } from "@/lib/data";
import { useStore } from "@/lib/store";

export default function Login() {
  const router = useRouter();
  const { people, setCurrentId } = useStore();
  const [role, setRole] = useState<Role>("faculty_coordinator");
  const candidates: Person[] = people.filter((p: Person) => p.role === role);
  const [personId, setPersonId] = useState<string>(candidates[0].id);

  const pickRole = (r: Role): void => {
    setRole(r);
    setPersonId(people.find((p: Person) => p.role === r)!.id);
  };

  const onDemoLogin = (e: FormEvent<HTMLFormElement>): void => {
    e.preventDefault();
    setCurrentId(personId);
    router.push(role === "university_leader" ? "/admin" : "/dashboard");
  };

  return (
    <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-indigo-50 to-slate-100 px-4 py-10">
      <div className="w-full max-w-md space-y-5">
        <div className="text-center">
          <Link href="/" className="text-xl font-bold text-indigo-700">
            Universitet Yetakchilari
          </Link>
          <p className="text-sm text-slate-500">Tizimga kirish</p>
        </div>

        <form className="space-y-3 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm" onSubmit={(e) => e.preventDefault()}>
          <input disabled placeholder="Login" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" />
          <input disabled type="password" placeholder="Parol" className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm" />
          <button disabled className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white opacity-50">
            Kirish
          </button>
          <p className="text-center text-xs text-slate-500">Haqiqiy avtorizatsiya hali ulanmagan.</p>
        </form>

        <form onSubmit={onDemoLogin} className="space-y-3 rounded-2xl border-2 border-dashed border-amber-400 bg-amber-50 p-6">
          <div>
            <div className="text-xs font-bold uppercase tracking-wide text-amber-800">DEV only — demo rol tanlash</div>
            <p className="mt-1 text-xs text-amber-900">
              Bu haqiqiy kirish emas va hech narsani himoya qilmaydi. Faqat prototipni sinash uchun; sahifa yangilanganda chiqib ketadi.
            </p>
          </div>
          <label className="block text-sm">
            Rol
            <select value={role} onChange={(e) => pickRole(e.target.value as Role)} className="mt-1 w-full rounded-md border border-slate-300 bg-white px-2 py-1.5">
              {(Object.keys(ROLE_LABELS) as Role[]).map((r: Role) => (
                <option key={r} value={r}>
                  {ROLE_LABELS[r]}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-sm">
            Demo foydalanuvchi
            <select value={personId} onChange={(e) => setPersonId(e.target.value)} className="mt-1 w-full rounded-md border border-slate-300 bg-white px-2 py-1.5">
              {candidates.map((p: Person) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>
          <button className="w-full rounded-md bg-amber-500 px-4 py-2 text-sm font-semibold text-amber-950 hover:bg-amber-400">Demo sifatida kirish</button>
        </form>
      </div>
    </main>
  );
}
