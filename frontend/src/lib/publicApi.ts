import { apiRequest } from "./api";

export interface PublicDirectionSlot {
  directionId: number;
  directionName: string;
  coordinatorName: string | null;
}

export interface PublicFaculty {
  id: number;
  name: string;
  leaderName: string | null;
  coordinators: PublicDirectionSlot[];
}

export interface PublicDirectory {
  universityLeaderName: string | null;
  universityCoordinators: PublicDirectionSlot[];
  faculties: PublicFaculty[];
}

export interface CompletedWork {
  id: string;
  title: string;
  description: string | null;
  outcome: string | null;
  activityDate: string | null;
  participantCount: number | null;
  facultyId: number | null;
  facultyName: string | null;
  directionId: number | null;
  directionName: string | null;
  authorName: string;
  score: number;
  approvedAt: string;
}

export interface PagedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface FacultyRanking {
  position: number;
  facultyId: number;
  facultyName: string;
  approvedReports: number;
  totalScore: number;
}

export const publicApi = {
  directory: (): Promise<PublicDirectory> => apiRequest<PublicDirectory>("/api/public/directory"),
  completedWork: ({ facultyId, page = 1, pageSize = 12 }: { facultyId?: number; page?: number; pageSize?: number } = {}): Promise<PagedResponse<CompletedWork>> => {
    const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (facultyId !== undefined) query.set("facultyId", String(facultyId));
    return apiRequest<PagedResponse<CompletedWork>>(`/api/public/completed-work?${query}`);
  },
  rankings: (): Promise<FacultyRanking[]> => apiRequest<FacultyRanking[]>("/api/public/faculty-rankings"),
};
