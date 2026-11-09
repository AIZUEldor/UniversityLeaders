// Typed services for the University Leader management APIs (exact backend contracts; see backend/README.md Phase 3B).
import { apiRequest } from "./api";

export interface FacultyDto {
  id: number;
  name: string;
  version: number;
}

export type ManagedRole = "FacultyLeader" | "UniversityCoordinator" | "FacultyCoordinator";

export interface ManagedUserDto {
  id: string;
  userName: string;
  fullName: string;
  role: ManagedRole;
  facultyId: number | null;
  directionId: number | null;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  deactivatedAt: string | null;
  version: number;
}

/** Returned once by provisioning or a password reset. The temporary password must never be stored or logged. */
export interface ProvisionedUserDto {
  user: ManagedUserDto;
  temporaryPassword: string;
}

export const facultiesApi = {
  list: (): Promise<FacultyDto[]> => apiRequest<FacultyDto[]>("/api/admin/faculties"),
  create: (name: string): Promise<FacultyDto> => apiRequest<FacultyDto>("/api/admin/faculties", { method: "POST", body: { name } }),
  rename: (id: number, name: string, version: number): Promise<FacultyDto> =>
    apiRequest<FacultyDto>(`/api/admin/faculties/${id}`, { method: "PUT", body: { name, version } }),
  remove: (id: number): Promise<void> => apiRequest<void>(`/api/admin/faculties/${id}`, { method: "DELETE" }),
};

/** Seat field per kind: faculty leaders sit in a faculty, university coordinators in a direction (1-7). */
export type ManagedKind = "faculty-leaders" | "university-coordinators";

type SeatBody = { facultyId: number } | { directionId: number };

function seatBody(kind: ManagedKind, seatId: number): SeatBody {
  return kind === "faculty-leaders" ? { facultyId: seatId } : { directionId: seatId };
}

export function managedUsersApi(kind: ManagedKind) {
  const base: string = `/api/admin/${kind}`;
  return {
    list: (includeInactive: boolean): Promise<ManagedUserDto[]> => apiRequest<ManagedUserDto[]>(`${base}?includeInactive=${includeInactive}`),
    create: (userName: string, fullName: string, seatId: number): Promise<ProvisionedUserDto> =>
      apiRequest<ProvisionedUserDto>(base, { method: "POST", body: { userName, fullName, ...seatBody(kind, seatId) } }),
    update: (id: string, fullName: string, seatId: number, version: number): Promise<ManagedUserDto> =>
      apiRequest<ManagedUserDto>(`${base}/${id}`, { method: "PUT", body: { fullName, ...seatBody(kind, seatId), version } }),
    deactivate: (id: string, version: number): Promise<ManagedUserDto> =>
      apiRequest<ManagedUserDto>(`${base}/${id}/deactivate`, { method: "POST", body: { version } }),
    /** New one-time temporary password; the account must change it and all its sessions end. */
    resetPassword: (id: string, version: number): Promise<ProvisionedUserDto> =>
      apiRequest<ProvisionedUserDto>(`${base}/${id}/reset-password`, { method: "POST", body: { version } }),
  };
}

/** Faculty ownership is resolved by the API from the signed-in Faculty Leader. Requests never contain facultyId. */
export const facultyCoordinatorsApi = {
  list: (includeInactive: boolean): Promise<ManagedUserDto[]> =>
    apiRequest<ManagedUserDto[]>(`/api/faculty/coordinators?includeInactive=${includeInactive}`),
  create: (userName: string, fullName: string, directionId: number): Promise<ProvisionedUserDto> =>
    apiRequest<ProvisionedUserDto>("/api/faculty/coordinators", { method: "POST", body: { userName, fullName, directionId } }),
  update: (id: string, fullName: string, directionId: number, version: number): Promise<ManagedUserDto> =>
    apiRequest<ManagedUserDto>(`/api/faculty/coordinators/${id}`, { method: "PUT", body: { fullName, directionId, version } }),
  deactivate: (id: string, version: number): Promise<ManagedUserDto> =>
    apiRequest<ManagedUserDto>(`/api/faculty/coordinators/${id}/deactivate`, { method: "POST", body: { version } }),
  resetPassword: (id: string, version: number): Promise<ProvisionedUserDto> =>
    apiRequest<ProvisionedUserDto>(`/api/faculty/coordinators/${id}/reset-password`, { method: "POST", body: { version } }),
};
