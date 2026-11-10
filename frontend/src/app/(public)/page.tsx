"use client";

import Link from "next/link";
import { useCallback } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { PersonPhoto } from "@/components/PersonPhoto";
import { UzFlag } from "@/components/UzFlag";
import { useT } from "@/lib/i18n";
import { publicApi, type PublicFaculty } from "@/lib/publicApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

function FacultyCard({ faculty, position }: { faculty: PublicFaculty; position: number }) {
  const t = useT();
  return <Link href={`/fakultetlar/${faculty.id}`} style={{ animationDelay: `${Math.min(position, 13) * 45}ms` }}
    className="fade-up btn group relative grid w-full grid-cols-[auto_1fr] items-center gap-x-4 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 text-left shadow-sm backdrop-blur transition hover:-translate-y-1 hover:border-indigo-300 hover:shadow-lg focus:outline-none sm:w-[calc(50%-0.5rem)] lg:flex lg:w-[calc(33.333%-0.667rem)] lg:flex-col lg:items-center lg:p-5 lg:text-center xl:w-[calc(25%-0.75rem)]">
    <span aria-hidden="true" className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-indigo-500 via-white to-violet-500 opacity-80" />
    <div className="row-span-4 transition duration-300 group-hover:scale-105 lg:order-2 lg:mt-2"><PersonPhoto photoId={faculty.leaderPhotoId} name={faculty.leaderName} size="card" /></div>
    <span className="justify-self-start rounded-full bg-indigo-50 px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wide text-indigo-700 lg:order-1 lg:self-start">{t("{n}-fakultet", { n: position })}</span>
    <h3 className="mt-1 text-sm font-semibold leading-snug text-slate-900 lg:order-3 lg:mt-3 lg:text-base">{faculty.name}</h3>
    {faculty.description && <p className="mt-0.5 text-xs text-slate-500 lg:order-4 lg:mt-1">{faculty.description}</p>}
    <div className="mt-1 w-full lg:order-5 lg:mt-3 lg:border-t lg:border-slate-100 lg:pt-3">
      <p className="hidden text-[11px] uppercase tracking-wide text-slate-400 lg:block">{t("Fakultet yetakchisi")}</p>
      <p className={`mt-0.5 text-sm ${faculty.leaderName ? "font-semibold text-slate-800" : "italic text-slate-400"}`}>{faculty.leaderName ?? t("Yetakchi tayinlanmagan")}</p>
    </div>
    <span className="hidden pt-3 text-xs font-semibold text-indigo-700 transition group-hover:translate-x-0.5 lg:order-6 lg:mt-auto lg:block">{t("Profilni ko'rish →")}</span>
  </Link>;
}

function Stat({ value, label }: { value: string; label: string }) {
  const t = useT();
  return <div className="rounded-xl bg-white/15 px-4 py-3 backdrop-blur-sm ring-1 ring-white/25">
    <div className="text-2xl font-bold leading-none">{value}</div>
    <div className="mt-1 text-xs text-white/85">{t(label)}</div>
  </div>;
}

export default function Home() {
  const request = useCallback(() => publicApi.directory(), []);
  const { data, loading, error, reload } = usePublicRequest(request);
  const t = useT();
  return <div className="space-y-10">
    <section className="hero-shine fade-up grid items-center gap-8 rounded-3xl bg-gradient-to-br from-indigo-600 via-indigo-500 to-violet-600 p-6 text-white shadow-xl md:grid-cols-[1.4fr_1fr] md:p-10">
      <div>
        <p className="inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-semibold uppercase tracking-wider ring-1 ring-white/25">{t("O'zMU Yoshlar ittifoqi")}</p>
        <h1 className="mt-4 text-3xl font-extrabold leading-tight md:text-4xl">{t("Universitet yetakchilari platformasi")}</h1>
        <p className="mt-3 max-w-xl text-white/90">{t("Fakultet yetakchilari, ularning 7 yo'nalishdagi koordinatorlari va tasdiqlangan ishlari bir joyda.")}</p>
        <p className="mt-2 text-sm text-white/80">{t("Universitet yetakchisi:")} <strong className="text-white">{data?.universityLeaderName ?? t("Tayinlanmagan")}</strong></p>
        <div className="mt-6 grid max-w-md grid-cols-3 gap-3">
          <Stat value="13" label="fakultet" />
          <Stat value="7" label="yo'nalish" />
          <Stat value="91" label="koordinator" />
        </div>
        <div className="mt-6 flex flex-wrap gap-3 text-sm font-semibold">
          <Link href="/reyting" className="btn rounded-lg bg-white px-4 py-2 text-indigo-700 shadow hover:bg-indigo-50">{t("Reytingni ko'rish")}</Link>
          <Link href="/completed-work" className="btn rounded-lg px-4 py-2 text-white ring-1 ring-white/60 hover:bg-white/10">{t("Bajarilgan ishlar")}</Link>
        </div>
      </div>
      <div className="mx-auto w-full max-w-xs md:max-w-sm">
        <UzFlag />
      </div>
    </section>

    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">{t("Fakultetlar yuklanmoqda...")}</p> : error !== null && data === null ? null : <section>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-2">
        <div><h2 className="text-2xl font-bold text-slate-900">{t("Fakultet yetakchilari")}</h2><p className="text-sm text-slate-500">{t("Fakultetni tanlang: yetakchi, 7 yo'nalish koordinatorlari va tasdiqlangan ishlar.")}</p></div>
      </div>
      {data?.faculties.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">{t("Hozircha fakultetlar kiritilmagan.")}</p>
        : <div className="flex flex-wrap justify-center gap-4">{data?.faculties.map((faculty, index) => <FacultyCard key={faculty.id} faculty={faculty} position={index + 1} />)}</div>}
    </section>}
  </div>;
}
