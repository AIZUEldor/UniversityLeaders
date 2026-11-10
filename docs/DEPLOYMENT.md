# Production deployment (Render)

> The checked-in `render.yaml` is the **zero-cost** variant (section 0). Sections 1-3 describe the paid variant with custom domains.

Prepared in Phase 7E; **nothing has been created or deployed**. Topology: `render.yaml` Blueprint with a private
PostgreSQL 17 database, the API as a Docker web service (`backend/Dockerfile`, .NET 10.0.14), the Next.js frontend as a
Node web service. Attachment files are stored in PostgreSQL (`attachment_contents`), so no object storage is needed.

The existing free service `university-leaders-demo` auto-deploys `main` with `cd frontend && npm ci && npm run build`.
It is independent of this setup and was not changed. **Before pushing the integrated code to `main`, disable its
auto-deploy or delete it**: the current frontend needs `NEXT_PUBLIC_API_URL` and a running API, so the demo would break.

## 0. Zero-cost setup (current `render.yaml`)

| Part | Service (free) | Limits that matter |
|---|---|---|
| Frontend | Render free web service `university-leaders-web` | Sleeps after 15 min idle (~1 min cold start). |
| API | Render free web service `university-leaders-api` (Docker) | Same sleep; no shell, no pre-deploy command. |
| PostgreSQL | Neon free project (region Frankfurt) | 1 GB storage, 100 compute-hours/month, persistent, no card. |
| Files | Stored in the same Neon database | 500 MB quota for all attachments (10 MB per file), leaving room for data. |

- **One origin for the browser:** two `*.onrender.com` hosts are different sites, so the frontend proxies `/api` and
  `/hubs` to the API (`API_PROXY_TARGET`, `frontend/next.config.ts`) and `NEXT_PUBLIC_API_URL` is the frontend's own
  URL. Session and CSRF cookies are then first-party (`SameSite=Lax`). Live notifications may fall back from WebSockets to
  long polling through the proxy; the saved inbox works regardless.
- **Shared IP:** the API sees every request coming from the frontend server, so the login rate limit is shared by all
  users (`RateLimiting__Login__PermitLimit=30` per minute).
- **Free hours:** 750 instance hours per month are shared by **all** free services in the workspace, including
  `fitmirror-backend` and `university-leaders-demo`; when they run out, every free service is suspended until the month
  ends. Watch Dashboard -> Billing -> usage.
- **Migrations (no pre-deploy):** before deploying a version with new migrations, run from a trusted machine with the Neon
  connection string: `dotnet ef database update --project src/UniversityLeaders.Infrastructure --startup-project src/UniversityLeaders.Api`
  (in `backend/`, `ASPNETCORE_ENVIRONMENT=Development`, `Database__ConnectionString=<Neon>`).
- **University Leader recovery (no shell):** run `dotnet run --project src/UniversityLeaders.Api -- recover-university-leader <userName>`
  the same way from the trusted machine; it only needs the database.
- **Data Protection certificate:** use the text `.pem` form (certificate + encrypted private key in one file); Render
  Secret Files are text, so a binary `.pfx` may not survive the upload. Generate it as in backend/README.md, e.g.
  `openssl req -x509 -newkey rsa:3072 -sha256 -days 1095 -subj "/CN=UniversityLeaders Data Protection" -keyout key.pem -out cert.pem -passout file:password.txt`
  then concatenate `cert.pem` + `key.pem` into `dataprotection.pem`. The password file must not end with a newline or `\r`
  (OpenSSL keeps a trailing `\r` as part of the password; .NET and the Render dashboard do not).
- **Backups:** Neon free keeps a short restore window only; take a weekly `pg_dump` (section 4); it includes the files.
- Upgrade path: `plan: starter`, `preDeployCommand: /app/efbundle`, custom domains (section 1) and remove the proxy.

## 1. Domains (required) (paid setup)

Frontend and API must be on the **same site**, e.g. `leaders.example.uz` (frontend) and `api.leaders.example.uz` (API).
Two `*.onrender.com` hosts are different sites (public suffix): the session cookie would then be a third-party cookie
(`SameSite=None`), which Safari and other browsers block. With a shared site the API keeps `Auth__SameSite=Lax`.
Add both custom domains in Render; Render issues and renews the TLS certificates and terminates TLS at its proxy, which
is why `ForwardedHeaders__Enabled=true` is set (the proxy is the only entry point).

## 2. One-time secrets (operator, never committed)

| What | Where |
|---|---|
| Data Protection certificate `dataprotection.pfx` (backend/README.md "Data Protection keys" → Provisioning) | API → Secret Files → `/etc/secrets/dataprotection.pfx`; password in `DataProtection__Certificate__Password`; copy + password in the vault |
| Database connection string | `Database__ConnectionString` = `Host=<internal hostname>;Port=5432;Database=university_leaders;Username=ul_api;Password=<from Render>;SSL Mode=Require` (Npgsql key-value format, not the `postgres://` URL; if the internal endpoint rejects TLS, use `SSL Mode=Prefer` on the internal network only) |
| Frontend origin | `Cors__AllowedOrigins__0=https://leaders.example.uz` |
| API URL for the frontend build | `NEXT_PUBLIC_API_URL=https://api.leaders.example.uz` (build time; redeploy the frontend after changing it) |

Production refuses to start, with a message naming the setting, when the database connection string, the certificate
or its password are missing or invalid, or when plaintext Data Protection keys are
in the database. Nothing falls back to insecure defaults.

## 3. First deployment

1. Render → Blueprints → New → this repository (review the plan and costs it shows). Enter every `sync: false` value and
   the secret file before the first deploy.
2. Deploy the API. The pre-deploy command `/app/efbundle` applies all EF Core migrations to the empty database before the
   app starts. Manual alternative from the API shell: `/app/efbundle`.
3. Check `https://api.leaders.example.uz/health` → `Healthy`.
4. Bootstrap the University Leader (once):
   - Generate a token on your machine: `python -c "import secrets;print(secrets.token_hex(32))"`; set it as
     `Bootstrap__Token` and redeploy the API.
   - `curl -X POST https://api.leaders.example.uz/api/auth/bootstrap -H "Content-Type: application/json" -H "X-Bootstrap-Token: <token>" -d '{"userName":"<login>","fullName":"<full name>","password":"<temporary, 12+ chars>"}'`
     (run it from a terminal whose history you clear afterwards).
   - **Delete `Bootstrap__Token` and redeploy.** The endpoint then answers 404; it answers 409 once a leader exists anyway.
   - Log in at the frontend and set the permanent password (forced on first login).
5. Deploy the frontend, then verify login, a report upload and the live notification badge.

Later deployments: manual deploy of the API (migrations run first), then the frontend if it changed.

## 4. Backup and restore

| Data | Backup | Restore |
|---|---|---|
| PostgreSQL | Render's daily backups on paid plans (point-in-time recovery on higher plans). Before risky changes also take a manual dump from a trusted machine: `pg_dump --format=custom --no-owner "<external URL>" -f ul-YYYYMMDD.dump` (temporarily allow your IP in the database's access list, then remove it). Store dumps encrypted, operator-only. | Restore into a **new** database (Render's restore, or `pg_restore --no-owner --dbname "<new DB URL>" ul-YYYYMMDD.dump`), point `Database__ConnectionString` at it, redeploy. |
| Data Protection certificate | `.pfx` and password in the organisation's vault, separately from database backups (backend/README.md). | Re-upload as the secret file and set the password. Without it every session is signed out (no data loss). |

The database backup contains the encrypted Data Protection keys, password hashes and reports: treat it as secret. Test a
restore into a scratch database before going live.

## 5. Exposure checks (verified locally in Phase 7E)

- API image: `aspnet:10.0.14-noble`, runs as the non-root `app` user, `ASPNETCORE_ENVIRONMENT=Production`, contains only
  `appsettings.json` (no development settings, `.env`, certificates or keys; see `backend/.dockerignore`).
- Without secrets the container stops at startup with `DataProtection:Certificate:Path is required in Production`.
- No database port is published by the app; files are stored in PostgreSQL and streamed only through the authorized API. `infra/docker-compose.yml` is for local development only and
  binds to 127.0.0.1.
- No developer exception pages, Swagger or debug endpoints exist; 500 responses are generic.
- HSTS on the API (Production) and on the frontend; `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy`.
