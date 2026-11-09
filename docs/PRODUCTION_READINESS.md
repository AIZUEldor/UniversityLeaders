# Production readiness (Phase 7A audit, updated in Phases 7B-7E, 2026-11-10)

Status of the integrated system before the first deployment. Nothing here is deployed. Items marked **open** are
confirmed gaps; they are documented, not implemented.

## Verified in place

- Cookie session `__Host-ul_session` (HttpOnly, Secure, Path=/), server-side session validation against the database
  (active flag, security stamp, role, forced password change) on every request; logout and password change revoke all sessions.
- CSRF: antiforgery header on every state-changing request, including login; token kept in memory by the frontend.
- CORS: exact https origins only (validated at startup), credentials, fixed methods and headers. Hub `Origin` check
  blocks cross-site WebSocket hijacking.
- Rate limit on login, bootstrap and password change (per client IP). Generic login failures with timing equalisation.
- Passwords: Identity PBKDF2 hashes; temporary passwords from a CSPRNG, returned once, never logged.
- Public endpoints expose display names, approved work and scores only (integration tests assert no user names, ids,
  hashes, review comments, breakdowns or attachments).
- Uploads: 10 MB / 10 files, content sniffing (JPEG, PNG, PDF, real DOCX), zip-bomb limits, random storage keys,
  private bucket, streamed only through the authorized API with `Content-Disposition: attachment` and `nosniff`.
- Errors: central handler; 500s return a generic message, details only in logs.
- Secrets: `.env*` git-ignored, `.env.example` has placeholders only; no credentials in tracked or untracked source.
- Dependencies: `dotnet list package --vulnerable --include-transitive` clean; `npm audit --omit=dev` 0 findings.
- Data Protection key ring persisted in PostgreSQL with a fixed application name and encrypted with an X.509 certificate
  (mandatory in Production); restart, wrong-certificate, rotation and cross-host revocation verified by integration
  tests (Phases 7B, 7C).
- HSTS on the API outside Development; frontend sends `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `nosniff`,
  `Referrer-Policy`.

## Critical (before production)

| # | Issue | Affected | Fix |
|---|---|---|---|
| C1 | **Fixed for production (Phase 7E).** `backend/Dockerfile` pins `aspnet:10.0.14-noble` / `sdk:10.0.403-noble` (.NET 10.0.14, security release 2026-10-25, latest per the official release metadata); Microsoft packages aligned on 10.0.14; image verified locally (runtime 10.0.14, non-root). **Open (local machine only):** installed runtime 10.0.5/10.0.6 and SDK 10.0.202 are outdated; not updated because it is a machine-wide install. | `backend/Dockerfile`, `backend/**/*.csproj` | Locally: `winget upgrade Microsoft.DotNet.SDK.10` (or install SDK 10.0.403 / 10.0.114). Every Patch Tuesday: bump both image tags and the 10.0.x packages, rebuild, redeploy. |
| C2 | **Fixed in code (Phases 7B, 7C); operator action open.** Key ring persisted in PostgreSQL with the fixed application name `UniversityLeaders` and encrypted with an X.509 certificate (`ProtectKeysWithCertificate`; previous certificates via `UnprotectKeysWithAnyCertificate`). Production refuses to start without a valid certificate or while plaintext keys remain. Verified by `DataProtectionPersistenceTests` and `DataProtectionKeyEncryptionTests`: keys encrypted at rest, sessions survive restarts with the right certificate, a wrong certificate cannot read them, rotation keeps sessions, logout and password change still revoke across hosts. **Open (deployment):** provision the certificate and its password in the platform secret store and the vault backup; start production on a fresh database (no development keys). | `backend/src/UniversityLeaders.Infrastructure/DependencyInjection.cs`, `Security/KeyEncryptionCertificates.cs`, `Security/UnencryptedKeyGuard.cs`, `Options/KeyEncryptionOptions.cs`, migration `20261109205101_AddDataProtectionKeys` | Follow backend/README.md "Data Protection keys" (provisioning, backup, rotation, rekey). Restrict database access (dedicated role, private network, `SSL Mode=Require`, encrypted backups). |
| C3 | **Fixed (Phase 7D).** University Leader resets faculty leaders and university coordinators; a faculty leader resets coordinators of their own faculty only; the University Leader is recovered offline with the server-side CLI command `recover-university-leader <userName>` (no HTTP route). Each reset issues a one-time CSPRNG temporary password (hash only), forces a change at next login, revokes all sessions and is audited without the password. Verified by `PasswordResetTests`. **Operator action:** decide who gets shell access to the API host and run the command interactively only. | `Application/Users/UserManagementService.cs`, admin and faculty user controllers, `Api/Security/UniversityLeaderRecoveryCommand.cs`, `frontend/src/components/admin/ManagedUsersPage.tsx` | See backend/README.md "Password recovery". |
| C4 | **New (Phase 7E).** Frontend and API must share a site (custom domains). On two `*.onrender.com` hosts the session cookie is third-party and is blocked by Safari and other browsers. | `render.yaml`, `Auth__SameSite`, `Cors__AllowedOrigins__0`, `NEXT_PUBLIC_API_URL` | Register a domain, add e.g. `leaders.<domain>` and `api.leaders.<domain>` in Render (docs/DEPLOYMENT.md §1). |
| C5 | **New (Phase 7E).** The free demo service `university-leaders-demo` auto-deploys `main` with the old demo build; pushing the integrated code would deploy a frontend without an API. | Render service `srv-dbom3bijnfac73fbdcp0` (not modified) | Disable its auto-deploy or delete it before pushing to `main`. |

## Important

| # | Issue | Affected | Fix |
|---|---|---|---|
| I1 | No per-account lockout; only a per-IP window (5/60 s). A distributed guess against one account is unthrottled. | `Application/Auth/AuthService.cs`, `Domain/Entities/User.cs` | Failed-attempt counter + temporary lockout (e.g. 10 failures → 15 min), reset on success; keep the generic `invalid_credentials` response. |
| I2 | Authentication events are not audited (login success/failure, logout, password change). Management, report and announcement actions are. | `Application/Auth/AuthService.cs`, `Application/Common/Audit.cs` | Add `auth.login_succeeded`, `auth.login_failed` (user name, IP, no password), `auth.logout`, `auth.password_changed`. |
| I3 | No malware scanning of uploads. PDF/DOCX are content-validated but may still carry malicious payloads that reviewers download. | `Application/Attachments/AttachmentService.cs` | Scan before `PutAsync` (ClamAV sidecar or provider scanning) and reject or quarantine; until then, tell reviewers to open files with up-to-date viewers. |
| I4 | Forwarded headers trust any proxy (`KnownIPNetworks`/`KnownProxies` cleared). Acceptable on Render, where the proxy is the only entry point; `render.yaml` sets `ForwardedHeaders__Enabled=true`. | `backend/src/UniversityLeaders.Api/Program.cs`, `render.yaml` | Keep the API reachable only through Render (no other ingress); list proxy networks if that ever changes. |
| I5 | **Fixed (Phase 7E).** Production refuses a non-https `ObjectStorage:ServiceUrl`; signing region configurable (`ObjectStorage:Region`) for AWS S3. **Open:** choose the provider and create the private bucket and a bucket-scoped key. | `Infrastructure/DependencyInjection.cs`, `Storage/S3AttachmentStorage.cs` | docs/DEPLOYMENT.md §2. |
| I6 | **Documented (Phase 7E).** Backup and restore for PostgreSQL, the bucket and the certificate in docs/DEPLOYMENT.md §4. **Open:** paid database plan with backups, a second bucket for the daily copy, one test restore before go-live. | Hosting | docs/DEPLOYMENT.md §4. |
| I7 | `docs/ARCHITECTURE_CHECKPOINT.md` is referenced by `CLAUDE.md` but does not exist. | `docs/` | Add the agreed checkpoint (owner decision; not generated). |

## Optional

| # | Issue | Affected | Fix |
|---|---|---|---|
| O1 | Orphaned storage objects (failed rollback delete) are only logged. | `Application/Attachments/AttachmentService.cs` | Periodic sweep: delete bucket keys without a `report_attachments` row older than 1 h. |
| O2 | Attachment limit is check-then-insert; two concurrent uploads at 9 files can end at 11. | `AttachmentService.UploadAsync` | Lock the report row (`SELECT … FOR UPDATE`) or count under the report's `xmin` version. |
| O3 | Dev-only npm advisory: `minimatch` ReDoS (GHSA-g3hv-75wg-m249) via `eslint-config-next`. Not shipped. | `frontend/package.json` | Update `eslint-config-next` when a fixed release exists. |
| O4 | **Fixed (Phase 7E).** Microsoft packages on 10.0.14 (EF Core Design, Identity.Core, Logging.Abstractions, Mvc.Testing); `dotnet list package --vulnerable` clean. Local `dotnet-ef` tool is 10.0.7 (the image uses 10.0.14). | `backend/**/*.csproj` | `dotnet tool update --global dotnet-ef --version 10.0.14`. |
| O5 | No full Content-Security-Policy on the frontend (only `frame-ancestors`). | `frontend/next.config.ts` | Add a nonce-based CSP after testing Next.js inline scripts. |
| O6 | `AllowedHosts: "*"`. | `backend/src/UniversityLeaders.Api/appsettings.json` | Set the production API host name. |
| O7 | SignalR runs on a single instance (no backplane). | `Program.cs` | Redis backplane or Azure SignalR before scaling out. |
| O8 | No retention policy for notifications, announcements and audit logs. | Database | Decide retention and add an archival job. |
