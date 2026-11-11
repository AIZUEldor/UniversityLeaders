"use client";

import Link from "next/link";
import { useCallback } from "react";
import { ManagementMessage } from "@/components/admin/ManagementUi";
import { PersonPhoto } from "@/components/PersonPhoto";
import { CoordinatorSlots } from "@/components/public/CoordinatorSlots";
import { useT } from "@/lib/i18n";
import { publicApi } from "@/lib/publicApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

/** The University Leader's public profile: like a faculty profile, with their own 7 direction coordinators. */
export default function UniversityLeaderProfile() {
  const t = useT();
  const request = useCallback(() => publicApi.directory(), []);
  const { data, loading, error, reload } = usePublicRequest(request);
  return <div className="space-y-8">
    <Link href="/" className="text-sm text-indigo-700 hover:underline">← {t("Bosh sahifa")}</Link>
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">{t("Yuklanmoqda...")}</p> : data === null ? null : <>
      <section className="hero-shine fade-up flex flex-col items-center gap-5 rounded-3xl bg-gradient-to-br from-amber-500 via-amber-400 to-yellow-500 p-6 text-center text-white shadow-xl sm:flex-row sm:text-left md:p-8">
        <div className="rounded-full bg-white/40 p-1"><PersonPhoto photoId={data.universityLeaderPhotoId} name={data.universityLeaderName} size="lg" /></div>
        <div className="min-w-0 flex-1">
          <div className="text-xs font-semibold uppercase tracking-wide text-white/90">{t("O'zMU Yoshlar ittifoqi")}</div>
          <h1 className="mt-2 text-2xl font-extrabold md:text-3xl">{data.universityLeaderName ?? t("Tayinlanmagan")}</h1>
          <p className="mt-1 inline-block rounded-full bg-white/20 px-3 py-0.5 text-sm ring-1 ring-white/30">{t("Universitet yetakchisi")}</p>
        </div>
      </section>
      <CoordinatorSlots slots={data.universityCoordinators} />
    </>}
  </div>;
}
