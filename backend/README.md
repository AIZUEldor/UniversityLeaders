# UniversityLeaders backend

ASP.NET Core (.NET 10) API, Clean Architecture.

- **Phase 1:** foundation (layers, DI, error handling, typed config, `/health`).
- **Phase 2:** PostgreSQL schema (entities, configurations, constraints, initial migration, direction seed).
- **Phase 3:** local PostgreSQL 17 via Docker Compose, snake_case schema, migration applied and verified.
- **Phase 3A:** cookie authentication, University Leader bootstrap, login/logout/me/change-password, integration tests.
- **Phase 3B:** University Leader management of faculties, faculty leaders and university coordinators, with audit logging.
- **Phase 3C:** faculty leaders manage the faculty coordinators of their own faculty.
- **Phase 4A:** report workflow (draft → submitted → approved/rejected → resubmitted), review queue, public completed work and faculty ranking.
- **Phase 4B:** report attachments, content-validated uploads, authorized streaming downloads (files in PostgreSQL since Phase 7F).
- **Phase 5:** announcements (University Leader → faculty leaders) with read tracking, persistent report notifications, SignalR live hints.
- **Phase 7B:** Data Protection key ring persisted in PostgreSQL (sessions and CSRF tokens survive restarts and deployments).
- **Phase 7C:** key ring encrypted with an X.509 certificate; mandatory in Production.
- **Phase 7D:** password recovery: administrative resets and offline University Leader recovery.

The frontend now integrates authentication, management, private report workflows, attachments, public faculty profiles, completed work, rankings, announcements and notifications (no demo data on integrated pages).

## Structure

```
backend/
  UniversityLeaders.slnx
  src/
    UniversityLeaders.Domain          entities, value objects, domain rules (no dependencies)
    UniversityLeaders.Application     services (business logic), interfaces, DTOs      -> Domain
    UniversityLeaders.Infrastructure  EF Core/PostgreSQL, repositories, external services -> Application, Domain
    UniversityLeaders.Api             thin controllers, middleware, composition root      -> Application, Infrastructure
```

Request flow: **Controller → Service → Repository → Database**. Each layer registers its own services
(`AddApplication()`, `AddInfrastructure(configuration)`); `Program.cs` only composes them.

Errors are handled centrally by `GlobalExceptionHandler` (RFC 7807 problem details, no internals leaked).
Controllers do not use try-catch.

## Prerequisites

- .NET SDK **10.0.202** (pinned in the repo-root `global.json`)
- Docker Desktop (runs PostgreSQL 17 from `infra/docker-compose.yml`; its MinIO service is no longer used by the API)
- `dotnet-ef` 10.x for migrations (see [Migrations](#migrations))

## Local services (PostgreSQL + MinIO)

Credentials live only in the repo-root `.env` (git-ignored). Start from the template and replace every `change_me` value:

```bash
cp .env.example .env        # set POSTGRES_*, Database__ConnectionString, MINIO_* and ObjectStorage__* (secrets must match)
docker compose --env-file .env -f infra/docker-compose.yml up -d
docker compose --env-file .env -f infra/docker-compose.yml ps     # postgres/minio "(healthy)", minio-init "Exited (0)"
```

- **PostgreSQL:** `postgres:17`, volume `university-leaders_postgres-data`, `127.0.0.1:5433` (5433 avoids a native PostgreSQL on 5432).
- **MinIO:** `quay.io/minio/minio:RELEASE.2024-08-17T01-24-54Z`, volume `university-leaders_minio-data`, S3 API `127.0.0.1:9010`,
  console `127.0.0.1:9011` (9010/9011 avoid other local MinIO instances on 9000/9001).
- **minio-init** (one-shot, idempotent) creates the bucket `MINIO_BUCKET` with anonymous access **disabled**, and the API account
  `MINIO_APP_ACCESS_KEY` whose policy allows only get/put/delete objects and listing in that bucket. The root account
  (`MINIO_ROOT_*`) is for administration only; the API never uses it.
- Stop: `docker compose --env-file .env -f infra/docker-compose.yml down` (keeps data). `down -v` **deletes** both volumes.

ASP.NET does not read `.env` itself. Load the backend settings into the shell before running `dotnet ef`, the API or the tests:

```bash
# bash (from repo root)
set -a; eval "$(grep -E '^(Database__)[A-Za-z_]*=' .env | sed -E 's/^([^=]+)=(.*)$/\1="\2"/')"; set +a
```

```powershell
# PowerShell (from repo root)
Get-Content .env | Where-Object { $_ -match '^(Database__)\w*=' } | ForEach-Object { $k, $v = $_ -split '=', 2; Set-Item "env:$k" $v }
```

## Configuration

Strongly typed options, validated at startup (the app refuses to start if a required value is missing):

| Setting | Env var | Notes |
|---|---|---|
| `Database:ConnectionString` | `Database__ConnectionString` | Required. `appsettings.Development.json` points at the local container (port 5433) **without** a password; supply the full string via the env var. |
| `DataProtection:Certificate:*` | `DataProtection__Certificate__Path`, `__Password` | Required in Production (see "Data Protection keys"). |

Never put real credentials in `appsettings*.json`; use the environment variable (from `.env`) or user secrets.

## Run

```bash
cd backend
dotnet restore UniversityLeaders.slnx
dotnet build UniversityLeaders.slnx
dotnet run --project src/UniversityLeaders.Api
```

The default `https` launch profile listens on `https://localhost:5001` (matches `NEXT_PUBLIC_API_URL`) and `http://localhost:5000`.
`/api/*` requires HTTPS (session and CSRF cookies are `Secure`); only `/health` answers over plain http.
Trust the development certificate once: `dotnet dev-certs https --trust`.

```bash
curl -k https://localhost:5001/health   # -> 200 Healthy
```

## Authentication (Phase 3A)

Cookie-based sessions for a browser frontend. Every error is RFC 7807 problem details with a stable `code`.

| Method | Path | Access | Purpose |
|---|---|---|---|
| GET | `/api/auth/csrf` | anyone | Returns `{ csrfToken }` for the current identity |
| POST | `/api/auth/bootstrap` | `X-Bootstrap-Token` header | One-time creation of the University Leader (temporary password) |
| POST | `/api/auth/login` | anyone, rate limited | `{ userName, password }` → session cookie + `{ user, csrfToken }` |
| GET | `/api/auth/me` | signed in (also while a password change is pending) | Current account (no hash, no stamp) |
| POST | `/api/auth/change-password` | signed in (also while pending), rate limited | `{ currentPassword, newPassword }` → new cookie + `{ user, csrfToken }` |
| POST | `/api/auth/logout` | signed in (also while pending) | Ends the session (204) |

**Frontend flow:** `GET /api/auth/csrf` → `POST /api/auth/login` with header `X-CSRF-TOKEN` → use the `csrfToken` from the
login/change-password response for later POSTs (CSRF tokens are bound to the identity, so the anonymous one stops working
after login). Send requests with `credentials: "include"`.

### Bootstrapping the University Leader

1. Generate a token (≥ 32 chars), e.g. `python -c "import secrets;print(secrets.token_hex(32))"`, and set `Bootstrap__Token` for the API process only.
2. `POST /api/auth/bootstrap` with header `X-Bootstrap-Token: <token>` and body `{ "userName", "fullName", "password" }`.
   The password is temporary: the account starts with `mustChangePassword = true`.
3. Remove `Bootstrap__Token` and restart. Without a token the endpoint answers 404; once an active University Leader exists it answers 409 `bootstrap_disabled` regardless of the token.

### Security decisions

- **Passwords:** ASP.NET Core Identity `PasswordHasher` (PBKDF2-HMAC-SHA512, salted, versioned; old hashes are upgraded on login). 12–128 characters, must not contain the user name, must differ from the current one. Hashes, temporary passwords and security stamps never appear in responses.
- **User names:** `citext`, unique regardless of case; stored lower-case; letters, digits, `.`, `_`, `-` (3–64).
- **Generic failures:** unknown user, wrong password and inactive account all return 401 `invalid_credentials`; a dummy hash is verified for unknown users so timing does not reveal valid names.
- **Session cookie** `__Host-ul_session`: `HttpOnly`, `Secure`, `Path=/`, no `Domain`, `SameSite` from `Auth:SameSite` (default `Lax`; use `None` when frontend and API are on different sites such as two `*.onrender.com` hosts), 8-hour sliding expiry (`Auth:SessionMinutes`).
- **Server-side session checks:** on every request the account is re-loaded; inactive, missing or stamp-mismatched accounts are signed out. Role and `mustChangePassword` are taken from the database, not the cookie.
- **Revocation:** changing the password and logging out rotate the security stamp, which ends **all** sessions of that account (logout is "log out everywhere").
- **CSRF:** ASP.NET antiforgery (HTTP-only `__Host-ul_csrf` cookie + `X-CSRF-TOKEN` header) on every POST/PUT/PATCH/DELETE, including login. Bootstrap is exempt (no ambient credentials; it has its own header token).
- **CORS:** only the exact origins in `Cors:AllowedOrigins` (https, or loopback for development), with credentials, methods GET/POST/PUT/DELETE and headers `Content-Type`, `X-CSRF-TOKEN`. Empty = no cross-origin access.
- **Account lockout (Phase 8A):** 10 consecutive wrong passwords lock the account for 15 minutes (`users.failed_login_count`,
  `users.lockout_ends_at`). While locked even the right password gets the generic 401 `invalid_credentials`; a successful
  login resets the counter and a password reset lifts the lockout. Audit: `auth.login_succeeded`, `auth.locked_out`,
  `auth.logout`, `auth.password_changed`; single failures are counted, not audited.
- **Rate limiting:** login, bootstrap and change-password share a fixed window per client IP (`RateLimiting:Login`, default 5 per 60 s) → 429 with `Retry-After`.
- **Forced password change:** the default and fallback authorization policy requires a signed-in user without a pending password change, so every endpoint (present and future) is locked until the change, except `me`, `change-password` and `logout`. Pending users get 403 `password_change_required`.
- **Roles:** policy `UniversityLeader` = signed in + role + password changed. Future admin endpoints use `[Authorize(Policy = AuthPolicies.UniversityLeader)]`.
- **Concurrency:** `users` has an `xmin` concurrency token; concurrent account updates (e.g. two password changes) return 409 `concurrent_update` instead of overwriting. Concurrent bootstraps are stopped by the unique index (409).
- **Reverse proxy:** set `ForwardedHeaders__Enabled=true` on Render so the client IP (rate limiting) and https scheme come from the proxy. Only enable it when the proxy is the sole entry point.

### Data Protection keys (Phase 7B, 7C)

Session and antiforgery cookies are encrypted with the ASP.NET Core Data Protection key ring. It is stored in the
`data_protection_keys` table (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, migration `AddDataProtectionKeys`)
with the fixed application name `UniversityLeaders` (`DependencyInjection.DataProtectionApplicationName`), so a restarted,
redeployed or second API instance on the same database reads existing cookies. Without the fixed name the key ring would be
isolated per content-root path. Covered by `DataProtectionPersistenceTests` and `DataProtectionKeyEncryptionTests`.

**Key encryption (Phase 7C).** With `DataProtection:Certificate:Path` set, every new key is encrypted with that X.509
certificate (`ProtectKeysWithCertificate`); the `xml` column then holds `<encryptedSecret>` instead of `<masterKey>`.

| Setting | Env var | Notes |
|---|---|---|
| `DataProtection:Certificate:Path` | `DataProtection__Certificate__Path` | Mounted secret file: PKCS#12 (`.pfx`/`.p12`) or `.pem` with certificate **and** RSA private key. **Required in Production.** |
| `DataProtection:Certificate:Password` | `DataProtection__Certificate__Password` | PFX password or encrypted-PEM password; omit if none. |
| `DataProtection:PreviousCertificates:N:Path` / `:Password` | `DataProtection__PreviousCertificates__0__Path`, ... | Retired certificates, used only to decrypt keys they encrypted. |

- **Production startup fails** (no plaintext fallback) when the path is missing, the file is absent or unreadable, the
  password is wrong, the certificate has no RSA private key, or `data_protection_keys` still contains an unencrypted key
  (`UnencryptedKeyGuard`). Messages name the setting and file, never the password. Development and Testing run without a
  certificate (plaintext keys; Data Protection logs "No XML encryptor configured").
- **Expiry is not enforced:** Data Protection uses the key pair, not the certificate's validity period, so an expired
  certificate keeps working. Rotate on a schedule anyway (below) and monitor the expiry date.
- **Secret material:** no API reads the table and EF Core logs commands without parameter values. With encryption on, a
  database or backup leak alone no longer exposes the keys; the certificate's private key is the secret to protect.

**Provisioning (operator, outside the repository).** Create a dedicated RSA certificate used only for this purpose
(self-signed is fine; no CA trust is involved), e.g.
`openssl req -x509 -newkey rsa:3072 -sha256 -days 1095 -nodes -subj "/CN=UniversityLeaders Data Protection" -keyout dp.key -out dp.crt`
then `openssl pkcs12 -export -inkey dp.key -in dp.crt -out dataprotection.pfx`. Upload the `.pfx` as a secret file (on
Render: *Secret Files*, mounted under `/etc/secrets/`), put its password in a secret environment variable, and delete the
local copies once they are in the secret store and in the backup below. `.pfx`, `.p12`, `.pem` and `.key` files are
git-ignored; never commit them.

**Backup.** Keep the `.pfx` and its password in the organisation's password manager or vault, separately from database
backups. Losing the certificate makes every stored key unreadable: all users are signed out (no data is lost; these keys
protect sessions and CSRF tokens only). A database backup restored without the matching certificate behaves the same way.

**Rotation.** 1) Provision a new certificate. 2) Set it as `DataProtection:Certificate`, move the old one to
`DataProtection:PreviousCertificates:0`, deploy: new keys use the new certificate, existing keys and sessions still
decrypt. 3) After at least one key lifetime (default 90 days) plus the session lifetime, remove the previous certificate.
Data keys themselves rotate automatically every 90 days; old keys stay in the table to decrypt older cookies.

**Wrong certificate.** A host started with a certificate that cannot decrypt the existing keys cannot read any session:
everyone is signed out, and it writes a new key encrypted with that certificate. Fix the configuration and redeploy; keys
the correct certificate cannot read are ignored (and can be revoked).

**Migrating existing plaintext keys (rekey).** Data Protection never re-encrypts keys written unencrypted, and Production
refuses to start while any remain. Before going live, with no real users yet:
1. Use a fresh production database; never copy `data_protection_keys` from development.
2. If plaintext keys exist (`SELECT count(*) FROM data_protection_keys WHERE xml LIKE '%<masterKey%';`), stop the API,
   delete them (`DELETE FROM data_protection_keys WHERE xml LIKE '%<masterKey%';`), configure the certificate and start
   the API: it creates a new encrypted key. Everyone is signed out once; nothing else is affected.
3. Once real users exist, do not delete keys by hand; after a suspected key leak revoke the key ring
   (`IKeyManager.RevokeAllKeys`) or rotate every user's security stamp.

**Database access.** Even when encrypted, restrict the table: a dedicated API database role, no public network access, TLS
(`SSL Mode=Require`), encrypted volumes and backups with operator-only access.

## University Leader management (Phase 3B)

All routes require policy `UniversityLeader` (signed in, role UniversityLeader, password already changed); every other
caller gets 401/403 from the server. POST/PUT/DELETE need `X-CSRF-TOKEN`.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/admin/faculties` | List faculties (`id`, `name`, `version`) |
| GET | `/api/admin/faculties/{id}` | One faculty |
| POST | `/api/admin/faculties` | `{ name }` — at most 13; names unique case-insensitively |
| PUT | `/api/admin/faculties/{id}` | `{ name, version }` — rename |
| DELETE | `/api/admin/faculties/{id}` | Only when no user or report (active or historical) references it, else 409 `faculty_in_use` |
| GET | `/api/admin/faculty-leaders?includeInactive=false` | List (deactivated only on request) |
| GET | `/api/admin/faculty-leaders/{id}` | One account |
| POST | `/api/admin/faculty-leaders` | `{ userName, fullName, facultyId }` → 201 `{ user, temporaryPassword }` |
| PUT | `/api/admin/faculty-leaders/{id}` | `{ fullName, facultyId, version }` — rename or move to another faculty |
| POST | `/api/admin/faculty-leaders/{id}/deactivate` | `{ version }` — soft deactivation |
| POST | `/api/admin/faculty-leaders/{id}/reset-password` | `{ version }` → `{ user, temporaryPassword }` (see Password recovery) |
| GET / POST / PUT | `/api/admin/university-coordinators[...]` | Same shape with `directionId` (1–7) instead of `facultyId` |
| POST | `/api/admin/university-coordinators/{id}/deactivate` | `{ version }` |
| POST | `/api/admin/university-coordinators/{id}/reset-password` | `{ version }` → `{ user, temporaryPassword }` |

- **Temporary passwords:** 20 characters from a CSPRNG (`RandomNumberGenerator`), no look-alike characters, never containing the user name. Only the PBKDF2 hash is stored; `mustChangePassword = true`. The plain value appears **only** in the 201 provisioning response (sent with `Cache-Control: no-store`), never in list/detail responses, logs or `audit_logs`. If it is lost, the account must be deactivated and re-provisioned (no reset endpoint yet).
- **First login:** the new user signs in with the temporary password and must call `POST /api/auth/change-password` before anything else works.
- **Seats:** one active faculty leader per faculty and one active university coordinator per direction — checked by the service (clear 409) and enforced by the partial unique indexes (race-safe; violations map to the same codes).
- **Deactivation:** sets `is_active = false` + `deactivated_at`, rotates the security stamp (open sessions end immediately), frees the seat. The row, its reports, reviews and audit entries stay. Deactivated accounts cannot log in and cannot be edited.
- **Concurrency:** every record carries `version` (PostgreSQL `xmin`). Updates and deactivations must send the version from the latest read; a stale version returns 409 `concurrent_update`, as does a race between two simultaneous writes.
- **Audit (`audit_logs`):** `user.bootstrapped`, `user.created`, `user.updated` (incl. faculty/direction moves), `user.deactivated`, `faculty.created`, `faculty.renamed`, `faculty.deleted` — actor, entity and JSON details (never passwords, hashes or stamps), written in the same transaction as the change.
- **Error codes:** `validation_failed` (400, with per-field `errors`), `username_taken`, `faculty_leader_exists`, `direction_coordinator_exists`, `faculty_name_taken`, `faculty_limit_reached`, `faculty_in_use`, `already_inactive`, `user_inactive`, `concurrent_update` (409), `user_not_found` / `faculty_not_found` (404).

## Password recovery (Phase 7D)

There is no e-mail/SMS channel and no unauthenticated reset endpoint. Recovery is always performed by someone who is
already trusted for that account:

| Account | Who resets it | How |
|---|---|---|
| Faculty leader, university coordinator | University Leader | `POST /api/admin/{faculty-leaders\|university-coordinators}/{id}/reset-password` ("Parolni tiklash" in the admin pages) |
| Faculty coordinator | Faculty leader of the **same** faculty | `POST /api/faculty/coordinators/{id}/reset-password` (other faculties: 404 `user_not_found`) |
| University Leader | Operator with shell access to the API host | Offline CLI command (below) |

Every reset generates a new CSPRNG temporary password (same generator and policy as provisioning), stores only its hash,
sets `mustChangePassword = true` and rotates the security stamp, so **all existing sessions of that account end
immediately**. The temporary password is returned once in the response and never logged, audited or retrievable later.
Rules: `version` is required (stale → 409 `concurrent_update`), deactivated accounts → 409 `user_inactive`, wrong role → 403.
Audit: `user.password_reset` (actor = the resetting user) and `user.password_recovered` (actor `null`), with user name and
role only.

**University Leader offline recovery.** Run inside the API's deployment (same configuration, database credentials and
key-encryption certificate), for example from the Render shell of the API service:

```bash
dotnet UniversityLeaders.Api.dll recover-university-leader <userName>     # published app
dotnet run --project src/UniversityLeaders.Api -- recover-university-leader <userName>   # local
```

It prints the temporary password to that terminal only and exits (0 success, 1 no active University Leader with that
user name, 2 usage). The web server is not started and there is no HTTP route, so the mechanism is only available to the
operators who are explicitly given shell access to the API host (provision that access deliberately, e.g. to two named
people). Run it **interactively**, never as a scheduled job or deploy hook, because job output may be stored in platform
logs. Then hand the password to the University Leader in person, who must change it at the first login.

## Faculty coordinator management (Phase 3C)

Policy `FacultyLeader` (signed in, role FacultyLeader, password changed). Requests never carry a `facultyId`: the service
reads the caller's faculty from **their own database record** on every request, so a forged value has no effect and a
leader who is moved to another faculty immediately manages that faculty instead.

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/faculty/coordinators?includeInactive=false` | Coordinators of the caller's faculty |
| GET | `/api/faculty/coordinators/{id}` | One coordinator of the caller's faculty |
| POST | `/api/faculty/coordinators` | `{ userName, fullName, directionId }` → 201 `{ user, temporaryPassword }` (`Cache-Control: no-store`) |
| PUT | `/api/faculty/coordinators/{id}` | `{ fullName, directionId, version }` — rename or move to a **vacant** direction of the same faculty |
| POST | `/api/faculty/coordinators/{id}/deactivate` | `{ version }` — soft deactivation |
| POST | `/api/faculty/coordinators/{id}/reset-password` | `{ version }` → `{ user, temporaryPassword }`; own faculty only |

- Same implementation as Phase 3B (`UserManagementService`): validation, CSPRNG temporary password (hash only, `mustChangePassword = true`), seat checks backed by `ux_users_active_faculty_coordinator`, `version` concurrency, audit rows (`user.created`, `user.updated`, `user.deactivated` with the faculty leader as actor).
- **Ownership:** coordinators of another faculty answer **404 `user_not_found`** for get/update/deactivate and never appear in lists, so their existence is not disclosed.
- **Roles:** university coordinators, faculty coordinators and the University Leader get 403 on these routes; faculty leaders get 403 on all `/api/admin/*` routes, so they cannot create university coordinators, faculty leaders or university leaders.
- **Deactivation** keeps the coordinator's reports, attachments, reviews and audit rows; login and open sessions stop working, and the direction slot is freed for a successor. Deactivated coordinators cannot be edited.
- **Error codes:** `coordinator_slot_taken` (409), plus the shared `validation_failed`, `username_taken`, `concurrent_update`, `already_inactive`, `user_inactive`, `user_not_found`, `forbidden`.

## Reports (Phase 4A)

### Workflow and routing

```
Draft ──submit──▶ Submitted ──approve (score 0-10 + breakdown)──▶ Approved (final)
  ▲                   │
  └──edit── Rejected ◀┴──reject (mandatory explanation)
```

| Author | Reviewer |
|---|---|
| Faculty coordinator | Active faculty leader of the **report's** faculty |
| Faculty leader | University Leader |
| University coordinator | University Leader |

The University Leader does not author reports. Faculty and direction are copied from the author's account when the report
is created (snapshot), so history stays with the faculty it was written for.

| Method | Path | Who | Purpose |
|---|---|---|---|
| POST | `/api/reports` | any author role | `{ title, description?, activityDate?, outcome?, participantCount? }` → Draft |
| GET | `/api/reports/mine?status=` | author | Own reports |
| GET | `/api/reports/{id}` | author, responsible reviewer | Detail incl. review history (comments, breakdowns) and attachment metadata |
| PUT | `/api/reports/{id}` | author | Same fields + `version`; only Draft or Rejected |
| POST | `/api/reports/{id}/submit` | author | `{ version }`; Draft/Rejected → Submitted; description, outcome and activityDate required |
| GET | `/api/reports/review-queue?status=Submitted` | faculty leader, University Leader | Reports the caller reviews (`Approved`/`Rejected` for history) |
| POST | `/api/reports/{id}/approve` | responsible reviewer | `{ version, score, scoreBreakdown: [{ criterion, points }], comment? }` |
| POST | `/api/reports/{id}/reject` | responsible reviewer | `{ version, comment }` (comment required) |
| GET | `/api/public/completed-work?facultyId=&directionId=&page=&pageSize=` | anyone | Approved reports, public fields only |
| GET | `/api/public/faculty-rankings` | anyone | Faculties by total approved score |

### Rules

- **Authorization from database state:** the actor, the report's author role and the report's faculty decide everything.
  Reports the caller may not see — other faculties, other authors, drafts of others — answer **404 `report_not_found`**
  for read, edit and review alike. Drafts are visible only to their author. Users without a review role get 403 on the queue.
- **Immutability:** Submitted reports are locked while in review; Approved reports are final (`report_locked`, 409).
  Reviewing anything but a Submitted report returns `report_not_submitted` (409).
- **Scores:** integer 0–10; the breakdown has 1–10 items `{ criterion (1–100 chars), points 0–10 }` whose points must add
  up to the score. The database enforces "score set ⇔ approved" on both `reports` and `report_reviews`, and "rejection ⇒ comment".
- **Review history:** every approval/rejection is a new `report_reviews` row (reviewer, action, score, breakdown, comment, time);
  rejected reports keep their history across resubmissions.
- **Concurrency:** all writes require `version` (PostgreSQL `xmin`). Status change, review row and audit row are saved in one
  transaction guarded by `xmin`, so of several concurrent approvals exactly one succeeds; the others get 409 and leave no review row.
- **Audit:** `report.created`, `report.updated`, `report.submitted` (with `resubmission`), `report.approved` (with score), `report.rejected`. Review comments are not copied into the audit log.
- **Inactive authors:** reports of deactivated users stay, keep their author id and name, can still be reviewed, and stay public once approved (`authorIsActive` shows the status to reviewers).
- **Public views:** approved only; title, description, outcome, activity date, participants, faculty, direction, author name, score and approval time. Never review comments, score breakdowns, attachments or versions.
- **Ranking:** every faculty with its approved count and total score. Only faculty-bound reports count, so university coordinator reports (no faculty) never affect it. Ties: more approved reports first, then faculty id.

## Attachments (Phase 4B)

File bytes live in PostgreSQL (`attachment_contents`: `storage_key`, `content bytea`, Phase 7F); metadata (`report_attachments`:
file name, detected type, size, uploader, time, storage key) sits beside it. All routes need a signed-in user; POST/DELETE need `X-CSRF-TOKEN`.

| Method | Path | Who | Purpose |
|---|---|---|---|
| GET | `/api/reports/{reportId}/attachments` | author, responsible reviewer | Metadata list (no storage keys) |
| POST | `/api/reports/{reportId}/attachments` | author; report Draft or Rejected | `multipart/form-data`, part `file` → 201 metadata |
| GET | `/api/reports/{reportId}/attachments/{attachmentId}` | author, responsible reviewer | Streams the file as a download |
| DELETE | `/api/reports/{reportId}/attachments/{attachmentId}` | author; report Draft or Rejected | Removes metadata, then the object |

- **Access:** same rules as the report (`ReportAccess`). Anyone else — other faculties, other coordinators, the University Leader
  for coordinator reports, drafts of others — gets **404 `report_not_found`**, identical to a non-existent report. A reviewer who
  tries to add or remove files gets 403 (they already know the report exists). Submitted and Approved reports are frozen:
  409 `report_locked`.
- **Limits:** JPEG, PNG, PDF and DOCX only; at most **10 MB** per file and **10** per report (409 `attachment_limit_reached`), and
  **500 MB** for all attachments together (409 `storage_quota_reached`; `AttachmentRules.MaxTotalBytes`, sized for Neon's free
  1 GB per project; raise it together with the database plan).
  Larger request bodies are refused before reading (413 `payload_too_large`); the service also reads at most 10 MB + 1 byte.
- **Content validation (`AttachmentInspector`):** the type is decided by the bytes (JPEG `FF D8 FF`, PNG signature, `%PDF-`,
  ZIP + `[Content_Types].xml` + `word/document.xml` for DOCX). The extension must match the detected type and a declared MIME type,
  if any, must not contradict it; the stored `Content-Type` is always the detected one. Rejected as unsafe where detectable:
  PDFs with `/JavaScript`, `/JS`, `/Launch`, `/EmbeddedFile`, `/RichMedia` or encryption, PDFs without `%%EOF`; DOCX with macros
  (`vbaProject.bin`, macro-enabled content types), embedded OLE objects, remote templates, path-traversal part names, more than
  1000 parts, or zip-bomb expansion (> 100 MB total or > 100:1 per large part).
- **Storage keys:** `reports/{reportId}/{attachmentId}` from server-generated GUIDs; the client file name is only display metadata
  (last path segment, control characters and quotes removed, max 255 chars).
- **Serving:** streamed through the API only — no public or presigned URLs. Responses use `Content-Disposition: attachment`
  (RFC 5987 `filename*`), the detected `Content-Type`, `X-Content-Type-Options: nosniff` (on every API response),
  `Content-Security-Policy: default-src 'none'; sandbox`, `Cache-Control: private, no-store` and `Cross-Origin-Resource-Policy: same-origin`.
- **Consistency (no distributed transaction):**
  - *Upload:* validate → insert the bytes (own statement) → commit metadata + report touch (xmin-guarded) + audit in one DB transaction. If the commit
    fails (e.g. the report was submitted meanwhile → 409 `concurrent_update`), the object is deleted again.
  - *Delete:* commit the metadata removal first, then delete the bytes.
  - **Recovery limitation:** if the process dies between storing an object and committing, or the compensating/cleanup delete
    fails, an object without metadata remains (logged as "Orphaned attachment object …" with its key). It is unreachable through
    the API but uses space; a periodic sweep removing `attachment_contents` rows without a `report_attachments.storage_key` is not implemented yet.
    Metadata without an object can only arise from out-of-band deletion; downloads then return 404 `attachment_unavailable` and log an error.
- **Audit:** `attachment.added` (id, file name, detected type, size) and `attachment.removed`, against the report.

## Announcements and notifications (Phase 5)

PostgreSQL is the source of truth for both; SignalR only tells connected clients that something new exists.

### Announcements

| Method | Path | Who | Purpose |
|---|---|---|---|
| POST | `/api/admin/announcements` | University Leader | `{ title, body }` → delivered to every **currently active** faculty leader |
| GET | `/api/admin/announcements?page=&pageSize=` | University Leader | Sent announcements with recipient and read counts |
| GET | `/api/admin/announcements/{id}` | University Leader | Per-recipient delivery and read status (incl. deactivated recipients) |
| GET | `/api/faculty/announcements?unreadOnly=&page=&pageSize=` | faculty leader | The caller's own inbox |
| GET | `/api/faculty/announcements/unread-count` | faculty leader | `{ unread }` |
| POST | `/api/faculty/announcements/{id}/read` | faculty leader | Idempotent; other people's announcements answer 404 |

- The announcement, one `announcement_recipients` row per active faculty leader and the `announcement.sent` audit row are written
  in **one transaction**. With no active faculty leader the request fails with 409 `no_recipients`.
- Deliveries belong to the recipient **account**. When a faculty leader is deactivated and replaced, past announcements stay with
  the original (inactive) recipient — visible in the delivery status — and the successor's inbox starts empty.
- Any University Leader account sees all sent announcements (institutional record).

### Notifications

| Method | Path | Who | Purpose |
|---|---|---|---|
| GET | `/api/notifications?unreadOnly=&page=&pageSize=` | any signed-in user | Own notifications, newest first |
| GET | `/api/notifications/unread-count` | any signed-in user | `{ unread }` |
| POST | `/api/notifications/{id}/read` | owner | Idempotent; other users' notifications answer 404 `notification_not_found` |
| POST | `/api/notifications/read-all` | owner | `{ marked }` |

| Event | Recipient | Type |
|---|---|---|
| Report submitted / resubmitted | the responsible reviewer: active faculty leader of the report's faculty (coordinator reports) or the active University Leader (faculty leader and university coordinator reports) | `ReportSubmitted` |
| Report approved | the author | `ReportApproved` |
| Report rejected | the author | `ReportRejected` |

Notification rows are created in the **same transaction** as the status change (a failed or concurrent review leaves none).
They carry the report id, a fixed title and the report title — never the review comment or score breakdown. If the reviewer seat
is vacant when a report is submitted, no notification is created (the report still appears in the queue of the next leader).

### Live delivery (SignalR)

- Hub: `/hubs/notifications` (HTTPS only), authenticated by the session cookie with the default policy (password changed).
  Server → client only; each connection is addressed by user id and receives only its own events.
- Events: `notification` and `announcement`, payload `{ id, type, reportId, createdAt }` — ids and types only, no titles,
  bodies or comments. Clients fetch details and counts from the REST endpoints.
- **Ordering and failure handling:** events are published only after the database commit. Publishing never fails a request
  (errors are logged). A missed event (client offline, network blip, server restart right after commit) loses nothing: the record
  is in PostgreSQL. **Clients must re-read unread counts/lists on connect and reconnect**; that makes an outbox unnecessary,
  because a push is a refresh hint, not the delivery mechanism.
- **Cross-site protection:** the hub is exempt from the CSRF header (SignalR negotiate is not state-changing), but browser
  requests with an `Origin` that is neither the API host nor a `Cors:AllowedOrigins` entry get 403 `origin_not_allowed`
  (prevents cross-site WebSocket hijacking with the user's cookie). CORS also allows the SignalR client headers
  `X-Requested-With` and `X-SignalR-User-Agent`.
- Frontend: the official `@microsoft/signalr` client with `withCredentials: true` to `{NEXT_PUBLIC_API_URL}/hubs/notifications`.
  Skip-negotiation WebSocket clients also work (`Plain_websocket_clients_without_negotiate_receive_live_hints`).
  Single API instance only for now; several instances need a SignalR backplane (e.g. Redis) or Azure SignalR.

### Integration tests

`tests/UniversityLeaders.Api.Tests` runs the real API (`WebApplicationFactory`) against the local PostgreSQL. Each test
creates its own database `ul_authtest_<guid>` (migrated, dropped afterwards), attachment bytes included. The dev database is
never touched.

Required environment (load from `.env` as shown in [Local services](#local-services-postgresql--minio)):
`Database__ConnectionString`.

```bash
dotnet test UniversityLeaders.slnx    # from backend/
```

The database role needs `CREATEDB` (the Compose superuser has it).

## Database schema

Entities live in `UniversityLeaders.Domain/Entities`; each has its own `IEntityTypeConfiguration<T>` in
`UniversityLeaders.Infrastructure/Persistence/Configurations`. Enums (`UserRole`, `ReportStatus`, `ReviewAction`) are stored as text.
All tables, columns, keys and indexes are **snake_case** (`EFCore.NamingConventions`); raw SQL in check constraints
and index filters uses the snake_case column names.

| Table | Purpose |
|---|---|
| `users` | One account per person (112 seats: 1 university leader, 13 faculty leaders, 7 university coordinators, 91 faculty coordinators) |
| `faculties` | The 13 faculties (not seeded — real names pending) |
| `directions` | The 7 official directions, seeded with fixed ids 1–7 |
| `reports` | Draft → Submitted → Approved / Rejected (Rejected can be edited and resubmitted), optional score 0–10; faculty/direction copied from the author |
| `report_attachments` | File metadata, with a unique `storage_key` |
| `attachment_contents` | File bytes (`bytea`) by `storage_key` |
| `report_reviews` | Append-only approve/reject history with comments |
| `announcements`, `announcement_recipients` | Announcement plus per-recipient delivery and read tracking |
| `notifications` | Per-user notifications with read tracking |
| `audit_logs` | Append-only who/what/which-entity log (`jsonb` details) |

Enforced by the database, not just the application:

- **Role placement** (`ck_users_role_placement`): university leader has no faculty or direction; faculty leader has a faculty only; university coordinator has a direction only; faculty coordinator has both.
- **One active holder per seat** (partial unique indexes `ux_users_active_*`): one active university leader, one active faculty leader per faculty, one active university coordinator per direction, one active faculty coordinator per faculty and direction. With 13 faculties and 7 directions this caps active accounts at 112.
- **Soft deactivation**: users are never deleted. `is_active = false` requires `deactivated_at` (`ck_users_deactivation`) and frees the seat for a replacement.
- **History is never cascade-deleted**: every foreign key is `RESTRICT`, except announcement → its recipient rows.
- **Case-insensitive login**: `user_name` is `citext` with a unique index, so `Ali` and `ali` collide; length 3–64 (`ck_users_user_name_length`).
- **Report consistency**: `submitted_at` is null exactly when the status is `Draft`; score is null or 0–10 (`ck_reports_score`); attachment size > 0.
- **Concurrent double approval**: `reports` uses PostgreSQL `xmin` as an optimistic concurrency token (`Report.Version`). When two reviewers save the same report, the second `SaveChanges` throws `DbUpdateConcurrencyException`; the application layer must turn that into a conflict response.
- **Credentials**: `password_hash` (nullable until credentials are issued) and `must_change_password` (default `true`) for one-time onboarding. No accounts or passwords are seeded.

## Migrations

Uses the `dotnet-ef` tool (10.x) and the `Microsoft.EntityFrameworkCore.Design` package in the API (startup) project.

```bash
dotnet tool install --global dotnet-ef --version 10.0.7   # once, if not installed

cd backend

# Add a migration after changing entities/configurations
dotnet ef migrations add <Name> --project src/UniversityLeaders.Infrastructure --startup-project src/UniversityLeaders.Api --output-dir Persistence/Migrations

# Apply to the database in Database__ConnectionString (or user secrets)
dotnet ef database update --project src/UniversityLeaders.Infrastructure --startup-project src/UniversityLeaders.Api

# Production: generate a reviewed, idempotent SQL script instead of applying from a dev machine
dotnet ef migrations script --idempotent --project src/UniversityLeaders.Infrastructure --startup-project src/UniversityLeaders.Api -o migrate.sql
```

## Decisions

Resolved before the first database deployment:

- **Naming:** snake_case for every PostgreSQL object.
- **Score:** optional, integer 0–10 inclusive.
- **Login:** unique, case-insensitive `user_name` (`citext`).
- **Rejected reports:** may be edited and resubmitted; every review stays in `report_reviews`.
- **Directions:** the 7 official names, identical in `frontend/src/lib/data.ts` and the migration seed, including the typographic quotes:
  `“Yoshlar daftari” va metsenatlik dasturlari`, `Harbiy-vatanparvarlik loyihalari`, `Talaba-qizlar`, `“Besh tashabbus” loyihalari`,
  `Intellektual loyihalar`, `Tadbirkorlik va innovatsion loyihalar`, `“Social Media”`.
- **History:** users are soft-deactivated; foreign keys restrict deletes, so reports, reviews and audit rows keep their author.
- **Double approval:** `xmin` concurrency token on `reports` (handling the conflict is application-layer work).

Still open (authentication):

- **Data Protection keys** (encrypt the session and CSRF cookies) are kept in the default local key ring. On a host with an ephemeral disk or several instances, persist them (e.g. to PostgreSQL) before production, otherwise every restart signs everyone out.
- **Per-account lockout:** rate limiting is per IP only; distributed guessing against one account is not throttled yet.
- **Audit logging** covers bootstrap and all management operations; logins, failed logins, logouts and password changes are not audited yet.
- **No reactivation or temporary-password reset** endpoints yet; a lost temporary password means deactivate and re-provision with a new user name (user names are never reused).

Still open (messaging):

- **Open connections after deactivation:** a deactivated user's existing SignalR connection is not force-closed (it can only
  receive id-only hints for its own account; all REST calls already fail). Track and abort connections on deactivation if needed.
- **Scale-out:** no SignalR backplane; with more than one API instance, users connected to another instance miss live hints
  (records are still delivered on refresh).
- **Retention:** notifications and announcements are kept forever; no archival or deletion policy yet.
- **Runtime patch:** NuGet advisory GHSA-334f-vvp4-7g6p (SignalR stateful-reconnect DoS) is fixed in 10.0.13+. The test client
  uses 10.0.14; the server uses the installed ASP.NET Core runtime (10.0.6 here), so update the .NET runtime/SDK before deploying.
  Stateful reconnect is not enabled by this API.

Still open (attachments):

- **Orphan sweep:** no background job yet removes `attachment_contents` rows without metadata (see Consistency above).
- **Detection limits:** PDF checks are textual, so scripts hidden in compressed object streams or with hex-escaped names
  (`/J#61vaScript`) are not detected; images are only signature-checked (no re-encoding/EXIF stripping). There is no malware
  scanning. Mitigated by download-only serving with nosniff/sandbox, but a real scanner (e.g. ClamAV) should be added before production.
- **Upload rate limiting** and per-user quotas are not implemented (only the global 500 MB quota).
- **Old local MinIO files:** none existed when storage moved to PostgreSQL (0 attachment rows locally and in Neon), so nothing
  was migrated. A database that still has `report_attachments` rows from the MinIO era needs their objects copied into
  `attachment_contents` (same `storage_key`) before those downloads work again.

Still open (reports):

- **Faculty leader without a successor:** while a faculty has no active faculty leader, its coordinators' submitted reports wait in no one's queue.
- **No withdraw/delete** for drafts or submitted reports, and no reopening of approved reports (deliberately final).
- **Ranking tie-breaks** and whether rankings should be per period (semester/year) are not decided.

Still open:

1. **Faculty names:** unknown, so faculties are not seeded and "exactly 13 faculties" is not enforced by the database. Seed them (fixed ids) once the real names are confirmed.
2. **Approval routing** (who approves whom) is application logic, not a database constraint: faculty coordinator → own faculty leader; faculty leader and university coordinator → university leader.
3. **Who may set the score** (approver only?) and whether a resubmission clears the previous score.
