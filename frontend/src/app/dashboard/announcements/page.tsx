"use client";

import { useCallback, useState } from "react";
import { ManagementMessage, Pager } from "@/components/admin/ManagementUi";
import { formatDateTime, inboxAnnouncementsApi, type InboxAnnouncement } from "@/lib/messagingApi";
import { announceLocalChange, useMessagingHints, type MessagingHint } from "@/lib/realtime";
import { useT } from "@/lib/i18n";
import { usePublicRequest } from "@/lib/usePublicRequest";

/** The signed-in faculty leader's own inbox; the API returns only announcements addressed to this account. */
export default function DashboardAnnouncements() {
  const t = useT();
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [actionError, setActionError] = useState<unknown>(null);
  const request = useCallback(() => inboxAnnouncementsApi.list(page, unreadOnly), [page, unreadOnly]);
  const { data, loading, error, reload } = usePublicRequest(request);
  const unreadRequest = useCallback(() => inboxAnnouncementsApi.unreadCount(), []);
  const unread = usePublicRequest(unreadRequest);
  useMessagingHints(true, (hint: MessagingHint) => {
    if (hint === "notification") return;
    void reload();
    void unread.reload();
  });

  const markRead = async (id: string): Promise<void> => {
    try { await inboxAnnouncementsApi.markRead(id); setActionError(null); announceLocalChange(); }
    catch (failure: unknown) { setActionError(failure); }
  };

  return <div className="space-y-6">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h1 className="text-2xl font-bold">{t("Kiruvchi e'lonlar")}{unread.data !== null && <span className="ml-2 align-middle text-sm font-medium text-indigo-700">{t("{n} ta o'qilmagan", { n: unread.data })}</span>}</h1>
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={unreadOnly} onChange={(event) => { setUnreadOnly(event.target.checked); setPage(1); }} />{t("Faqat o'qilmaganlar")}</label>
    </div>
    {actionError !== null && <ManagementMessage error={actionError} />}
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading && data === null ? <p role="status" className="text-sm text-slate-500">{t("E'lonlar yuklanmoqda...")}</p>
      : data === null ? null
      : data.items.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">{t(unreadOnly ? "O'qilmagan e'lon yo'q." : "Hozircha e'lon yo'q.")}</p>
      : <>
        <ul className="space-y-2">{data.items.map((item: InboxAnnouncement) => <li key={item.id} className={`rounded-lg border bg-white p-3 ${item.readAt === null ? "border-indigo-300" : "border-slate-200"}`}>
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div className="min-w-0 font-medium">{item.readAt === null && <span className="mr-2 rounded-full bg-indigo-100 px-2 py-0.5 text-xs text-indigo-800">{t("Yangi")}</span>}{item.title}</div>
            {item.readAt === null
              ? <button type="button" onClick={() => void markRead(item.id)} className="text-sm text-indigo-700 underline">{t("O'qildi deb belgilash")}</button>
              : <span className="text-xs text-slate-400">{t("O'qilgan")} · {formatDateTime(item.readAt)}</span>}
          </div>
          <p className="mt-1 whitespace-pre-wrap break-words text-sm text-slate-600">{item.body}</p>
          <div className="mt-1 text-xs text-slate-400">{t("Universitet rahbari")} · {formatDateTime(item.createdAt)}</div>
        </li>)}</ul>
        <Pager page={page} total={data.total} pageSize={data.pageSize} label="E'lonlar sahifalari" onPage={setPage} />
      </>}
  </div>;
}
