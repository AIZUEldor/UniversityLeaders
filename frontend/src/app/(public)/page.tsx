"use client";

import Link from "next/link";
import { useCallback } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { publicApi } from "@/lib/publicApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

export default function Home() {
  const request = useCallback(() => publicApi.directory(), []);
  const { data, loading, error, reload } = usePublicRequest(request);
  return <div className="space-y-8">
    <section className="rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 p-6 text-white shadow">
      <h1 className="text-2xl font-bold md:text-3xl">Universitet yetakchilari platformasi</h1>
      <p className="mt-2 text-indigo-100">Universitet rahbari: <strong>{data?.universityLeaderName ?? "Hozircha tayinlanmagan"}</strong> · {data?.faculties.length ?? 0} fakultet · 7 koordinator yo&apos;nalishi</p>
    </section>
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">Fakultetlar yuklanmoqda...</p> : error !== null && data === null ? null : <>
      <section><h2 className="mb-3 text-xl font-semibold">Fakultetlar</h2>
        {data?.faculties.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">Hozircha fakultetlar kiritilmagan.</p> : <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{data?.faculties.map((faculty) => <Link key={faculty.id} href={`/fakultetlar/${faculty.id}`} className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-indigo-400 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-indigo-500">
          <div className="text-xs font-semibold uppercase tracking-wide text-indigo-600">Fakultet #{faculty.id}</div><h3 className="mt-1 text-lg font-semibold">{faculty.name}</h3><p className="mt-2 text-sm text-slate-600">Rahbar: {faculty.leaderName ?? "Vakant"}</p><span className="mt-3 inline-block text-xs text-indigo-700">Profilni ko&apos;rish →</span>
        </Link>)}</div>}
      </section>
      <section><h2 className="mb-3 text-xl font-semibold">Universitet koordinatorlari (7 yo&apos;nalish)</h2>
        <ol className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">{data?.universityCoordinators.map((slot) => <li key={slot.directionId} className="rounded-lg border border-slate-200 bg-white p-4 text-sm shadow-sm"><div className="font-medium text-indigo-700">{slot.directionId}. {slot.directionName}</div><div className="mt-1 text-slate-600">{slot.coordinatorName ?? "Vakant"}</div></li>)}</ol>
      </section>
    </>}
  </div>;
}