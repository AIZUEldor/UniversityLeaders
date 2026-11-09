# UniversityLeaders frontend

Next.js 16 (App Router, TypeScript, Tailwind). Public website (`/`, `/reyting`, `/completed-work`, `/fakultetlar/[id]`),
login (`/login`) and two private areas: `/admin` (University Leader) and `/dashboard` (faculty leaders and coordinators).

## Status

| Area | State |
|---|---|
| Authentication | **Real** — ASP.NET Core API cookie session (`/api/auth/*`) |
| Admin faculties, faculty leaders, university coordinators; faculty leader's coordinator management | **Real** management APIs with creation, editing, password reset (temporary password shown once) and account deactivation |
| Private reports, review queues, approvals, attachments and overview counts | **Real** PostgreSQL and MinIO APIs; role and ownership checks are server-side |
| Announcements, notifications, admin ranking | **Real** APIs; nav shows unread counts; live hints from the SignalR hub (see below) |
| Public home, faculty profiles, completed work and ranking | **Real** read-only APIs; only approved work and active public directory names appear |

## Authentication design

- `src/lib/api.ts` — typed `fetch` wrapper: `credentials: "include"`, base URL from `NEXT_PUBLIC_API_URL`, `ApiError`
  with the API's stable `code` and field errors. State-changing requests send `X-CSRF-TOKEN`; the token is fetched from
  `GET /api/auth/csrf`, replaced by the one returned from login/change-password, kept **in memory only**, and refreshed
  once automatically on `csrf_invalid`.
- `src/lib/auth.tsx` — `AuthProvider` / `useAuth()`. The session is restored on every page load from
  `GET /api/auth/me` (a 401 means signed out). Returning to the tab re-validates it, so an ended session (expiry, logout
  elsewhere, password change, deactivation) shows "Sessiya tugadi". Nothing is stored in `localStorage`/`sessionStorage`;
  the session cookie is HTTP-only and unreadable by scripts.
- `src/components/PrivateShell.tsx` — layout for `/admin` and `/dashboard`: loading, signed-out and expired states;
  a pending password change shows only `ChangePasswordForm`; a user in the wrong area is sent to their own
  (University Leader → `/admin`, everyone else → `/dashboard`).
- **These checks are navigation and UX only.** Every API call is authorized by the server (roles, ownership, forced
  password change), whatever the browser shows. Role values are never taken from the client.
- `/login?faculty=N` (from a public profile's "Profilga kirish") only shows which faculty the visitor came from; it
  selects no account and grants nothing.
- There is no demo data or demo store; every page reads the real API.

## Realtime notifications

`src/lib/realtime.ts` holds one shared `@microsoft/signalr` connection to `{NEXT_PUBLIC_API_URL}/hubs/notifications`
(cookie-authenticated, `withCredentials`) while a signed-in view subscribes. Transport fallback (WebSockets → SSE →
long polling) and automatic reconnect with backoff (1 s → 30 s, never gives up) come from the client; a failed initial
start is retried the same way. Events carry ids only and are treated as **refresh hints**: on every (re)connect, live
event or local read change the open lists and nav badges re-read the REST endpoints, so the persisted inbox works even
when the hub is unreachable.

Ranking (`/reyting`, `/admin/ranking`, shared `RankingBoard`): the official rank is the API's score order. The
"Tasdiqlangan ishlar soni" toggle reorders by approved count with its own positions and keeps the official position in a
separate column.

## Local development

Prerequisites: the backend running locally (see `backend/README.md`) and the .NET development certificate trusted
(`dotnet dev-certs https --trust`, once per machine).

1. Frontend config:

   ```bash
   cp .env.example .env.local      # NEXT_PUBLIC_API_URL=https://localhost:5001
   ```

2. API: the dev frontend (`http://localhost:3000`) and the API (`https://localhost:5001`) are **different sites** for the
   browser (different scheme), so the API must allow the origin and send its cookies with `SameSite=None`
   (still `Secure`, HTTP-only). Set these for the API process (e.g. in the repo-root `.env` and load them as in
   `backend/README.md`):

   ```bash
   Cors__AllowedOrigins__0=http://localhost:3000
   Auth__SameSite=None
   ```

   Then run the API on HTTPS: `dotnet run --project src/UniversityLeaders.Api` (default `https` profile, port 5001).

3. `npm run dev` and open `http://localhost:3000/login`.

Production uses the same pattern: the deployed frontend origin in `Cors__AllowedOrigins__0`, `Auth__SameSite=None`
when frontend and API are on different sites (e.g. two `*.onrender.com` hosts), `Lax` when they share a site, and
`ForwardedHeaders__Enabled=true` behind Render's TLS proxy. `NEXT_PUBLIC_API_URL` is inlined at build time and must
never contain secrets.

### First account

There is no seeded user. Create the University Leader once with the API's bootstrap procedure
(`backend/README.md` → "Bootstrapping the University Leader"), log in with the temporary password and set a permanent
one. Faculty leaders and coordinators get temporary passwords from the provisioning APIs.

## Scripts

```bash
npm run dev       # development server
npm run build     # production build (needs NEXT_PUBLIC_API_URL)
npm run lint      # ESLint
npx tsc --noEmit  # type check
```

### Known dev-only console messages

`next dev` logs "Could not validate that a segment in your UI has instant navigation" (and a related
"URL data during prerendering" note for `/login`) for the private areas: their content depends on the signed-in user
and intentionally renders only after the session is known. These are development diagnostics; `npm run build` passes.
