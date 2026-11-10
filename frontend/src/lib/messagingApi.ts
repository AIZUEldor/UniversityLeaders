// Typed services for announcements and notifications (exact backend contracts; see backend/README.md Phase 5).
import { apiRequest } from "./api";
import type { Role } from "./data";
import type { PagedResponse } from "./publicApi";

export interface SentAnnouncement {
  id: string;
  title: string;
  body: string;
  authorId: string;
  authorName: string;
  createdAt: string;
  recipients: number;
  read: number;
}

export interface RecipientStatus {
  userId: string;
  fullName: string;
  facultyId: number | null;
  isActive: boolean;
  readAt: string | null;
}

export interface AnnouncementDelivery {
  announcement: SentAnnouncement;
  recipients: RecipientStatus[];
}

export interface InboxAnnouncement {
  id: string;
  title: string;
  body: string;
  createdAt: string;
  readAt: string | null;
}

export type NotificationType = "ReportSubmitted" | "ReportApproved" | "ReportRejected";

export interface AppNotification {
  id: string;
  type: NotificationType;
  reportId: string | null;
  title: string;
  /** The report title. Review comments are never included. */
  message: string;
  createdAt: string;
  readAt: string | null;
}

interface UnreadCount { unread: number; }

function paged(path: string, page: number, pageSize: number, unreadOnly?: boolean): string {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (unreadOnly !== undefined) query.set("unreadOnly", String(unreadOnly));
  return `${path}?${query}`;
}

const unread = async (path: string): Promise<number> => (await apiRequest<UnreadCount>(path)).unread;

/** University Leader: announcements go to every active faculty leader at send time. */
export const adminAnnouncementsApi = {
  send: (title: string, body: string): Promise<SentAnnouncement> =>
    apiRequest<SentAnnouncement>("/api/admin/announcements", { method: "POST", body: { title, body } }),
  list: (page: number, pageSize = 10): Promise<PagedResponse<SentAnnouncement>> =>
    apiRequest<PagedResponse<SentAnnouncement>>(paged("/api/admin/announcements", page, pageSize)),
  delivery: (id: string): Promise<AnnouncementDelivery> => apiRequest<AnnouncementDelivery>(`/api/admin/announcements/${id}`),
};

/** Faculty leader: the API scopes the inbox to the signed-in account; requests carry no recipient id. */
export const inboxAnnouncementsApi = {
  list: (page: number, unreadOnly: boolean, pageSize = 10): Promise<PagedResponse<InboxAnnouncement>> =>
    apiRequest<PagedResponse<InboxAnnouncement>>(paged("/api/faculty/announcements", page, pageSize, unreadOnly)),
  unreadCount: (): Promise<number> => unread("/api/faculty/announcements/unread-count"),
  markRead: (id: string): Promise<InboxAnnouncement> =>
    apiRequest<InboxAnnouncement>(`/api/faculty/announcements/${id}/read`, { method: "POST" }),
};

export const notificationsApi = {
  list: (page: number, unreadOnly: boolean, pageSize = 20): Promise<PagedResponse<AppNotification>> =>
    apiRequest<PagedResponse<AppNotification>>(paged("/api/notifications", page, pageSize, unreadOnly)),
  unreadCount: (): Promise<number> => unread("/api/notifications/unread-count"),
  markRead: (id: string): Promise<AppNotification> => apiRequest<AppNotification>(`/api/notifications/${id}/read`, { method: "POST" }),
  markAllRead: (): Promise<number> => apiRequest<{ marked: number }>("/api/notifications/read-all", { method: "POST" }).then((r) => r.marked),
};

export const NOTIFICATION_LABELS: Record<NotificationType, string> = {
  ReportSubmitted: "Ko'rib chiqish uchun yangi hisobot",
  ReportApproved: "Hisobotingiz tasdiqlandi",
  ReportRejected: "Hisobotingiz rad etildi",
};

/** Where a notification's report opens for this role: reviewers go to the review page, authors to their own report. */
export function notificationReportHref(role: Role, notification: AppNotification): string | null {
  if (notification.reportId === null) return null;
  if (notification.type !== "ReportSubmitted") return `/dashboard/reports/${notification.reportId}`;
  return role === "university_leader" ? `/admin/approvals/${notification.reportId}` : null; // only the University Leader reviews
}

export const formatDateTime = (iso: string): string =>
  new Date(iso).toLocaleString("uz-UZ", { dateStyle: "short", timeStyle: "short" });
