"use client";

import { useCallback, useState } from "react";
import { ManagementMessage, Pager } from "@/components/admin/ManagementUi";
import { publicApi, type CompletedWork } from "@/lib/publicApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

function WorkCard({ work }: { work: CompletedWork }) {
  return <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
    <div className="flex flex-wrap items-start justify-between gap-2"><h3 className="font-semibold">{work.title}</h3><span className="rounded-full bg-emerald-100 px-2 py-1 text-xs font-medium text-emerald-800">{work.score}/10 ball</span></div>
    <p className="mt-1 text-xs text-slate-500">{work.authorName} · {work.facultyName ?? "Universitet"}{work.directionName && ` · ${work.directionName}`} · {work.activityDate ?? work.approvedAt.slice(0, 10)}</p>
    {work.description && <p className="mt-3 whitespace-pre-wrap text-sm text-slate-700">{work.description}</p>}
    {work.outcome && <p className="mt-2 whitespace-pre-wrap text-sm text-slate-600"><strong>Natija:</strong> {work.outcome}</p>}
    {work.participantCount !== null && <p className="mt-2 text-xs text-slate-500">Ishtirokchilar: {work.participantCount}</p>}
  </article>;
}

export function CompletedWorkPanel({ facultyId }: { facultyId?: number }) {
  const [page, setPage] = useState(1);
  const request = useCallback(() => publicApi.completedWork({ facultyId, page }), [facultyId, page]);
  const { data, loading, error, reload } = usePublicRequest(request);

  return <div className="space-y-4">
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading ? <p role="status" className="text-sm text-slate-500">Tasdiqlangan ishlar yuklanmoqda...</p>
      : error !== null && data === null ? null
      : data?.items.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">Hozircha tasdiqlangan ish yo&apos;q.</p>
      : <><div className="space-y-3">{data?.items.map((work) => <WorkCard key={work.id} work={work} />)}</div>
        {data && <Pager page={page} total={data.total} pageSize={data.pageSize} label="Ishlar sahifalari" onPage={setPage} />}
      </>}
  </div>;
}
