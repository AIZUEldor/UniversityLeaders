"use client";

import Link from "next/link";
import { useCallback } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { CompletedWorkPanel } from "@/components/public/CompletedWorkPanel";
import { publicApi } from "@/lib/publicApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

export function FacultyProfile({ id }: { id: number }) {
  const request = useCallback(() => publicApi.directory(), []);
  const { data, loading, error, reload } = usePublicRequest(request);
  const faculty = data?.faculties.find((item) => item.id === id);
  return <div className="space-y-8">
    <Link href="/" className="text-sm text-indigo-700 hover:underline">← Barcha fakultetlar</Link>
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">Fakultet profili yuklanmoqda...</p> : error !== null && data === null ? null : !faculty ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-600">Fakultet topilmadi.</p> : <>
      <section className="flex flex-wrap items-center gap-4 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 p-6 text-white shadow"><div className="flex h-16 w-16 items-center justify-center rounded-full bg-white/20 text-2xl font-bold">{faculty.id}</div><div className="min-w-0 flex-1"><div className="text-xs font-semibold uppercase tracking-wide text-indigo-100">Ommaviy profil · {faculty.name}</div><h1 className="text-2xl font-bold md:text-3xl">{faculty.leaderName ?? "Rahbar o'rni vakant"}</h1><p className="text-indigo-100">Fakultet rahbari</p></div><Link href={`/login?faculty=${faculty.id}`} className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-indigo-700 shadow hover:bg-indigo-50">Profilga kirish</Link></section>
      <section><h2 className="mb-3 text-xl font-semibold">Koordinatorlar (7 yo&apos;nalish)</h2><ol className="grid gap-3 sm:grid-cols-2">{faculty.coordinators.map((slot) => <li key={slot.directionId} className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white p-3 shadow-sm"><span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-sm font-semibold text-indigo-700">{slot.directionId}</span><div><div className="text-sm font-medium">{slot.directionName}</div><div className="text-xs text-slate-500">{slot.coordinatorName ?? "Vakant"}</div></div></li>)}</ol></section>
      <section><h2 className="mb-3 text-xl font-semibold">Tasdiqlangan ishlar</h2><CompletedWorkPanel key={faculty.id} facultyId={faculty.id} /></section>
    </>}
  </div>;
}