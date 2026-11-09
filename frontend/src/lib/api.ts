// Typed client for the ASP.NET Core API. The session lives in an HTTP-only cookie set by the API;
// this module never sees it. The CSRF token is kept in memory only (never localStorage).

const API_BASE_URL: string = (process.env.NEXT_PUBLIC_API_URL ?? "").replace(/\/+$/, "");

/** RFC 7807 problem details as returned by the API, with its stable `code`. */
interface ProblemBody {
  code?: string;
  title?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Record<string, string[]>;

  constructor(status: number, code: string, message: string, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }
}

type HttpMethod = "GET" | "POST" | "PUT" | "DELETE";

interface RequestOptions {
  method?: HttpMethod;
  body?: unknown;
  formData?: FormData;
}

/** Configured API origin without a trailing slash ("" when not configured). */
export function apiBaseUrl(): string {
  return API_BASE_URL;
}

let csrfToken: string | null = null;
let unauthorizedHandler: (() => void) | null = null;

/** Called when a non-auth endpoint answers 401 (session expired or revoked); registered by AuthProvider. */
export function setUnauthorizedHandler(handler: (() => void) | null): void {
  unauthorizedHandler = handler;
}

/** Login and password change return a token bound to the new session; logout clears it. */
export function setCsrfToken(token: string | null): void {
  csrfToken = token;
}

async function getCsrfToken(): Promise<string> {
  if (csrfToken === null) {
    const response: { csrfToken: string } = await send<{ csrfToken: string }>("/api/auth/csrf", { method: "GET" });
    csrfToken = response.csrfToken;
  }

  return csrfToken;
}

/**
 * Calls the API with cookies (credentials: "include") and, for state-changing methods, the X-CSRF-TOKEN header.
 * A stale CSRF token (e.g. after the session changed in another tab) is refreshed and the request retried once.
 */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const method: HttpMethod = options.method ?? "GET";
  if (method === "GET") {
    return send<T>(path, options);
  }

  try {
    return await send<T>(path, options, await getCsrfToken());
  } catch (error: unknown) {
    if (error instanceof ApiError && error.code === "csrf_invalid") {
      csrfToken = null;
      return send<T>(path, options, await getCsrfToken());
    }

    throw error;
  }
}

async function send<T>(path: string, options: RequestOptions, csrf?: string): Promise<T> {
  const response: Response = await sendResponse(path, options, csrf);
  if (response.status === 204) return undefined as T;
  const text: string = await response.text();
  return (text === "" ? null : safeJson(text)) as T;
}

/** Authenticated binary download; errors and session expiry follow the same path as JSON requests. */
export async function apiDownload(path: string): Promise<Blob> {
  const response: Response = await sendResponse(path, { method: "GET" });
  return response.blob();
}

async function sendResponse(path: string, options: RequestOptions, csrf?: string): Promise<Response> {
  if (API_BASE_URL === "") {
    throw new ApiError(0, "not_configured", "NEXT_PUBLIC_API_URL is not configured.");
  }

  const headers: Record<string, string> = { Accept: "application/json" };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  if (csrf !== undefined) headers["X-CSRF-TOKEN"] = csrf;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: options.method ?? "GET",
      headers,
      body: options.formData ?? (options.body === undefined ? undefined : JSON.stringify(options.body)),
      credentials: "include",
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, "network_error", "The server could not be reached.");
  }

  if (!response.ok) {
    if (response.status === 401 && !path.startsWith("/api/auth/")) {
      unauthorizedHandler?.();
    }

    const text: string = await response.text();
    const problem: ProblemBody = (text === "" ? {} : safeJson(text) ?? {}) as ProblemBody;
    throw new ApiError(response.status, problem.code ?? `http_${response.status}`, problem.title ?? response.statusText, problem.errors ?? {});
  }
  return response;
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}
