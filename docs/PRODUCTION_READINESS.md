# Production readiness (Phase 7A audit, updated in Phases 7B-8A, 2026-11-10)

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
  stored in PostgreSQL (`attachment_contents`, 500 MB total quota), streamed only through the authorized API with `Content-Disposition: attachment` and `nosniff`.
- Errors: central handler; 500s return a generic message, details only in logs.
- Secrets: `.env*` git-ignored, `.env.example` has placeholders only; no credentials in tracked or untracked source.
- Dependencies: `dotnet list package --vulnerable --include-transitive` clean; `npm audit --omit=dev` 0 findings.
- Data Protection key ring persisted in PostgreSQL with a fixed application name and encrypted with an X.509 certificate
  (mandatory in Production); restart, wrong-certificate, rotation and cross-host revocation verified by integration
  tests (Phases 7B, 7C).
- Per-account login lockout (10 wrong passwords -> 15 min, generic `invalid_credentials`, lifted by a password reset)
  and audit records for login success, lockout, logout and password change (Phase 8A, `LoginLockoutTests`).
- End-to-end browser check (Phase 8A) in the zero-cost shape, the browser talking only to the frontend which
  proxies `/api` and `/hubs`: bootstrap login and forced password change, faculty and faculty leader provisioning,
  report draft, PDF upload, submit, live "new report" notification to the University Leader over the proxied
  WebSocket, reviewer download of the original bytes, approval, live approval badge, announcement delivery, read
  and read status, public home, profile, completed work and ranking with the private review comment absent from
  pages and the public API, security headers present. Only expected console messages (signed-out 401 on `/me`).
- HSTS on the API outside Development; frontend sends `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `nosniff`,
  `Referrer-Policy`.

## Critical (before production)

| # | Issue | Affected | Fix |
|---|---|---|---|
| C1 | **Fixed for production (Phase 7E).** `backend/Dockerfile` pins `aspnet:10.0.14-noble` / `sdk:10.0.403-noble` (.NET 10.0.14, security release 2026-10-25, latest per the official release metadata); Microsoft packages aligned on 10.0.14; image verified locally (runtime 10.0.14, non-root). **Open (local machine only):** installed runtime 10.0.5/10.0.6 and SDK 10.0.202 are outdated; not updated because it is a machine-wide install. | `backend/Dockerfile`, `backend/**/*.csproj` | Locally: `winget upgrade Microsoft.DotNet.SDK.10` (or install SDK 10.0.403 / 10.0.114). Every Patch Tuesday: bump both image tags and the 10.0.x packages, rebuild, redeploy. |
| C2 | **Fixed in code (Phases 7B, 7C); certificate generated (Phase 8A).** Key ring persisted in PostgreSQL and encrypted with an X.509 certificate; Production refuses to start without it or with plaintext keys (`DataProtectionPersistenceTests`, `DataProtectionKeyEncryptionTests`). The production certificate (`dataprotection.pem`, RSA 3072, encrypted key, valid until 2029-11-09, verified loadable by .NET) and its password file are in `C:\Users\Acer\UniversityLeaders-secrets\` on the operator machine, outside the repository. **Open (operator):** upload it as the Render Secret File `dataprotection.pem`, set `DataProtection__Certificate__Password`, store both in the password manager, then delete the local copy. | `Infrastructure/DependencyInjection.cs`, `Security/KeyEncryptionCertificates.cs`, `Security/UnencryptedKeyGuard.cs`, `render.yaml` | docs/DEPLOYMENT.md §0 and backend/README.md "Data Protection keys". |
| C3 | **Fixed (Phase 7D).** University Leader resets faculty leaders and university coordinators; a faculty leader resets coordinators of their own faculty only; the University Leader is recovered offline with the server-side CLI command `recover-university-leader <userName>` (no HTTP route). Each reset issues a one-time CSPRNG temporary password (hash only), forces a change at next login, revokes all sessions and is audited without the password. Verified by `PasswordResetTests`. **Operator action:** decide who gets shell access to the API host and run the command interactively only. | `Application/Users/UserManagementService.cs`, admin and faculty user controllers, `Api/Security/UniversityLeaderRecoveryCommand.cs`, `frontend/src/components/admin/ManagedUsersPage.tsx` | See backend/README.md "Password recovery". |
| C4 | **Fixed for the zero-cost setup (Phase 7F, verified Phase 8A).** The frontend proxies `/api` and `/hubs` to the API (`API_PROXY_TARGET`), so the browser sees one origin and cookies are first-party on free `*.onrender.com` subdomains; verified end to end, including the WebSocket. Custom domains remain the upgrade path. | `frontend/next.config.ts`, `render.yaml` | — |
| C5 | **Rule, not a code issue.** The free demo `university-leaders-demo` auto-deploys `main`; production lives on the `production` branch. Never merge `production` into `main` while the demo exists. | Render service `srv-dbom3bijnfac73fbdcp0` (not modified) | Disable or delete the demo before any merge into `main`. |

## Important

| # | Issue | Affected | Fix |
|---|---|---|---|
| I1 | **Fixed (Phase 8A).** Per-account lockout: 10 consecutive wrong passwords lock the account for 15 minutes; the response stays the generic `invalid_credentials`; a password reset lifts it. | `Application/Auth/AuthService.cs`, `Domain/Entities/User.cs`, migration `AddLoginLockout` | — |
| I2 | **Fixed (Phase 8A).** Audited: `auth.login_succeeded`, `auth.locked_out`, `auth.logout`, `auth.password_changed` (no passwords). Single failed attempts only increase the account counter, so guessing cannot flood the 1 GB database. Client IPs are not recorded (behind the frontend proxy every request shows the proxy's IP). | `Application/Auth/AuthService.cs`, `Application/Common/Audit.cs` | — |
| I3 | **Accepted risk for the free tier (decision needed).** No malware scanner: a ClamAV sidecar needs more memory than free instances have. Mitigations in place: only JPEG/PNG/PDF/DOCX by content, PDFs with JavaScript/launch/embedded files and DOCX with macros/OLE/remote templates rejected, download-only serving with `nosniff`, sandbox CSP and `attachment` disposition, access limited to author and responsible reviewer. | `Application/Attachments/AttachmentInspector.cs` | Add a scanner when moving to a paid plan; reviewers open files with up-to-date viewers. |
| I4 | Forwarded headers trust any proxy (`KnownIPNetworks`/`KnownProxies` cleared). Acceptable on Render, where the proxy is the only entry point; `render.yaml` sets `ForwardedHeaders__Enabled=true`. | `backend/src/UniversityLeaders.Api/Program.cs`, `render.yaml` | Keep the API reachable only through Render (no other ingress); list proxy networks if that ever changes. |
| I5 | **Obsolete (Phase 7F).** No object storage: attachment bytes are stored in PostgreSQL. | — | — |
| I6 | **Documented (Phase 7E).** Backup and restore for PostgreSQL (including files) and the certificate in docs/DEPLOYMENT.md §4. **Open:** paid database plan with backups, a second bucket for the daily copy, one test restore before go-live. | Hosting | docs/DEPLOYMENT.md §4. |
| I7 | `docs/ARCHITECTURE_CHECKPOINT.md` is referenced by `CLAUDE.md` but does not exist. | `docs/` | Add the agreed checkpoint (owner decision; not generated). |

## Optional

| # | Issue | Affected | Fix |
|---|---|---|---|
| O1 | Orphaned attachment bytes (failed rollback delete) are only logged. | `Application/Attachments/AttachmentService.cs` | Periodic job deleting `attachment_contents` rows without a `report_attachments.storage_key`. |
| O2 | Attachment limit is check-then-insert; two concurrent uploads at 9 files can end at 11. | `AttachmentService.UploadAsync` | Lock the report row (`SELECT … FOR UPDATE`) or count under the report's `xmin` version. |
| O3 | Dev-only npm advisory: `minimatch` ReDoS (GHSA-g3hv-75wg-m249) via `eslint-config-next`. Not shipped. | `frontend/package.json` | Update `eslint-config-next` when a fixed release exists. |
| O4 | **Fixed (Phase 7E).** Microsoft packages on 10.0.14 (EF Core Design, Identity.Core, Logging.Abstractions, Mvc.Testing); `dotnet list package --vulnerable` clean. Local `dotnet-ef` tool is 10.0.7 (the image uses 10.0.14). | `backend/**/*.csproj` | `dotnet tool update --global dotnet-ef --version 10.0.14`. |
| O5 | No full Content-Security-Policy on the frontend (only `frame-ancestors`). | `frontend/next.config.ts` | Add a nonce-based CSP after testing Next.js inline scripts. |
| O6 | `AllowedHosts: "*"`. | `backend/src/UniversityLeaders.Api/appsettings.json` | Set the production API host name. |
| O7 | SignalR runs on a single instance (no backplane). | `Program.cs` | Redis backplane or Azure SignalR before scaling out. |
| O8 | No retention policy for notifications, announcements and audit logs. | Database | Decide retention and add an archival job. |
