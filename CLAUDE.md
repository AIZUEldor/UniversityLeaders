# CLAUDE.md — University Leaders Platform

## Stack

- Frontend: Next.js with TypeScript
- Backend: ASP.NET Core on .NET 10, Clean Architecture
- Database: PostgreSQL
- File storage: PostgreSQL (`attachment_contents`, bytea; no object store)
- Real-time notifications: SignalR

## Architecture rules

- Request flow is strictly: **Controller -> Service -> Repository -> Database**.
- Controllers are thin: receive the request, call one service method, return the result.
- No business logic in controllers.
- No try-catch in controllers; errors are handled centrally (middleware / exception handler).
- Use explicit C# types (no `var` where the type is not obvious, explicit DTOs and return types).
- Services hold business logic; repositories hold data access only.
- The architecture checkpoint lives in `docs/ARCHITECTURE_CHECKPOINT.md`. Follow it; do not invent or overwrite it.

## Core requirements

### Roles and fixed counts

| Role | Count | Login |
|------|-------|-------|
| University leader (Admin) | 1 | yes |
| Faculty leaders | 13 | yes |
| University coordinators (7 directions) | 7 | no — public profiles managed by the University Leader |
| Faculty coordinators (7 directions × 13 faculties) | 91 | no — public profiles managed by their faculty leader |

112 seats, only 14 logins. Coordinators never sign in. Only faculty leaders submit reports; only the University Leader
approves them.

### Reports

- Reports follow role-based approval.
- Only **approved** reports appear in completed work.
- Only **approved** reports count toward faculty ranking.

### Announcements

- Admin announcements are delivered to the 13 faculty leaders.

## Engineering standards (permanent)

- **Clean Architecture:** dependencies point inward (API → Application → Domain; Infrastructure implements Application interfaces). Domain has no framework dependencies.
- **SOLID:** single responsibility per class/component; depend on abstractions (interfaces) at layer boundaries; extend by addition, not by editing stable code.
- **DRY:** one source of truth for rules (roles, the 7 directions, approval hierarchy, slot rules). Reuse existing helpers/components before adding new ones.
- **KISS:** the simplest solution that fully meets the requirement; no speculative abstractions or options.
- **Type safety:** strict TypeScript (no `any`, avoid non-null `!` unless justified in a comment); explicit C# types and DTOs; validate all input at trust boundaries.
- **Reusable components:** shared layouts and panels (`PrivateShell`, `ReportsCollection`, `ReportDetailView`, `RankingBoard`, `NotificationsList`) scoped by the logged-in identity — never a separate dashboard per person.
- **UI vs business logic:** pure rules live in `frontend/src/lib` (later: backend services); components only render and call them. Public pages never render private controls.
- **Server-side RBAC and ownership (backend):** every protected endpoint enforces role and ownership on the server (e.g. a faculty leader acts only on their own faculty; coordinators only submit their own reports). Client-side checks are UX only, never security.
- **Accounts:** unique accounts per person, one-time temporary credentials, mandatory password change on first login, passwords hashed server-side only. The frontend never stores passwords.
- **Soft deletion:** deactivate people instead of deleting them; keep identity, reports and audit history.

## Working rules

- Never commit secrets. Real values go in `.env` (git-ignored); `.env.example` holds placeholders only.
- Do not commit, push, install dependencies, or scaffold applications without explicit approval.
