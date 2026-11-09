# CLAUDE.md — University Leaders Platform

## Stack

- Frontend: Next.js with TypeScript
- Backend: ASP.NET Core on .NET 10, Clean Architecture
- Database: PostgreSQL
- File storage: MinIO
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

| Role | Count |
|------|-------|
| University leader | 1 |
| Faculty leaders | 13 |
| University coordinators | 7 |
| Faculty coordinators | 91 |

### Reports

- Reports follow role-based approval.
- Only **approved** reports appear in completed work.
- Only **approved** reports count toward faculty ranking.

### Announcements

- Admin announcements are delivered to the 13 faculty leaders.

## Working rules

- Never commit secrets. Real values go in `.env` (git-ignored); `.env.example` holds placeholders only.
- Do not commit, push, install dependencies, or scaffold applications without explicit approval.
