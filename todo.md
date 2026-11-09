# todo.md — Project Log

## Current Status
Phase 00 (Repository bootstrap) is pushed and merged into `main`: repository conventions, the full specification under `docs/`, `CLAUDE.md`, and skeletons for the root deliverables. No code exists yet. Next: Phase 01 (backend solution skeleton, health endpoint, Dockerfile and compose with PostgreSQL).

## In Progress
- Phase 01 — Backend skeleton & Docker: not started

## Pending / Upcoming

### 00 — Repository bootstrap (`phase/00-bootstrap`)
- [x] 00.1 chore(repo): initialize repository with gitignore, gitattributes and editorconfig
- [x] 00.2 docs: add specification documents and agent guide
- [x] 00.3 docs: add README, ASSUMPTIONS, AI_USAGE skeletons and project log

### 01 — Backend skeleton & Docker (`phase/01-backend-skeleton`)
- [ ] 01.1 chore(backend): scaffold clean-architecture solution and projects
- [ ] 01.2 feat(api): add program setup and health endpoint
- [ ] 01.3 build(docker): add backend Dockerfile and compose with PostgreSQL

### 02 — Domain & persistence (`phase/02-domain-persistence`)
- [ ] 02.1 feat(domain): add class and hold entities with state transitions
- [ ] 02.2 test(domain): cover hold state transitions with unit tests
- [ ] 02.3 feat(infra): add EF Core context and entity configurations
- [ ] 02.4 feat(infra): add initial migration and startup migrator

### 03 — Application plumbing (`phase/03-app-plumbing`)
- [ ] 03.1 feat(app): add CQRS abstractions and MediatR pipeline behaviors
- [ ] 03.2 feat(infra): add unit of work, repositories and constraint translation
- [ ] 03.3 feat(api): add problem-details errors, JSON conventions and CORS
- [ ] 03.4 test: add integration test harness with real PostgreSQL

### 04 — Classes (`phase/04-classes`)
- [ ] 04.1 feat(classes): add create-class command with validation
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
| 01 — Backend skeleton & Docker | phase/01-backend-skeleton | 0/3 | | | |
| 02 — Domain & persistence | phase/02-domain-persistence | 0/4 | | | |
| 03 — Application plumbing | phase/03-app-plumbing | 0/4 | | | |
| 04 — Classes | phase/04-classes | 0/3 | | | |
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
- 2026-11-09 23:18 — 00 merge — pushed phase/00-bootstrap, merged --no-ff into main, Git History row updated in the merge commit — tracked files checked (no PDF/bin/obj/secrets) — merge commit on main
- 2026-11-09 23:16 — 00.3 — README, ASSUMPTIONS (root copy) and AI_USAGE skeletons, .env.example, this project log — no tests (docs only) — a95d5a1
- 2026-11-09 23:13 — 00.2 — specification documents under docs/ and CLAUDE.md (copy of docs/Agents.md) — no tests (docs only) — 61a6a72
- 2026-11-09 23:12 — 00.1 — .gitignore, .gitattributes (LF for *.sh/Dockerfile), .editorconfig — no tests (config only) — c925994
- 2026-11-09 23:11 — 00.0 — empty root commit on main so phases can merge with --no-ff — n/a — f7e8948

## Blockers & Questions for Me
- None.

## Decisions Made
- **Empty root commit on `main` (f7e8948).** The repository had no commits, and `git merge --no-ff` needs an existing `main` to merge into. This is the only direct commit to `main` besides merge commits. Process-only, so not added to ASSUMPTIONS.md.
- **Build and test through Docker.** The .NET 10 SDK isn't installed locally and local Node is v24 (spec: 22). Backend commands run in `mcr.microsoft.com/dotnet/sdk:10.0`, frontend commands in `node:22-alpine`. Tooling-only, so not added to ASSUMPTIONS.md.
- **ASSUMPTIONS.md scope.** Only product/spec interpretations go into both ASSUMPTIONS.md files; git/tooling decisions are logged here only, to keep the graded ASSUMPTIONS.md short and on-topic.

## AI Mistakes & Fixes
- None yet.
