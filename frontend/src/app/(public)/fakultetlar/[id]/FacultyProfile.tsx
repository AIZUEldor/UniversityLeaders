"use client";

import Link from "next/link";
import { useCallback } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { PersonPhoto } from "@/components/PersonPhoto";
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
      <section className="flex flex-wrap items-center gap-5 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 p-6 text-white shadow">
        <PersonPhoto photoId={faculty.leaderPhotoId} name={faculty.leaderName} size="lg" />
        <div className="min-w-0 flex-1">
          <div className="text-xs font-semibold uppercase tracking-wide text-indigo-100">{faculty.name}</div>
          {faculty.description && <div className="text-sm text-indigo-100">{faculty.description}</div>}
          <h1 className="mt-1 text-2xl font-bold md:text-3xl">{faculty.leaderName ?? "Yetakchi tayinlanmagan"}</h1>
          <p className="text-indigo-100">Fakultet yetakchisi</p>
        </div>
      </section>
      <section><h2 className="mb-3 text-xl font-semibold">Yo&apos;nalishlar va koordinatorlar</h2>
        <ol className="grid gap-3 sm:grid-cols-2">{faculty.coordinators.map((slot) => <li key={slot.directionId} className="flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <PersonPhoto photoId={slot.photoId} name={slot.coordinatorName} size="md" />
          <div className="min-w-0">
            <div className="text-xs font-semibold text-indigo-700">{slot.directionId}. {slot.directionName}</div>
            <div className={`mt-1 ${slot.coordinatorName ? "font-medium text-slate-800" : "italic text-slate-400"}`}>{slot.coordinatorName ?? "Koordinator tayinlanmagan"}</div>
            {slot.phone && <a href={`tel:${slot.phone.replace(/[^+\d]/g, "")}`} className="text-sm text-indigo-700 hover:underline">{slot.phone}</a>}
          </div>
        </li>)}</ol>
      </section>
      <section><h2 className="mb-3 text-xl font-semibold">Tasdiqlangan bajarilgan ishlar</h2><CompletedWorkPanel key={faculty.id} facultyId={faculty.id} /></section>
    </>}
  </div>;
}
