# Agents.md — Operating Manual for AI Coding Agents

> **Read this file first, at the start of every session.** It tells you what this project is, what you must never break, how to work, and where every other piece of context lives.
>
> Tip for the human: copy (or symlink) this file to the repository root as `AGENTS.md` (and `CLAUDE.md` if you use Claude Code) so agent tools load it automatically.

---

## 1. What we are building (30-second version)

A **first-come-first-served online school admission system** (take-home assignment, Onnorokom Projukti Ltd, Assistant Software Engineer).

- A school admin creates "classes" (each *shift* is its own class, e.g. `Class 6 - Morning Shift`, 120 seats).
- A parent types the student's **17-digit birth registration number** into a box at the top of the page. That number *is* the identity. No login.
- Applying creates a **hold** on one seat for `HOLD_DURATION_SECONDS` (default 600). Paying the (simulated) fee within that window **confirms** admission. Otherwise the hold **expires automatically** and the seat returns to the pool.
- Thousands of parents apply at the same instant. **The seat count must never be oversold.**

Stack: **.NET 10 (C#) + MediatR CQRS + EF Core/Npgsql + PostgreSQL 17 + React 19/TypeScript/Vite + Docker Compose.**

---

## 2. Read order

| # | File | Read it when |
|---|------|--------------|
| 1 | `Agents.md` (this) | Always, first |
| 2 | `PRD.md` | Always — the requirements and the 12 mandatory rules (R1–R12) |
| 3 | `API.md` | Before touching any endpoint, DTO, or the frontend API client |
| 4 | `Architecture.md` | Before writing any backend code; §6 (concurrency) before touching holds |
| 5 | `Database.md` | Before touching entities, EF config, migrations, or SQL |
| 6 | `Implementation.md` | To find the current phase/task and the reference code |
| 7 | `Feature_Development.md` | Before adding any new command/query/screen (templates + checklist) |
| 8 | `Design.md` | Before touching UI screens, copy, or error messages |
| 9 | `Integration.md` | Before touching frontend↔backend wiring, CORS, config, polling |
| 10 | `Testing_QA.md` | Before writing tests and before declaring anything "done" |
| 11 | `Authentication_Security.md` | Before touching headers, identity, logging, CORS, nginx |
| 12 | `Setup.md` | For commands, env vars, Docker |
| 13 | `Debugging.md` | When something fails |
| 14 | `ASSUMPTIONS.md` | Whenever the spec is unclear — the decision may already be made |
| 15 | `Git_Workflow.md` | Before every commit, at the start and end of every phase (branches, commit plan, pushes) |

If two documents disagree, priority is: **API.md > PRD.md > ASSUMPTIONS.md > Architecture.md > everything else.** Report the contradiction to the human.

---

## 3. Non-negotiable rules (breaking one fails the assignment)

1. **The API contract is frozen.** Paths, HTTP methods, JSON field names, casing (`camelCase`), status values (`HELD`/`CONFIRMED`/`EXPIRED`/`CANCELLED`) and success status codes (`201`, `200`, `204`, `409` when full) must match `API.md` exactly. The evaluators run automated tests directly against the API. You may **add** response fields; you may **never remove or rename** one.
2. **Seat invariant:** for every class, `held + confirmed <= totalSeats` at all times, under any concurrency, even with two backend processes on one database. Enforce it **only** inside a PostgreSQL transaction that locks the class row (`SELECT ... FOR UPDATE`). See `Architecture.md §6`. Never use in-process locks, static state, caches, or `SemaphoreSlim` for this.
3. **Never return 500 for bad input.** Malformed JSON, wrong types, missing/invalid header, unknown IDs, garbage path segments → 4xx with a problem+json body.
4. **Expiry is automatic.** Seats return at `expiresAt` without anyone calling an API. Counts are computed from `expiresAt`; a background sweeper only updates the stored status.
5. **Everything runs with `docker compose up`** from a fresh clone, with nothing installed but Docker. Backend on `http://localhost:8080`, frontend on `http://localhost:5173`.
6. **No authentication anywhere.** Do not add API keys, JWT, admin passwords, or per-IP rate limiting (it would break the evaluators' concurrency tests).
7. **Data survives restarts**, including `docker compose down` + `docker compose up` (named volume; only `down -v` wipes it).
8. **Every behavior change ships with tests.** Required minimum: ≥ 3 backend tests including a concurrency test. We go well beyond that (see `Testing_QA.md`).
9. **Structured git history** exactly as planned in `Git_Workflow.md`: one branch per phase, the planned commits, merge `--no-ff` into `main`, push both. Never one giant commit. Commit history is graded.

---

## 4. Pinned technical decisions (do not change without the human's approval)

| Area | Decision |
|------|----------|
| Backend runtime | .NET 10 (LTS). `mcr.microsoft.com/dotnet/sdk:10.0` / `aspnet:10.0` |
| Architecture | Clean Architecture (Domain / Application / Infrastructure / Api) + CQRS via **MediatR 12.x** (Apache-2.0; do **not** upgrade to 13+, which needs a license key) |
| Validation | FluentValidation 11.x, run in a MediatR pipeline behavior |
| Mapping | Manual mapping. **No AutoMapper** (commercial license since v15) |
| ORM | EF Core 10 + Npgsql provider. Raw SQL only for row locks and the sweeper |
| DB | PostgreSQL **17** (`postgres:17-alpine`; v18 changed the volume path) |
| IDs | UUID v7 (`Guid.CreateVersion7()`), serialized as lowercase hyphenated strings |
| Time | Inject `TimeProvider`. Never call `DateTime.Now/UtcNow` or `DateTimeOffset.UtcNow` in app code |
| Errors | Exceptions mapped to RFC 9457 `application/problem+json` with an extra `code` field |
| Tests (backend) | xUnit + `Microsoft.AspNetCore.Mvc.Testing` + real PostgreSQL + `FakeTimeProvider`. Plain `Assert` (no FluentAssertions 8+, commercial) |
| Frontend | React 19 + TypeScript (strict) + Vite + React Router + TanStack Query v5. Plain CSS, no UI kit |
| Tests (frontend) | Vitest + React Testing Library + MSW v2 |
| Serving | nginx serves the built SPA; backend URL injected at container start via `/config.js` |

---

## 5. Repository map (short — full tree in `Architecture.md §2`)

```
/
├── README.md  ASSUMPTIONS.md  AI_USAGE.md        # graded deliverables at ROOT
├── docker-compose.yml  docker-compose.dev.yml  docker-compose.multi.yml
├── docs/                                          # these documents
├── backend/   SchoolAdmission.sln, Dockerfile, src/{Domain,Application,Infrastructure,Api}, tests/{UnitTests,IntegrationTests}
└── frontend/  Dockerfile, nginx.conf, src/{api,hooks,student,pages,components,i18n,test}
```

---

## 6. Session protocol (how to work)

1. **Locate the task.** State which phase/task of `Implementation.md` you are doing. If the human's request isn't in the plan, say where it fits.
2. **Load context.** Read the documents listed for that task in §2. Re-read `API.md` for anything touching an endpoint.
3. **Plan briefly.** List files you'll create/modify and the tests you'll add. Keep the change small enough for one commit.
4. **Test first for rules.** For anything touching R1–R7, write/extend the integration test before or alongside the code.
5. **Implement** following `Feature_Development.md` templates and the conventions in §7.
6. **Verify.** Run backend tests, frontend tests and typecheck (commands in §10). If you can't run them, say so explicitly — never claim tests pass without running them.
7. **Self-review** against §9 (Definition of Done).
8. **Commit** using the planned message from `Git_Workflow.md §5` (format §3), update `todo.md` in the same commit, and at phase end follow `Git_Workflow.md §4` (verify → push branch → merge → push main).

---

## 7. Coding conventions

### C# (backend)
- `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in all projects.
- File-scoped namespaces. `sealed` classes by default. Primary constructors are fine.
- Commands/queries/DTOs are `sealed record`s. One feature folder per use case: `Holds/CreateHold/{CreateHoldCommand, CreateHoldCommandValidator, CreateHoldCommandHandler}.cs`.
- Async all the way; every async method takes and forwards a `CancellationToken`.
- Handlers never touch `HttpContext`. Controllers never contain business logic — they parse route/header values, `Send` a request, and shape the HTTP response.
- Dependency rule: `Api → Application → Domain`; `Infrastructure → Application → Domain`. Application never references EF Core or Npgsql.
- Business errors are thrown as `NotFoundException`, `ForbiddenException`, `ConflictException`, `DomainException`, `RequestValidationException`. Every error has a stable `code` from `ErrorCodes`.
- Log student IDs **masked** (last 4 digits only). Never log full request bodies.

### TypeScript (frontend)
- `strict: true`, no `any`, no non-null assertions on API data.
- Server state lives **only** in TanStack Query. Client state: the student ID (React context + `localStorage`). No Redux/Zustand.
- All API calls go through `src/api/client.ts` (`apiFetch`). Never call `fetch` directly elsewhere.
- All user-facing strings and error-code→message mappings live in `src/i18n/messages.ts`.
- API types in `src/api/types.ts` mirror `API.md` exactly.

---

## 8. Forbidden list

- ❌ Renaming/removing contract fields, changing status codes, or changing status strings.
- ❌ `lock`, `SemaphoreSlim`, `Interlocked`, static mutable fields, `IMemoryCache` for seat logic.
- ❌ `IsolationLevel.Serializable` (causes serialization failures under load). Use READ COMMITTED + explicit row lock.
- ❌ `EnableRetryOnFailure()` on the Npgsql provider (conflicts with user-initiated transactions).
- ❌ EF Core InMemory or SQLite providers in tests that touch holds (they can't do `FOR UPDATE`).
- ❌ Auth, API keys, per-IP rate limiting.
- ❌ Hard-coded backend URLs in frontend code other than the default in `src/config.ts`.
- ❌ `http://backend:8080` in the frontend — the **browser** calls the API, so it must be `http://localhost:8080`.
- ❌ Committing the assignment PDF, `.env` files with real secrets, `bin/`, `obj/`, `node_modules/`, `dist/`.
- ❌ Packages with commercial licenses (MediatR ≥ 13, AutoMapper ≥ 15, FluentAssertions ≥ 8).
- ❌ Saying "tests pass" without having run them.

---

## 9. Definition of Done (every task)

- [ ] Behavior matches `PRD.md` and `API.md` exactly.
- [ ] New/changed behavior covered by tests; all backend + frontend tests pass; `npm run typecheck` passes.
- [ ] No input can produce a 500 (add a robustness test case if you added an input).
- [ ] UI shows friendly errors for every failure path you introduced (no blank page, no console-only error).
- [ ] `docker compose up --build` still works from a clean state.
- [ ] Docs updated if you changed a decision (`ASSUMPTIONS.md`, `API.md`, `Database.md`, …).
- [ ] Committed per `Git_Workflow.md`; `todo.md` updated in the same commit.

---

## 10. Commands cheat sheet

```bash
# Run everything
docker compose up --build                          # frontend :5173, backend :8080
HOLD_DURATION_SECONDS=30 docker compose up --build  # short holds for manual testing

# Tests (Docker only)
docker compose --profile test run --rm backend-tests
docker compose --profile test run --rm frontend-tests

# Local dev (needs .NET 10 SDK + Node 22)
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db   # Postgres on localhost:5433
cd backend && dotnet test
cd backend && dotnet run --project src/SchoolAdmission.Api
cd frontend && npm ci && npm run dev && npm test && npm run typecheck

# Migrations
cd backend && dotnet ef migrations add <Name> \
  -p src/SchoolAdmission.Infrastructure -s src/SchoolAdmission.Api -o Persistence/Migrations

# Inspect DB
docker compose exec db psql -U admission -d admission

# Reset all data
docker compose down -v
```

---

## 11. When the spec is ambiguous

1. Check `ASSUMPTIONS.md` — the decision is probably already there.
2. If not, choose the **simplest option consistent with the 12 rules and the frozen contract**.
3. Add a one-line entry (assumption + reason) to `ASSUMPTIONS.md`.
4. Tell the human what you decided in your reply.

---

## 12. AI_USAGE.md (graded deliverable — the human owns it)

The human must write `AI_USAGE.md` at the repo root (short): which AI tools, which parts they were used for, **where the AI was wrong and how it was caught**. Help them keep it honest: whenever the human corrects you, or you discover your earlier output was wrong (a failing test, a wrong assumption, a bad API shape), end your reply with a suggested entry:

```markdown
- **[Area]** AI suggested <what>. Problem: <why it was wrong>. Caught by: <test / review / manual QA>. Fix: <what changed>.
```
