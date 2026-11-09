# University Leaders Platform

Platform for managing university and faculty leadership work: role-based report submission and approval, completed-work tracking, faculty ranking, and admin announcements.

## Stack

- **Frontend:** Next.js (TypeScript)
- **Backend:** ASP.NET Core (.NET 10), Clean Architecture
- **Database:** PostgreSQL
- **File storage:** PostgreSQL (attachment bytes in `attachment_contents`)
- **Real-time:** SignalR

## Status

Frontend (`frontend/`) and backend (`backend/`) are integrated; production deployment is prepared but not performed: see `docs/DEPLOYMENT.md` and `docs/PRODUCTION_READINESS.md`.

## Setup

1. Copy `.env.example` to `.env` and fill in real values. Never commit `.env`.
2. Further setup steps will be added once the backend and frontend are scaffolded.

## Docs

- `CLAUDE.md` — project rules and requirements for AI-assisted development
- `docs/ARCHITECTURE_CHECKPOINT.md` — architecture checkpoint (added separately)
