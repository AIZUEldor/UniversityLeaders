"use client";

import Link from "next/link";
import { useCallback } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { PersonPhoto } from "@/components/PersonPhoto";
import { publicApi, type PublicFaculty } from "@/lib/publicApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

function FacultyCard({ faculty, position }: { faculty: PublicFaculty; position: number }) {
  return <Link href={`/fakultetlar/${faculty.id}`}
    className="group flex h-full flex-col items-center rounded-2xl border border-slate-200 bg-white p-5 text-center shadow-sm transition hover:-translate-y-0.5 hover:border-indigo-400 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-indigo-500">
    <span className="self-start text-xs font-semibold uppercase tracking-wide text-indigo-600">{position}-fakultet</span>
    <PersonPhoto photoId={faculty.leaderPhotoId} name={faculty.leaderName} size="lg" />
    <h3 className="mt-3 text-base font-semibold leading-snug">{faculty.name}</h3>
    {faculty.description && <p className="mt-1 text-xs text-slate-500">{faculty.description}</p>}
    <p className={`mt-3 text-sm ${faculty.leaderName ? "font-medium text-slate-800" : "italic text-slate-400"}`}>
      {faculty.leaderName ?? "Yetakchi tayinlanmagan"}
    </p>
    <p className="text-xs text-slate-500">Fakultet yetakchisi</p>
    <span className="mt-auto pt-3 text-xs font-medium text-indigo-700 group-hover:underline">Profilni ko&apos;rish →</span>
  </Link>;
}

export default function Home() {
  const request = useCallback(() => publicApi.directory(), []);
  const { data, loading, error, reload } = usePublicRequest(request);
  return <div className="space-y-8">
    <section className="rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 p-6 text-white shadow">
      <h1 className="text-2xl font-bold md:text-3xl">Universitet yetakchilari platformasi</h1>
      <p className="mt-2 text-indigo-100">Universitet yetakchisi: <strong>{data?.universityLeaderName ?? "Tayinlanmagan"}</strong> · 13 fakultet yetakchisi · 91 fakultet koordinatori (har fakultetda 7 yo&apos;nalish)</p>
    </section>
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">Fakultetlar yuklanmoqda...</p> : error !== null && data === null ? null : <>
      <section><h2 className="mb-3 text-xl font-semibold">Fakultet yetakchilari</h2>
        {data?.faculties.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">Hozircha fakultetlar kiritilmagan.</p>
          : <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">{data?.faculties.map((faculty, index) => <FacultyCard key={faculty.id} faculty={faculty} position={index + 1} />)}</div>}
      </section>
    </>}
  </div>;
}
