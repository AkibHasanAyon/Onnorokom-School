# todo.md — Project Log

## Current Status
Phase 04 (Classes) in progress on `phase/04-classes`. Phases 00–03 are merged: domain + EF model + migrations, MediatR pipeline (validation → 400, READ COMMITTED transactions), repositories with `FOR UPDATE` row locks, problem+json errors with stable codes, CORS, 64 KB limit, and a real-PostgreSQL integration-test harness. `POST /classes` now works: 201 + `Location`, trimmed name, 400 `VALIDATION_FAILED`/`MALFORMED_JSON`, 409 `CLASS_NAME_EXISTS` (case-insensitive), 413 over 64 KB (verified against real Kestrel). Tests: 92 unit + 79 integration passing. Next: `GET /classes` and `GET /classes/{id}` with derived seat counts.

## In Progress
- Phase 04 — Classes: 04.2 `feat(classes): add class list and detail queries with seat accounting`

## Pending / Upcoming

### 00 — Repository bootstrap (`phase/00-bootstrap`)
- [x] 00.1 chore(repo): initialize repository with gitignore, gitattributes and editorconfig
- [x] 00.2 docs: add specification documents and agent guide
- [x] 00.3 docs: add README, ASSUMPTIONS, AI_USAGE skeletons and project log

### 01 — Backend skeleton & Docker (`phase/01-backend-skeleton`)
- [x] 01.1 chore(backend): scaffold clean-architecture solution and projects
- [x] 01.2 feat(api): add program setup and health endpoint
- [x] 01.3 build(docker): add backend Dockerfile and compose with PostgreSQL

### 02 — Domain & persistence (`phase/02-domain-persistence`)
- [x] 02.1 feat(domain): add class and hold entities with state transitions
- [x] 02.2 test(domain): cover hold state transitions with unit tests
- [x] 02.3 feat(infra): add EF Core context and entity configurations
- [x] 02.4 feat(infra): add initial migration and startup migrator

### 03 — Application plumbing (`phase/03-app-plumbing`)
- [x] 03.1 feat(app): add CQRS abstractions and MediatR pipeline behaviors
- [x] 03.2 feat(infra): add unit of work, repositories and constraint translation
- [x] 03.3 feat(api): add problem-details errors, JSON conventions and CORS
- [x] 03.4 test: add integration test harness with real PostgreSQL

### 04 — Classes (`phase/04-classes`)
- [x] 04.1 feat(classes): add create-class command with validation
- [ ] 04.2 feat(classes): add class list and detail queries with seat accounting
- [ ] 04.3 test(classes): add contract and validation tests for class endpoints

### 05 — Holds & concurrency ⭐ (`phase/05-holds-concurrency`)
- [ ] 05.1 feat(holds): add create-hold command with class-row locking
- [ ] 05.2 test(holds): prove seat limit under concurrent apply bursts
- [ ] 05.3 test(holds): add duplicate-application and hold contract tests

### 06 — Hold lifecycle (`phase/06-hold-lifecycle`)
- [ ] 06.1 feat(holds): add hold status and my-holds queries
- [ ] 06.2 feat(holds): add confirm and cancel with ownership checks
- [ ] 06.3 test(holds): cover lifecycle, ownership and idempotent confirm

### 07 — Automatic expiry (`phase/07-expiry`)
- [ ] 07.1 feat(holds): add expiry command and background sweeper
- [ ] 07.2 test(holds): verify expiry timing with fake time provider

### 08 — Backend hardening (`phase/08-hardening`)
- [ ] 08.1 test(api): guarantee 4xx for invalid input on all endpoints
- [ ] 08.2 fix(api): fixes found by the robustness matrix (or refactor(api) if none)
- [ ] 08.3 feat(app): mask student ids in logs and add request logging
- [ ] 08.4 test(infra): verify data persists across application restarts

### 09 — Frontend foundation (`phase/09-frontend-foundation`)
- [ ] 09.1 chore(frontend): scaffold Vite React TypeScript app
- [ ] 09.2 feat(frontend): add runtime config, API client and contract types
- [ ] 09.3 feat(frontend): add layout, routing, error boundary and query client
- [ ] 09.4 build(docker): serve frontend with nginx and add to compose

### 10 — Identity & classes UI (`phase/10-classes-ui`)
- [ ] 10.1 feat(frontend): add student ID bar with validation and persistence
- [ ] 10.2 feat(frontend): add class list with live seat counts and apply flow
- [ ] 10.3 feat(frontend): add admin create-class page

### 11 — Applications UI (`phase/11-applications-ui`)
- [ ] 11.1 feat(frontend): add application page with live countdown and payment
- [ ] 11.2 feat(frontend): add my-applications page
- [ ] 11.3 feat(frontend): add friendly error messages and connection banner

### 12 — Frontend tests (`phase/12-frontend-tests`)
- [ ] 12.1 test(frontend): set up Vitest, Testing Library and MSW
- [ ] 12.2 test(frontend): cover student ID, class polling and apply errors
- [ ] 12.3 test(frontend): cover expiry countdown, confirm flow and error boundary
- [ ] 12.4 build(docker): add frontend test service to compose

### 13 — Multi-instance bonus (`phase/13-multi-instance`)
- [ ] 13.1 build(docker): add multi-instance compose override
- [ ] 13.2 test(holds): verify seat limit across two app instances
- [ ] 13.3 chore(scripts): add two-instance concurrency smoke script

### 14 — Delivery (`phase/14-delivery`)
- [ ] 14.1 docs: write README with run and test instructions
- [ ] 14.2 docs: finalize assumptions with multi-instance explanation
- [ ] 14.3 docs: add AI usage report
- [ ] 14.4 fix: final QA fixes from clean-clone run (only if needed)

## Git History

| Phase | Branch | Commits | Tests | Pushed | Merged to main |
|-------|--------|---------|-------|--------|----------------|
| 00 — Repository bootstrap | phase/00-bootstrap | 3/3 (c925994, 61a6a72, a95d5a1) | n/a (docs only) | ✅ | ✅ |
| 01 — Backend skeleton & Docker | phase/01-backend-skeleton | 3/3 (797a484, 836e02e, d597e04) | ✅ 1 passed (Docker) | ✅ | ✅ |
| 02 — Domain & persistence | phase/02-domain-persistence | 4/4 (53a7d5a, a23bc5b, 16442dd, c07ff0a) | ✅ 44 unit + 33 integration (Docker, real PostgreSQL) | ✅ | ✅ |
| 03 — Application plumbing | phase/03-app-plumbing | 4/4 (bd437fd, f60470f, 7cd9ee1, 6e5c70e) | ✅ 77 unit + 79 integration (Docker, real PostgreSQL) | ✅ | ✅ |
| 04 — Classes | phase/04-classes | 1/3 | | | |
| 05 — Holds & concurrency | phase/05-holds-concurrency | 0/3 | | | |
| 06 — Hold lifecycle | phase/06-hold-lifecycle | 0/3 | | | |
| 07 — Automatic expiry | phase/07-expiry | 0/2 | | | |
| 08 — Backend hardening | phase/08-hardening | 0/4 | | | |
| 09 — Frontend foundation | phase/09-frontend-foundation | 0/4 | | | |
| 10 — Identity & classes UI | phase/10-classes-ui | 0/3 | | | |
| 11 — Applications UI | phase/11-applications-ui | 0/3 | | | |
| 12 — Frontend tests | phase/12-frontend-tests | 0/4 | | | |
| 13 — Multi-instance bonus | phase/13-multi-instance | 0/3 | | | |
| 14 — Delivery | phase/14-delivery | 0/4 | | | |

## Done Log
(Times are the git commit times, `git log --date=format:"%Y-%m-%d %H:%M"`.)
- 2026-11-10 00:29 — 04.1 — CreateClassCommand (+ CreatedClassDto), validator (name required/≤100 after trim, totalSeats required 1–10000), handler (UUIDv7, uniqueness via unique index → 409), CreateClassRequest (nullable fields), ClassesController POST → 201 + Location; appsettings silences EF Command/Update error logs for expected constraint violations — clean build 0 warnings; `dotnet test`: UnitTests 92 passed (15 new validator cases), IntegrationTests 79 passed, 0 failed; Docker curl: 201 + Location with trimmed name, case-insensitive duplicate → 409 CLASS_NAME_EXISTS, `"120"` → 400 MALFORMED_JSON, 100 KB body → 413 PAYLOAD_TOO_LARGE, 0 fail/crit log lines — (this commit)
- 2026-11-10 00:21 — 03 merge — full verification on 6e5c70e (fresh volume; `docker compose --profile test run --rm backend-tests`: 77 unit + 79 integration passed, exit 0, no MSB warnings; `docker compose up --build -d`: health 200, unknown route → 404 problem+json, 0 fail/crit log lines; no leftover test DBs; no bin/obj/PDF/.env tracked), pushed phase/03-app-plumbing, merged --no-ff into main, Git History row updated in the merge commit — 359e8ca
- 2026-11-10 00:19 — 03.4 — IntegrationTests harness: ApiFixture (+ Customize hook; ProbeApiFixture), TestData (StudentId, unique ClassName), HttpAssertions (ReadJsonAsync, ProblemCodeAsync, AssertProblemAsync, AssertHasProperties), Db.AssertInvariantAsync (Database.md §10 queries evaluated at a given time), Concurrency collection; HarnessTests (7: ids valid/distinct, names unique, fixture migrated, invariant passes/detects overselling/uses given time, DB isolation + drop); ApiConventionsTests and RepositoryTests moved onto the shared fixture; sweep-batch test moved to SweepBatchTests (own DB) — clean rebuild 0 warnings; `dotnet test` ×3: UnitTests 77 passed, IntegrationTests 79 passed, 0 failed; no leftover test DBs — 6e5c70e
- 2026-11-10 00:15 — 03.3 — Api: GlobalExceptionHandler (AppException/DomainException → 4xx with code + extensions, DB outage → 503, BadHttpRequestException → its status, else 500 without details), ProblemDetailsSetup (type httpstatuses.io, default codes, traceId, MALFORMED_JSON vs VALIDATION_FAILED for model binding), UtcDateTimeOffsetConverter, strict JSON number handling, CORS policy, Kestrel 64 KB limit, UseExceptionHandler/UseStatusCodePages/UseCors order; HealthResponse now uses DateTimeOffset + converter; EF Core pinned to 10.0.14 in Infrastructure (MSB3277 fix) — clean rebuild 0 warnings; `dotnet test`: UnitTests 77 passed, IntegrationTests 72 passed (28 new ApiConventionsTests via a test-only ProbeController), 0 failed; Docker curl: 404/405 problem+json, CORS preflight 204 with allowed headers/methods — 7cd9ee1
- 2026-11-10 00:08 — 03.2 — Application `IClassRepository`/`IHoldRepository` (+ `SeatCounts`); Infrastructure `UnitOfWork` (READ COMMITTED tx, 23505 → ConflictException by constraint name, 23514 → 400), `ClassRepository`/`HoldRepository` per Database.md §5 (FromSql FOR UPDATE without LINQ composition, ExecuteUpdate stale-hold expiry, SKIP LOCKED batch), DI registrations — `dotnet test` (PostgreSQL 17): UnitTests 77 passed, IntegrationTests 44 passed (11 new RepositoryTests: 409 translation ×2, rollback/commit, class + hold row locks verified from a second connection with lock_timeout, seat counts incl. exactly-at-expiry, find active/confirmed, stale-hold expiry scope, sweep batches), 0 failed — f60470f
- 2026-11-10 00:05 — 03.1 — Application: ICommand/ICommandMarker/IQuery, IUnitOfWork + IUnitOfWorkTransaction, Logging/Validation/Transaction behaviors (registered in that order), AppException hierarchy + RequestValidationException (INVALID_STUDENT_ID vs VALIDATION_FAILED, camelCase field errors), ServiceUnavailableException, HoldOptions (env binding, 1..86400 / 1..300, ValidateOnStart), TruncateToSeconds, RouteId, StudentIdRules; AddApplication wired in Program — `dotnet test` (sdk:10.0 + PostgreSQL 17): UnitTests 77 passed (33 new: behaviors, options, rules, helpers), IntegrationTests 33 passed, 0 failed — bd437fd
- 2026-11-09 23:57 — 02 merge — full verification on c07ff0a (`docker compose --profile test run --rm backend-tests`: 44 unit + 33 integration passed, exit 0; `docker compose up --build -d` + `curl /health` → 200; no leftover test DBs; no bin/obj/PDF/.env tracked), pushed phase/02-domain-persistence, merged --no-ff into main, Git History row updated in the merge commit — b206b7e
- 2026-11-09 23:55 — 02.4 — `InitialCreate` migration (generated with dotnet-ef 10.0.14 in the SDK container, matches Database.md §2), `DatabaseMigrator` (connect retry 15×2 s, `pg_advisory_lock(7272026)`, creates history table first, `MigrateAsync`), Program migrates before `Run`, `IDatabaseHealthCheck` + `/health` → 503 when the DB is unreachable (3 s probe timeout); test harness: `PostgresDatabase` (throwaway `test_<guid>` DB) + `AdmissionApiFactory`; tests: migration applied, two instances starting together migrate once, restart keeps data, status stored as "HELD", partial unique index + case-insensitive class name index enforced, health 200/503 — `dotnet test` against PostgreSQL 17 (compose network): UnitTests 44 passed, IntegrationTests 33 passed, 0 failed; Docker stack: fresh volume migrates with no error logs, health 200, `down`+`up` keeps rows, `stop db` → 503, `start db` → 200 — c07ff0a
- 2026-11-09 23:47 — 02.3 — Infrastructure: AdmissionDbContext, SchoolClassConfiguration, HoldConfiguration, HoldStatusConverter (Held ↔ "HELD"), AddInfrastructure (Npgsql, no retry strategy), wired in Program — `dotnet test`: UnitTests 44 passed, IntegrationTests 27 passed (26 new ModelTests cases checking generated DDL vs Database.md §2), 0 failed — 16442dd
- 2026-11-09 23:45 — 02.2 — unit tests: HoldTests (Create, EffectiveStatus boundaries incl. exactly-at-expiry, Confirm/Cancel matrix with idempotency and error codes, Expire), SchoolClassTests (trim/normalize, name 1–100, seats 1–10000), StudentIdentifierTests (ASCII-only, Bengali/full-width rejected) — `dotnet test` in sdk:10.0 container: UnitTests 44 passed, IntegrationTests 1 passed, 0 failed — a23bc5b
- 2026-11-09 23:44 — 02.1 — Domain: SchoolClass, Hold (state transitions per Implementation.md §R2 plus `Expire`), HoldStatus, ErrorCodes (all API.md §3 codes), DomainException, StudentIdentifier — build 0 warnings; `dotnet test`: 1 passed (existing health test; domain tests follow in 02.2) — 53a7d5a
- 2026-11-09 23:37 — 01 merge — full verification on d597e04 (backend-tests in Docker: 1 passed; `docker compose up --build -d` + `curl /health` → 200; no bin/obj/PDF/.env tracked), pushed phase/01-backend-skeleton, merged --no-ff into main, Git History row updated in the merge commit — 9945738
- 2026-11-09 23:36 — 01.3 — backend/Dockerfile (restore-cached build, `test` stage, non-root `runtime` stage last), .dockerignore, docker-compose.yml (db PostgreSQL 17 + named volume `pgdata`, backend :8080, `backend-tests` profile), docker-compose.dev.yml (db on :5433) — `docker compose up --build -d`: db healthy, `curl /health` → 200 `{"status":"ok","serverTime":"…Z"}`, backend runs as uid 1654; `docker compose --profile test run --rm backend-tests`: 1 passed, exit 0 — d597e04
- 2026-11-09 23:31 — 01.2 — Program.cs (TimeProvider singleton, controllers, `public partial class Program`), HealthController + HealthResponse, HealthEndpointTests with FakeTimeProvider — build 0 warnings; `dotnet test`: 1 passed, 0 failed — 836e02e
- 2026-11-09 23:29 — 01.1 — solution `SchoolAdmission.sln` with 4 src + 2 test projects, references per dependency rule, pinned packages, Directory.Build.props (net10.0, nullable, TreatWarningsAsErrors), global.json — `dotnet build -c Release` in sdk:10.0 container: 0 warnings, 0 errors; `dotnet test`: 0 tests (none exist yet) — 797a484
- 2026-11-09 23:16 — 00 merge — pushed phase/00-bootstrap, merged --no-ff into main, Git History row updated in the merge commit — tracked files checked (no PDF/bin/obj/secrets) — 9299aad
- 2026-11-09 23:15 — 00.3 — README, ASSUMPTIONS (root copy) and AI_USAGE skeletons, .env.example, this project log — no tests (docs only) — a95d5a1
- 2026-11-09 23:14 — 00.2 — specification documents under docs/ and CLAUDE.md (copy of docs/Agents.md) — no tests (docs only) — 61a6a72
- 2026-11-09 23:14 — 00.1 — .gitignore, .gitattributes (LF for *.sh/Dockerfile), .editorconfig — no tests (config only) — c925994
- 2026-11-09 23:14 — 00.0 — empty root commit on main so phases can merge with --no-ff — n/a — f7e8948

## Blockers & Questions for Me
- **GitHub default branch is `phase/00-bootstrap`** (it was the first branch pushed). Please switch it to `main` in GitHub → Settings → General → Default branch. `gh` isn't installed here, so I can't change it. This doesn't block work, but reviewers will land on the wrong branch until it's fixed.

## Decisions Made
- **Empty root commit on `main` (f7e8948).** The repository had no commits, and `git merge --no-ff` needs an existing `main` to merge into. This is the only direct commit to `main` besides merge commits. Process-only, so not added to ASSUMPTIONS.md.
- **Build and test through Docker.** The .NET 10 SDK isn't installed locally and local Node is v24 (spec: 22). Backend commands run in `mcr.microsoft.com/dotnet/sdk:10.0`, frontend commands in `node:22-alpine`. Tooling-only, so not added to ASSUMPTIONS.md.
- **Classic `.sln` instead of `.slnx`.** The .NET 10 SDK creates `.slnx` by default; the spec names `SchoolAdmission.sln`, so the solution was created with `--format sln`.
- **Exact package pins.** FluentValidation 11.12.0 (latest 11.x; 12.x exists but the spec pins 11.x), MediatR 12.5.0, Npgsql/Npgsql EF 10.0.3, Microsoft EF Design / Mvc.Testing / Options 10.0.14, TimeProvider.Testing 10.11.0, xunit 2.9.3 (as generated by the SDK template). `coverlet.collector` from the template was removed (no coverage requirement).
- **`global.json`** pins SDK 10.0.x (`rollForward: latestFeature`) so the 10.0 SDK is used even when a newer SDK is also installed (Docker builds are unaffected).
- **Health test added in Phase 01** (the plan starts tests in Phase 02/03). It needs no database and pins the timestamp format early. Its `serverTime` is formatted in the controller for now; Phase 03 moves timestamp formatting to the shared `UtcDateTimeOffsetConverter`.
- **Explicit lowercase PK/FK names** (`pk_classes`, `pk_holds`, `fk_holds_classes_class_id`). Database.md §2 leaves them unnamed; lowercase keeps every DB object snake_case, matching the other names. Schema detail only, so not added to ASSUMPTIONS.md.
- **Model verified by generating the DDL** (`GenerateCreateScript`) in `IntegrationTests/Persistence/ModelTests`, so schema drift from Database.md §2 is caught without a database.
- **Test harness pulled forward from 03.4.** Since 02.4 the API migrates the database at startup, so every host-based test (including the Phase 01 health test) needs a real PostgreSQL. A minimal `PostgresDatabase` + `AdmissionApiFactory` (Testing_QA.md §3.1–3.2) ships in 02.4; 03.4 still adds the remaining helpers (`TestData`, API client extensions, invariant assertion, the Concurrency collection). Same structure, nothing merged or skipped.
- **DB-backed health check lives in 02.4** (Implementation.md task 2.3 has no commit of its own in Git_Workflow.md §5); it needs the migrator anyway. Exposed via an `IDatabaseHealthCheck` abstraction in Application so the controller doesn't touch EF Core.
- **Migrator creates `__EFMigrationsHistory` before `MigrateAsync`** (`IHistoryRepository.CreateIfNotExistsAsync`). Otherwise EF logs a `fail: Failed executing DbCommand` on every fresh database, which is harmless but alarming for reviewers reading `docker compose logs`.
- **`IUnitOfWork` interface ships in 03.1** (not 03.2) because `TransactionBehavior` depends on it; the EF implementation is 03.2.
- **Read stores (`IClassReadStore`, `IHoldReadStore`) are added with their first query** (Phases 04 and 06), because they return DTOs that don't exist yet. 03.2 adds the unit of work and the write-side repositories only.
- **Validation error key for the header is `X-Student-Id`** (not camel-cased); body fields are camelCase like the JSON (`totalSeats`).
- **Behaviors forward the `CancellationToken` to `next(...)`** (MediatR 12.5's delegate accepts one) so request aborts reach the handler.
- **Check-violation (23514) → 400 translation has no automated test**: the domain guards make it unreachable through the public model, so a test would need reflection hacks. It stays as a safety net and is logged as an error if it ever fires.
- **Test-only `ProbeController`** (in the IntegrationTests assembly, added via `AddApplicationPart` only in `ApiConventionsTests`) exercises error mapping and body binding before any real endpoint exists. It is never part of the shipped API.
- **DB outage detection** in `GlobalExceptionHandler`: `NpgsqlException` without a server error code, `PostgresException.IsTransient` (e.g. 53300 too many clients), `TimeoutException` and `ServiceUnavailableException` → 503 `SERVICE_UNAVAILABLE`; other server errors are bugs → 500.
- **Client disconnects** (`OperationCanceledException` with `RequestAborted`) get status 499 and no body instead of an error log.
- **413 must be checked against real Kestrel.** TestServer ignores `MaxRequestBodySize`, and Kestrel only enforces it when an endpoint reads the body. The mapping is unit-tested via the probe; a real 100 KB `POST /classes` curl check is planned for Phase 04, and a test for Phase 08.
- **EF Core pinned to 10.0.14 in Infrastructure.** Npgsql EF 10.0.3 alone resolves EF Core 10.0.4, which conflicted (MSB3277) with the 10.0.14 the API compiles against.
- **HTTP client helpers per endpoint (`CreateClassIdAsync`, `HoldAsync`, `ConfirmAsync`, ...) arrive with their endpoints** (Phases 04–06), so every helper is exercised by real tests when it is added.
- **`Db.AssertInvariantAsync` takes the time to evaluate at** (default: real now). Tests run the API on a `FakeTimeProvider`, so using PostgreSQL's `now()` as in Database.md §10 would judge expiry at the wrong instant.
- **Global sweep tests get their own database.** With a shared per-class database, a global `ExpireStaleBatchAsync` also touches other tests' rows.
- **Class `created_at` keeps full precision** (not exposed in the API; holds still use whole seconds). `GET /classes` orders by creation time then id, and second-truncated times would order classes created in the same second arbitrarily.
- **EF Core `Database.Command` and `Update` log categories set to `None`.** Expected unique violations (translated to 409) were logged twice at error level, which would flood logs during duplicate-application bursts. Real failures are still logged with the full exception by `GlobalExceptionHandler`; SQL logging can be re-enabled with `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information` (Debugging.md §5).
- **Create-class validation messages**: "Name is required.", "Name must be at most 100 characters.", "Total seats is required.", "Total seats must be between 1 and 10000." (the last matches the API.md §3 example).
- **ASSUMPTIONS.md scope.** Only product/spec interpretations go into both ASSUMPTIONS.md files; git/tooling decisions are logged here only, to keep the graded ASSUMPTIONS.md short and on-topic.

## AI Mistakes & Fixes
- **[Backend tests]** When the AI moved RepositoryTests onto a shared per-class database (03.4), it missed that the sweep-batch test operates on all holds in the database, not only its own class. Caught by: the test failing (expected 1 expired, got 2). Fix: the sweep test moved to its own class with its own database.
- **[API JSON]** The AI assumed ASP.NET Core rejects `"totalSeats":"120"` by default. The MVC web JSON defaults allow numbers in strings, which would break API.md §4.1 (`"120"` → 400). Caught by: the new `MalformedOrWronglyTypedJson_Is400MalformedJson` test (got 200). Fix: `JsonNumberHandling.Strict`.
- **[Build]** An MSB3277 EF Core version conflict (10.0.4 vs 10.0.14) had been present since Phase 02. It went unnoticed because incremental builds and the AI's grep for `Warning(s)` hid it, and `TreatWarningsAsErrors` doesn't cover MSBuild warnings. Caught by: a `--no-incremental` rebuild in 03.3. Fix: pin `Microsoft.EntityFrameworkCore(.Relational)` 10.0.14 in Infrastructure; clean rebuild shows 0 warnings. Future verification uses clean builds.
- **[MediatR]** The AI wrote unit tests with a parameterless `RequestHandlerDelegate` lambda (`() => ...`), from the MediatR 12.0 signature; in 12.5 the delegate takes an optional `CancellationToken`. Caught by: the compiler (CS1593) on the first 03.1 build. Fix: `_ => ...` in tests, and behaviors now call `next(cancellationToken)`.
- **[Backend tests]** The AI set `FakeTimeProvider` to a fixed past date (2026-11-01) in the health test, but the factory clock starts at the current time and `FakeTimeProvider` can't go backwards → `ArgumentOutOfRangeException`. Caught by: the first test run in 02.4. Fix: use a fixed future instant (2099-11-01T04:03:12.789Z).
- **[Migrator]** The AI first logged pending migrations via `GetPendingMigrationsAsync` and assumed `MigrateAsync` alone would start cleanly; both produce an EF `fail:` log line on a brand-new database (history table queried before it exists). Caught by: reading `docker compose logs backend` during manual Docker verification. Fix: create the history table first with `IHistoryRepository.CreateIfNotExistsAsync`, then `MigrateAsync`; verified 0 `fail:` lines on a fresh volume and on restart.
- **[Project log]** The AI wrote estimated times into the Done Log instead of real ones (e.g. 01.1 logged at 23:40, but committed at 23:29; Phase 00 entries were off by 1–4 minutes). Caught by: comparing with `git log` before committing 01.3. Fix: all Done Log times now come from `git log` commit times. Pushed Phase 00 history was not rewritten; the correction ships in 01.3.
- **[Git / GitHub]** The AI pushed `phase/00-bootstrap` before `main` existed on the remote, so GitHub made the phase branch the default branch. Caught by: `git ls-remote --symref origin HEAD` after the Phase 00 push. Fix: needs a manual switch in GitHub settings (see Blockers). For future repos, push `main` first.
