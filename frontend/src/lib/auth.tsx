"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { ApiError, apiRequest, setCsrfToken, setUnauthorizedHandler } from "./api";
import type { Role } from "./data";

// Identity comes only from the API (GET /api/auth/me). Nothing about the session is stored in the browser except the
// HTTP-only cookie the API sets. Client-side checks below are for navigation only; the API enforces every permission.

type ApiRole = "UniversityLeader" | "FacultyLeader" | "UniversityCoordinator" | "FacultyCoordinator";

const ROLE_FROM_API: Record<ApiRole, Role> = {
  UniversityLeader: "university_leader",
  FacultyLeader: "faculty_leader",
  UniversityCoordinator: "university_coordinator",
  FacultyCoordinator: "faculty_coordinator",
};

interface ApiCurrentUser {
  id: string;
  userName: string;
  fullName: string;
  role: ApiRole;
  mustChangePassword: boolean;
  facultyId: number | null;
  directionId: number | null;
}

interface ApiSession {
  user: ApiCurrentUser;
  csrfToken: string;
}

export interface CurrentUser {
  id: string;
  userName: string;
  fullName: string;
  role: Role;
  mustChangePassword: boolean;
  facultyId: number | null;
  directionId: number | null;
}

export type AuthState =
  | { status: "loading" }
  | { status: "anonymous"; expired: boolean }
  | { status: "authenticated"; user: CurrentUser };

interface AuthContextValue {
  state: AuthState;
  login: (userName: string, password: string) => Promise<CurrentUser>;
  changePassword: (currentPassword: string, newPassword: string) => Promise<CurrentUser>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/** Landing area per role. */
export function homeFor(role: Role): "/admin" | "/dashboard" {
  return role === "university_leader" ? "/admin" : "/dashboard";
}

function toUser(user: ApiCurrentUser): CurrentUser {
  const role: Role | undefined = ROLE_FROM_API[user.role];
  if (role === undefined) {
    throw new ApiError(0, "unknown_role", "The server returned an unknown role.");
  }

  return { ...user, role };
}

/**
 * Reads the session from the API. A 401 means signed out; after having been signed in it means the session ended
 * (expired, revoked by logout/password change elsewhere, or account deactivated). Returns null when the API is unreachable.
 */
async function loadSession(wasAuthenticated: boolean): Promise<AuthState | null> {
  try {
    const user: ApiCurrentUser = await apiRequest<ApiCurrentUser>("/api/auth/me");
    return { status: "authenticated", user: toUser(user) };
  } catch (error: unknown) {
    if (error instanceof ApiError && error.status === 401) {
      setCsrfToken(null);
      return { status: "anonymous", expired: wasAuthenticated };
    }

    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: "loading" });
  const statusRef = useRef<AuthState["status"]>("loading");
  useEffect(() => {
    statusRef.current = state.status;
  }, [state.status]);

  // Coming back to the tab re-validates the session, so expiry, revocation or deactivation is noticed without a reload.
  useEffect(() => {
    let cancelled: boolean = false;
    const apply = (wasAuthenticated: boolean): void => {
      void loadSession(wasAuthenticated).then((next: AuthState | null) => {
        if (cancelled) return;
        // null = API unreachable: keep a known session, otherwise show signed out.
        setState((previous: AuthState) => next ?? (previous.status === "authenticated" ? previous : { status: "anonymous", expired: false }));
      });
    };
    const onVisible = (): void => {
      if (document.visibilityState === "visible" && statusRef.current === "authenticated") apply(true);
    };

    apply(false);
    document.addEventListener("visibilitychange", onVisible);
    return () => {
      cancelled = true;
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, []);

  // Any API call that finds the session gone ends the client session too.
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setCsrfToken(null);
      setState({ status: "anonymous", expired: true });
    });
    return () => setUnauthorizedHandler(null);
  }, []);

  const login = useCallback(async (userName: string, password: string): Promise<CurrentUser> => {
    const session: ApiSession = await apiRequest<ApiSession>("/api/auth/login", { method: "POST", body: { userName, password } });
    setCsrfToken(session.csrfToken);
    const user: CurrentUser = toUser(session.user);
    setState({ status: "authenticated", user });
    return user;
  }, []);

  const changePassword = useCallback(async (currentPassword: string, newPassword: string): Promise<CurrentUser> => {
    const session: ApiSession = await apiRequest<ApiSession>("/api/auth/change-password", { method: "POST", body: { currentPassword, newPassword } });
    setCsrfToken(session.csrfToken);
    const user: CurrentUser = toUser(session.user);
    setState({ status: "authenticated", user });
    return user;
  }, []);

  const logout = useCallback(async (): Promise<void> => {
    try {
      await apiRequest<void>("/api/auth/logout", { method: "POST" });
    } catch (error: unknown) {
      // An already-ended session is fine; anything else still ends the local session state.
      if (!(error instanceof ApiError)) throw error;
    } finally {
      setCsrfToken(null);
      setState({ status: "anonymous", expired: false });
    }
  }, []);

  const value: AuthContextValue = useMemo(() => ({ state, login, changePassword, logout }), [state, login, changePassword, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value: AuthContextValue | null = useContext(AuthContext);
  if (!value) throw new Error("useAuth must be used inside AuthProvider");
  return value;
}

/** User-facing message for an API failure (Uzbek UI). */
export function describeError(error: unknown): string {
  if (!(error instanceof ApiError)) return "Kutilmagan xatolik yuz berdi.";
  switch (error.code) {
    case "invalid_credentials":
      return "Login yoki parol noto'g'ri.";
    case "too_many_requests":
      return "Juda ko'p urinish. Birozdan keyin qayta urinib ko'ring.";
    case "network_error":
      return "Server bilan aloqa yo'q. Internet yoki API manzilini tekshiring.";
    case "not_configured":
      return "API manzili sozlanmagan (NEXT_PUBLIC_API_URL).";
    case "https_required":
      return "API faqat HTTPS orqali ishlaydi.";
    case "validation_failed":
      return Object.values(error.fieldErrors).flat()[0] ?? "Ma'lumotlarni tekshiring.";
    case "concurrent_update":
      return "Ma'lumot boshqa joyda o'zgartirilgan. Ro'yxat yangilandi — o'zgarishni qaytadan kiriting.";
    case "username_taken":
      return "Bu login band. Boshqa login tanlang.";
    case "faculty_leader_exists":
      return "Bu fakultetda faol rahbar allaqachon bor.";
    case "direction_coordinator_exists":
      return "Bu yo'nalishda faol universitet koordinatori allaqachon bor.";
    case "coordinator_slot_taken":
      return "Bu yo'nalishda faol fakultet koordinatori allaqachon bor. Ro'yxatni yangilang.";
    case "faculty_name_taken":
      return "Bunday nomli fakultet allaqachon mavjud.";
    case "faculty_limit_reached":
      return "Fakultetlar soni 13 tadan oshmaydi.";
    case "faculty_in_use":
      return "Fakultetga foydalanuvchi yoki hisobotlar (tarixiy ham) bog'langan — o'chirib bo'lmaydi.";
    case "already_inactive":
      return "Foydalanuvchi allaqachon nofaol.";
    case "user_inactive":
      return "Nofaol foydalanuvchini tahrirlab bo'lmaydi.";
    case "user_not_found":
    case "faculty_not_found":
      return "Yozuv topilmadi (o'chirilgan yoki o'zgargan bo'lishi mumkin).";
    case "forbidden":
      return "Bu amal uchun ruxsat yo'q.";
    case "storage_unavailable":
      return "Server vaqtincha mavjud emas. Birozdan keyin qayta urinib ko'ring.";
    case "report_not_found":
      return "Hisobot topilmadi yoki uni ko'rishga ruxsat yo'q.";
    case "report_locked":
      return "Hisobot hozir tahrirlanmaydi. Yangilangan holatini tekshiring.";
    case "report_not_submitted":
      return "Faqat ko'rib chiqilayotgan hisobotni baholash mumkin.";
    case "attachment_limit_reached":
      return "Bir hisobotga ko'pi bilan 10 ta fayl biriktiriladi.";
    case "attachment_not_found":
    case "attachment_unavailable":
      return "Fayl topilmadi yoki hozir mavjud emas.";
    case "storage_quota_reached":
      return "Fayllar uchun ajratilgan joy to'ldi. Yangi fayl yuklab bo'lmaydi.";
    case "no_recipients":
      return "Faol fakultet rahbari yo'q — e'lonni qabul qiladigan hech kim yo'q.";
    case "announcement_not_found":
    case "notification_not_found":
      return "Xabar topilmadi.";
    case "payload_too_large":
      return "Fayl hajmi 10 MB chegaradan oshdi.";
    default:
      return error.message || "Xatolik yuz berdi.";
  }
}
