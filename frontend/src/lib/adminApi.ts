// Typed services for the management APIs (exact backend contracts; see backend/README.md).
import { apiRequest } from "./api";

export interface FacultyDto {
  id: number;
  name: string;
  description: string | null;
  version: number;
}

export type ManagedRole = "FacultyLeader" | "FacultyCoordinator" | "UniversityCoordinator";

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

// An empty id addresses the signed-in account itself (/api/admin/profile/photo).
const photoPath = (base: string, id: string): string => (id === "" ? `${base}/photo` : `${base}/${id}/photo`);

function photoApi(base: string) {
  return {
    /** Expects an already downsized JPEG/PNG (see lib/photo.ts). */
    setPhoto: (id: string, photo: Blob): Promise<ManagedUserDto> => {
      const form = new FormData();
      form.append("file", photo, "photo.jpg");
      return apiRequest<ManagedUserDto>(photoPath(base, id), { method: "PUT", formData: form });
    },
    removePhoto: (id: string): Promise<ManagedUserDto> => apiRequest<ManagedUserDto>(photoPath(base, id), { method: "DELETE" }),
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

/** The signed-in University Leader's own public photo (featured on the public home page). */
export const ownProfileApi = photoApi("/api/admin/profile");

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

/** Coordinators: profiles without a login, one per direction (1-7). Requests never contain facultyId. */
function coordinatorsApi(base: string) {
  return {
    ...lifecycleApi(base),
    create: (fullName: string, directionId: number, contact: ProfileContact): Promise<ManagedUserDto> =>
      apiRequest<ManagedUserDto>(base, { method: "POST", body: { fullName, directionId, ...contact } }),
    update: (id: string, fullName: string, directionId: number, contact: ProfileContact, version: number): Promise<ManagedUserDto> =>
      apiRequest<ManagedUserDto>(`${base}/${id}`, { method: "PUT", body: { fullName, directionId, ...contact, version } }),
  };
}

/** Managed by their own faculty leader; the API resolves the faculty from the signed-in leader. */
export const facultyCoordinatorsApi = coordinatorsApi("/api/faculty/coordinators");

/** The University Leader's own 7 coordinators (no faculty). */
export const universityCoordinatorsApi = coordinatorsApi("/api/admin/university-coordinators");
