"use client";

import Link from "next/link";
import { useCallback, useState } from "react";
import { ManagementMessage, Pager, secondaryClass } from "@/components/admin/ManagementUi";
import { useAuth } from "@/lib/auth";
import { formatDateTime, NOTIFICATION_LABELS, notificationReportHref, notificationsApi, type AppNotification } from "@/lib/messagingApi";
import { announceLocalChange, useMessagingHints, type MessagingHint } from "@/lib/realtime";
import { usePublicRequest } from "@/lib/usePublicRequest";

/** The signed-in user's own report notifications (any role). Rendered only inside PrivateShell. */
export function NotificationsList() {
  const { state } = useAuth();
  const role = state.status === "authenticated" ? state.user.role : null;
  const [page, setPage] = useState(1);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [actionError, setActionError] = useState<unknown>(null);
  const request = useCallback(() => notificationsApi.list(page, unreadOnly), [page, unreadOnly]);
  const { data, loading, error, reload } = usePublicRequest(request);
  useMessagingHints(true, (hint: MessagingHint) => { if (hint !== "announcement") void reload(); });

  const run = async (action: () => Promise<unknown>): Promise<void> => {
    try { await action(); setActionError(null); announceLocalChange(); }
    catch (failure: unknown) { setActionError(failure); }
  };
  const markRead = (item: AppNotification): Promise<void> => run(() => notificationsApi.markRead(item.id));
  const hasUnread: boolean = data?.items.some((item: AppNotification) => item.readAt === null) ?? false;

  return <div className="space-y-4">
    <div className="flex flex-wrap items-center gap-3">
      <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={unreadOnly} onChange={(event) => { setUnreadOnly(event.target.checked); setPage(1); }} />Faqat o&apos;qilmaganlar</label>
      <button type="button" disabled={!hasUnread} onClick={() => void run(notificationsApi.markAllRead)} className={`${secondaryClass} ml-auto`}>Hammasini o&apos;qildi deb belgilash</button>
    </div>
    {actionError !== null && <ManagementMessage error={actionError} />}
    {error !== null && <ManagementMessage error={error} onRetry={() => void reload()} />}
    {loading && data === null ? <p role="status" className="text-sm text-slate-500">Bildirishnomalar yuklanmoqda...</p>
      : data === null ? null
      : data.items.length === 0 ? <p className="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-500">{unreadOnly ? "O'qilmagan bildirishnoma yo'q." : "Hozircha bildirishnoma yo'q."}</p>
      : <>
        <ul className="space-y-2">{data.items.map((item: AppNotification) => {
          const href: string | null = role === null ? null : notificationReportHref(role, item);
          const unread: boolean = item.readAt === null;
          return <li key={item.id} className={`rounded-lg border bg-white p-3 ${unread ? "border-indigo-300" : "border-slate-200"}`}>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="font-medium">{unread && <span className="mr-2 rounded-full bg-indigo-100 px-2 py-0.5 text-xs text-indigo-800">Yangi</span>}{NOTIFICATION_LABELS[item.type]}</div>
                <p className="break-words text-sm text-slate-600">{item.message}</p>
                <div className="mt-1 text-xs text-slate-400">{formatDateTime(item.createdAt)}</div>
              </div>
              <div className="flex shrink-0 gap-2 text-sm">
                {href && <Link href={href} onClick={() => { if (unread) void markRead(item); }} className="text-indigo-700 hover:underline">Hisobotni ochish</Link>}
                {unread && <button type="button" onClick={() => void markRead(item)} className="text-slate-600 underline">O&apos;qildi</button>}
              </div>
            </div>
          </li>;
        })}</ul>
        <Pager page={page} total={data.total} pageSize={data.pageSize} label="Bildirishnomalar sahifalari" onPage={setPage} />
      </>}
  </div>;
}
