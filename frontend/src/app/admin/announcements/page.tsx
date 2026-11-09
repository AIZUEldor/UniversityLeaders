"use client";

import { useCallback, useState, type FormEvent } from "react";
import { inputClass, ManagementMessage, ManagementPanel, Pager, primaryClass, secondaryClass } from "@/components/admin/ManagementUi";
import { facultiesApi, managedUsersApi } from "@/lib/adminApi";
import { adminAnnouncementsApi, formatDateTime, type RecipientStatus, type SentAnnouncement } from "@/lib/messagingApi";
import { usePublicRequest } from "@/lib/usePublicRequest";

export default function AdminAnnouncements() {
  const [page, setPage] = useState(1);
  const [openId, setOpenId] = useState<string | null>(null);
  const listRequest = useCallback(() => adminAnnouncementsApi.list(page), [page]);
  const list = usePublicRequest(listRequest);
  const leadersRequest = useCallback(() => managedUsersApi("faculty-leaders").list(false), []);
  const leaders = usePublicRequest(leadersRequest);

  const onSent = (): void => {
    setOpenId(null);
    if (page === 1) void list.reload(); else setPage(1);
  };

  return <div className="space-y-6">
    <h1 className="text-2xl font-bold">E&apos;lonlar</h1>
    <SendForm activeLeaders={leaders.data?.length ?? null} onSent={onSent} />
    <section className="space-y-3">
      <h2 className="font-semibold">Yuborilganlar{list.data && ` (${list.data.total})`}</h2>
      {list.error !== null && <ManagementMessage error={list.error} onRetry={() => void list.reload()} />}
      {list.loading && list.data === null ? <p role="status" className="text-sm text-slate-500">E&apos;lonlar yuklanmoqda...</p>
        : list.data === null ? null
        : list.data.items.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">Hozircha e&apos;lon yuborilmagan.</p>
        : <>
          <ul className="space-y-2">{list.data.items.map((item: SentAnnouncement) => <li key={item.id} className="rounded-lg border border-slate-200 bg-white p-3">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0"><div className="font-medium">{item.title}</div><div className="text-xs text-slate-400">{item.authorName} · {formatDateTime(item.createdAt)}</div></div>
              <span className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-700">O&apos;qilgan: {item.read} / {item.recipients}</span>
            </div>
            <p className="mt-2 whitespace-pre-wrap break-words text-sm text-slate-600">{item.body}</p>
            <button type="button" aria-expanded={openId === item.id} onClick={() => setOpenId(openId === item.id ? null : item.id)} className="mt-2 text-sm text-indigo-700 hover:underline">
              {openId === item.id ? "Yetkazish holatini yopish" : "Yetkazish holati"}
            </button>
            {openId === item.id && <DeliveryStatus id={item.id} />}
          </li>)}</ul>
          <Pager page={page} total={list.data.total} pageSize={list.data.pageSize} label="E'lonlar sahifalari" onPage={setPage} />
        </>}
    </section>
  </div>;
}

function SendForm({ activeLeaders, onSent }: { activeLeaders: number | null; onSent: () => void }) {
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [sentTo, setSentTo] = useState<number | null>(null);

  const submit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    setBusy(true);
    setSentTo(null);
    try {
      const sent: SentAnnouncement = await adminAnnouncementsApi.send(title.trim(), body.trim());
      setTitle(""); setBody(""); setError(null); setSentTo(sent.recipients);
      onSent();
    } catch (failure: unknown) { setError(failure); }
    finally { setBusy(false); }
  };

  return <ManagementPanel><form onSubmit={(event) => void submit(event)} className="space-y-3">
    <h2 className="font-semibold">Yangi e&apos;lon{activeLeaders !== null && ` → ${activeLeaders} ta faol fakultet rahbariga`}</h2>
    <label className="block text-sm">Sarlavha<input value={title} onChange={(event) => setTitle(event.target.value)} maxLength={300} required className={`${inputClass} mt-1`} /></label>
    <label className="block text-sm">Matn<textarea value={body} onChange={(event) => setBody(event.target.value)} maxLength={10000} rows={4} required className={`${inputClass} mt-1`} /></label>
    {error !== null && <ManagementMessage error={error} />}
    <div className="flex flex-wrap items-center gap-3">
      <button type="submit" disabled={busy || title.trim() === "" || body.trim() === ""} className={primaryClass}>{busy ? "Yuborilmoqda..." : "Yuborish"}</button>
      {sentTo !== null && <span role="status" className="text-sm text-emerald-700">✓ {sentTo} ta fakultet rahbariga yuborildi.</span>}
    </div>
  </form></ManagementPanel>;
}

function DeliveryStatus({ id }: { id: string }) {
  const request = useCallback(() => adminAnnouncementsApi.delivery(id), [id]);
  const { data, loading, error, reload } = usePublicRequest(request);
  const facultiesRequest = useCallback(() => facultiesApi.list(), []);
  const faculties = usePublicRequest(facultiesRequest);
  const facultyName = (facultyId: number | null): string =>
    faculties.data?.find((faculty) => faculty.id === facultyId)?.name ?? (facultyId === null ? "—" : `Fakultet #${facultyId}`);

  return <div className="mt-3 space-y-2">
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading && data === null ? <p role="status" className="text-sm text-slate-500">Yetkazish holati yuklanmoqda...</p>
      : data === null ? null
      : <>
        <div className="overflow-x-auto rounded-lg border border-slate-200"><table className="w-full text-sm">
          <caption className="sr-only">Qabul qiluvchilar va o&apos;qilganlik holati</caption>
          <thead className="bg-slate-50 text-left text-slate-500"><tr><th className="px-3 py-2">Rahbar</th><th className="px-3 py-2">Fakultet</th><th className="px-3 py-2">Holat</th></tr></thead>
          <tbody>{data.recipients.map((recipient: RecipientStatus) => <tr key={recipient.userId} className="border-t">
            <td className="px-3 py-2">{recipient.fullName}{!recipient.isActive && <span className="ml-2 rounded-full bg-slate-200 px-2 py-0.5 text-xs text-slate-600">nofaol</span>}</td>
            <td className="px-3 py-2">{facultyName(recipient.facultyId)}</td>
            <td className="px-3 py-2">{recipient.readAt === null ? <span className="text-amber-700">O&apos;qilmagan</span> : <span className="text-emerald-700">O&apos;qilgan · {formatDateTime(recipient.readAt)}</span>}</td>
          </tr>)}</tbody>
        </table></div>
        <button type="button" onClick={() => void reload()} className={secondaryClass}>Holatni yangilash</button>
      </>}
  </div>;
}
