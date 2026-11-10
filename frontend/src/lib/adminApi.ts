// Typed services for the management APIs (exact backend contracts; see backend/README.md).
import { apiRequest } from "./api";

export interface FacultyDto {
  id: number;
  name: string;
  description: string | null;
  version: number;
}

export type ManagedRole = "FacultyLeader" | "FacultyCoordinator";

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
  /** False for coordinators: they are public profiles without a login. */
  hasLogin: boolean;
  photoId: string | null;
  phone: string | null;
  phoneIsPublic: boolean;
}

/** Returned once by provisioning or a password reset of a faculty leader. Never store or log the temporary password. */
export interface ProvisionedUserDto {
  user: ManagedUserDto;
  temporaryPassword: string;
}

/** Coordinator contact; the phone is shown publicly only with explicit consent. */
export interface ProfileContact {
  phone: string;
  phoneIsPublic: boolean;
}

export const facultiesApi = {
  list: (): Promise<FacultyDto[]> => apiRequest<FacultyDto[]>("/api/admin/faculties"),
  create: (name: string, description: string): Promise<FacultyDto> =>
    apiRequest<FacultyDto>("/api/admin/faculties", { method: "POST", body: { name, description } }),
  update: (id: number, name: string, description: string, version: number): Promise<FacultyDto> =>
    apiRequest<FacultyDto>(`/api/admin/faculties/${id}`, { method: "PUT", body: { name, description, version } }),
  remove: (id: number): Promise<void> => apiRequest<void>(`/api/admin/faculties/${id}`, { method: "DELETE" }),
};

function photoApi(base: string) {
  return {
    /** Expects an already downsized JPEG/PNG (see lib/photo.ts). */
    setPhoto: (id: string, photo: Blob): Promise<ManagedUserDto> => {
      const form = new FormData();
      form.append("file", photo, "photo.jpg");
      return apiRequest<ManagedUserDto>(`${base}/${id}/photo`, { method: "PUT", formData: form });
    },
    removePhoto: (id: string): Promise<ManagedUserDto> => apiRequest<ManagedUserDto>(`${base}/${id}/photo`, { method: "DELETE" }),
  };
}

function lifecycleApi(base: string) {
  return {
    list: (includeInactive: boolean): Promise<ManagedUserDto[]> => apiRequest<ManagedUserDto[]>(`${base}?includeInactive=${includeInactive}`),
    deactivate: (id: string, version: number): Promise<ManagedUserDto> =>
      apiRequest<ManagedUserDto>(`${base}/${id}/deactivate`, { method: "POST", body: { version } }),
    ...photoApi(base),
  };
}

/** Faculty leaders have a login: user name, one-time temporary password and password reset. */
export const facultyLeadersApi = {
  ...lifecycleApi("/api/admin/faculty-leaders"),
  create: (userName: string, fullName: string, facultyId: number): Promise<ProvisionedUserDto> =>
    apiRequest<ProvisionedUserDto>("/api/admin/faculty-leaders", { method: "POST", body: { userName, fullName, facultyId } }),
  update: (id: string, fullName: string, facultyId: number, version: number): Promise<ManagedUserDto> =>
    apiRequest<ManagedUserDto>(`/api/admin/faculty-leaders/${id}`, { method: "PUT", body: { fullName, facultyId, version } }),
  /** New one-time temporary password; the account must change it and all its sessions end. */
  resetPassword: (id: string, version: number): Promise<ProvisionedUserDto> =>
    apiRequest<ProvisionedUserDto>(`/api/admin/faculty-leaders/${id}/reset-password`, { method: "POST", body: { version } }),
};

/**
 * Faculty coordinators: profiles without a login, managed by their own faculty leader. The faculty is resolved by the
 * API from the signed-in faculty leader; requests never contain facultyId.
 */
export const facultyCoordinatorsApi = {
  ...lifecycleApi("/api/faculty/coordinators"),
  create: (fullName: string, directionId: number, contact: ProfileContact): Promise<ManagedUserDto> =>
    apiRequest<ManagedUserDto>("/api/faculty/coordinators", { method: "POST", body: { fullName, directionId, ...contact } }),
  update: (id: string, fullName: string, directionId: number, contact: ProfileContact, version: number): Promise<ManagedUserDto> =>
    apiRequest<ManagedUserDto>(`/api/faculty/coordinators/${id}`, { method: "PUT", body: { fullName, directionId, ...contact, version } }),
};
