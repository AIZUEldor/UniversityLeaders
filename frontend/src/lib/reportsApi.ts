import { apiDownload, apiRequest } from "./api";

export type ReportStatus = "Draft" | "Submitted" | "Approved" | "Rejected";
export type ReportAuthorRole = "FacultyLeader" | "FacultyCoordinator" | "UniversityCoordinator";
export type ReviewAction = "Approved" | "Rejected";

export interface ReportSummary {
  id: string;
  title: string;
  status: ReportStatus;
  authorId: string;
  authorName: string;
  authorRole: ReportAuthorRole;
  authorIsActive: boolean;
  facultyId: number | null;
  directionId: number | null;
  activityDate: string | null;
  score: number | null;
  submittedAt: string | null;
  updatedAt: string;
  version: number;
}

export interface ScoreBreakdownItem { criterion: string; points: number; }
export interface Review {
  id: string;
  action: ReviewAction;
  score: number | null;
  scoreBreakdown: ScoreBreakdownItem[];
  comment: string | null;
  reviewerId: string;
  reviewerName: string;
  createdAt: string;
}

export interface AttachmentMetadata {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedById: string;
  uploadedAt: string;
}

export interface ReportDetail {
  summary: ReportSummary;
  description: string | null;
  outcome: string | null;
  participantCount: number | null;
  createdAt: string;
  reviews: Review[];
  attachments: AttachmentMetadata[];
}

export interface ReportContent {
  title: string;
  description: string | null;
  activityDate: string | null;
  outcome: string | null;
  participantCount: number | null;
}

const reportPath = (id: string): string => `/api/reports/${id}`;
const attachmentPath = (reportId: string): string => `${reportPath(reportId)}/attachments`;

export const reportsApi = {
  mine: (): Promise<ReportSummary[]> => apiRequest<ReportSummary[]>("/api/reports/mine"),
  reviewQueue: (status: Exclude<ReportStatus, "Draft"> = "Submitted"): Promise<ReportSummary[]> =>
    apiRequest<ReportSummary[]>(`/api/reports/review-queue?status=${status}`),
  get: (id: string): Promise<ReportDetail> => apiRequest<ReportDetail>(reportPath(id)),
  create: (content: ReportContent): Promise<ReportDetail> =>
    apiRequest<ReportDetail>("/api/reports", { method: "POST", body: content }),
  update: (id: string, content: ReportContent, version: number): Promise<ReportDetail> =>
    apiRequest<ReportDetail>(reportPath(id), { method: "PUT", body: { ...content, version } }),
  submit: (id: string, version: number): Promise<ReportDetail> =>
    apiRequest<ReportDetail>(`${reportPath(id)}/submit`, { method: "POST", body: { version } }),
  approve: (id: string, version: number, score: number, scoreBreakdown: ScoreBreakdownItem[], comment: string | null): Promise<ReportDetail> =>
    apiRequest<ReportDetail>(`${reportPath(id)}/approve`, { method: "POST", body: { version, score, scoreBreakdown, comment } }),
  reject: (id: string, version: number, comment: string): Promise<ReportDetail> =>
    apiRequest<ReportDetail>(`${reportPath(id)}/reject`, { method: "POST", body: { version, comment } }),
};

export const attachmentsApi = {
  upload: (reportId: string, file: File): Promise<AttachmentMetadata> => {
    const formData = new FormData();
    formData.append("file", file);
    return apiRequest<AttachmentMetadata>(attachmentPath(reportId), { method: "POST", formData });
  },
  remove: (reportId: string, attachmentId: string): Promise<void> =>
    apiRequest<void>(`${attachmentPath(reportId)}/${attachmentId}`, { method: "DELETE" }),
  download: (reportId: string, attachmentId: string): Promise<Blob> =>
    apiDownload(`${attachmentPath(reportId)}/${attachmentId}`),
};

/** Mirrors the API's score breakdown limits; the server remains authoritative. */
export function approvalError(score: number, items: ScoreBreakdownItem[]): string | null {
  if (!Number.isInteger(score) || score < 0 || score > 10) return "Ball 0 dan 10 gacha butun son bo'lishi kerak.";
  if (items.length < 1 || items.length > 10) return "1–10 ta baholash mezonini kiriting.";
  if (items.some((item) => item.criterion.trim().length < 1 || item.criterion.trim().length > 100 || !Number.isInteger(item.points) || item.points < 0 || item.points > 10))
    return "Har bir mezon 1–100 belgi, balli esa 0–10 oralig'ida bo'lishi kerak.";
  if (items.reduce((sum, item) => sum + item.points, 0) !== score) return "Mezon ballari jami umumiy ballga teng bo'lishi kerak.";
  return null;
}
