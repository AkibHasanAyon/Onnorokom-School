# Git_Workflow.md — Phases, Branches, Commits, Pushes

> The commit history is graded ("commit as you work, not one big commit at the end"). This file is the **single source of truth for git**. It overrides the commit messages suggested in `Implementation.md`.

## 1. Model

```
main ──●────────●──────────●──────────●── ...   (always builds, always passes tests)
        \      / \        / \        /
phase/00 ●─●─●   phase/01 ●─●─●  phase/02 ●─●─●─●
```

- `main` only receives **merge commits** from phase branches (`--no-ff`), so every phase is visible as a block in the history.
- One branch per phase: `phase/NN-short-name`.
- Each phase has **2–5 commits**. Each commit is one coherent, reviewable unit (typically 3–15 files) that builds and passes tests.
- After every phase: merge to `main`, push the phase branch **and** `main`. Phase branches are kept (not deleted) so GitHub shows the full structure.
- Total: **15 phases, ~50 commits**.

## 2. Commit size rules

| ✅ Good commit | ❌ Too small | ❌ Too big |
|---------------|-------------|-----------|
| "Add create-hold command with class-row locking" (command, validator, handler, repo methods, controller action) | "Add CreateHoldCommand.cs" | "Add all hold features" |
| "Add concurrency tests for seat limit" (3 related tests + helper) | "Fix typo" (squash it into the related commit before pushing) | Backend + frontend in one commit |

Rules:
1. Never mix backend and frontend changes in one commit (exception: a `docs:` commit).
2. Tests go either in the same commit as the feature or in the immediately following `test:` commit of the same phase, as planned in §5.
3. Every commit must build. Run the relevant tests before committing.
4. Update `todo.md` in the same commit as the work it describes.
5. Small fixes discovered before the phase is pushed → `git commit --amend` or fold into the next planned commit. After pushing, never rewrite history; use a `fix:` commit.

## 3. Commit message format

```
<type>(<scope>): <imperative summary, ≤ 72 chars>

<body: 2–5 lines — what changed and why, wrapped at 72>
- bullet points are fine

Phase: NN — <phase title>
```

Types: `feat`, `fix`, `test`, `refactor`, `docs`, `build`, `chore`, `perf`.
Scopes: `repo`, `backend`, `domain`, `app`, `infra`, `api`, `classes`, `holds`, `frontend`, `docker`, `scripts`.

Example:
```
feat(holds): add create-hold command with class-row locking

Lock the class row (SELECT ... FOR UPDATE) before counting active
holds so concurrent applications can never exceed totalSeats, even
across multiple backend instances. Enforce one active-or-confirmed
hold per student per class.

Phase: 05 — Holds & concurrency
```

Merge commit message: `merge: phase NN — <phase title>`

## 4. Per-phase procedure

```bash
# start
git switch main && git pull --ff-only origin main   # skip pull if nothing remote yet
git switch -c phase/NN-short-name

# ... planned commits (see §5) ...

# finish: full verification first
#   backend:  dotnet test   OR  docker compose --profile test run --rm backend-tests
#   frontend: npm test -- --run && npm run typecheck   (from Phase 09 on)
#   docker:   docker compose up --build -d && curl -f http://localhost:8080/health
git push -u origin phase/NN-short-name
git switch main
git merge --no-ff phase/NN-short-name -m "merge: phase NN — <phase title>"
git push origin main
```

If a push fails (auth, network): stop, record it in `todo.md` → "Blockers & Questions for Me", and tell the human. Never force-push `main`.

## 5. Phase & commit plan

| Phase | Branch | Commits (in order) |
|-------|--------|--------------------|
| **00 — Repository bootstrap** | `phase/00-bootstrap` | 1. `chore(repo): initialize repository with gitignore, gitattributes and editorconfig`<br>2. `docs: add specification documents and agent guide` (docs/, CLAUDE.md)<br>3. `docs: add README, ASSUMPTIONS, AI_USAGE skeletons and project log` (todo.md) |
| **01 — Backend skeleton & Docker** | `phase/01-backend-skeleton` | 1. `chore(backend): scaffold clean-architecture solution and projects`<br>2. `feat(api): add program setup and health endpoint`<br>3. `build(docker): add backend Dockerfile and compose with PostgreSQL` |
| **02 — Domain & persistence** | `phase/02-domain-persistence` | 1. `feat(domain): add class and hold entities with state transitions`<br>2. `test(domain): cover hold state transitions with unit tests`<br>3. `feat(infra): add EF Core context and entity configurations`<br>4. `feat(infra): add initial migration and startup migrator` |
| **03 — Application plumbing** | `phase/03-app-plumbing` | 1. `feat(app): add CQRS abstractions and MediatR pipeline behaviors`<br>2. `feat(infra): add unit of work, repositories and constraint translation`<br>3. `feat(api): add problem-details errors, JSON conventions and CORS`<br>4. `test: add integration test harness with real PostgreSQL` |
| **04 — Classes** | `phase/04-classes` | 1. `feat(classes): add create-class command with validation`<br>2. `feat(classes): add class list and detail queries with seat accounting`<br>3. `test(classes): add contract and validation tests for class endpoints` |
| **05 — Holds & concurrency** ⭐ | `phase/05-holds-concurrency` | 1. `feat(holds): add create-hold command with class-row locking`<br>2. `test(holds): prove seat limit under concurrent apply bursts`<br>3. `test(holds): add duplicate-application and hold contract tests` |
| **06 — Hold lifecycle** | `phase/06-hold-lifecycle` | 1. `feat(holds): add hold status and my-holds queries`<br>2. `feat(holds): add confirm and cancel with ownership checks`<br>3. `test(holds): cover lifecycle, ownership and idempotent confirm` |
| **07 — Automatic expiry** | `phase/07-expiry` | 1. `feat(holds): add expiry command and background sweeper`<br>2. `test(holds): verify expiry timing with fake time provider` |
| **08 — Backend hardening** | `phase/08-hardening` | 1. `test(api): guarantee 4xx for invalid input on all endpoints`<br>2. `fix(api): <fixes found by the robustness matrix>` (or `refactor(api): …` if none)<br>3. `feat(app): mask student ids in logs and add request logging`<br>4. `test(infra): verify data persists across application restarts` |
| **09 — Frontend foundation** | `phase/09-frontend-foundation` | 1. `chore(frontend): scaffold Vite React TypeScript app`<br>2. `feat(frontend): add runtime config, API client and contract types`<br>3. `feat(frontend): add layout, routing, error boundary and query client`<br>4. `build(docker): serve frontend with nginx and add to compose` |
| **10 — Identity & classes UI** | `phase/10-classes-ui` | 1. `feat(frontend): add student ID bar with validation and persistence`<br>2. `feat(frontend): add class list with live seat counts and apply flow`<br>3. `feat(frontend): add admin create-class page` |
| **11 — Applications UI** | `phase/11-applications-ui` | 1. `feat(frontend): add application page with live countdown and payment`<br>2. `feat(frontend): add my-applications page`<br>3. `feat(frontend): add friendly error messages and connection banner` |
| **12 — Frontend tests** | `phase/12-frontend-tests` | 1. `test(frontend): set up Vitest, Testing Library and MSW`<br>2. `test(frontend): cover student ID, class polling and apply errors`<br>3. `test(frontend): cover expiry countdown, confirm flow and error boundary`<br>4. `build(docker): add frontend test service to compose` |
| **13 — Multi-instance bonus** | `phase/13-multi-instance` | 1. `build(docker): add multi-instance compose override`<br>2. `test(holds): verify seat limit across two app instances`<br>3. `chore(scripts): add two-instance concurrency smoke script` |
| **14 — Delivery** | `phase/14-delivery` | 1. `docs: write README with run and test instructions`<br>2. `docs: finalize assumptions with multi-instance explanation`<br>3. `docs: add AI usage report`<br>4. `fix: final QA fixes from clean-clone run` (only if needed) |

Mapping to `Implementation.md`: Phases 00–08 = Implementation phases 0–8; Phase 09 = Implementation 9; Phases 10–11 = Implementation 10.1–10.5; Phase 12 = 10.6; Phase 13 = 11.1; Phase 14 = 11.2–11.3.

## 6. Tracking in `todo.md`

The "Git History" section has one row per phase:

```markdown
| Phase | Branch | Commits | Tests | Pushed | Merged to main |
|-------|--------|---------|-------|--------|----------------|
| 05 — Holds & concurrency | phase/05-holds-concurrency | 3/3 (a1b2c3d, …) | ✅ 24 passed | ✅ | ✅ |
```

## 7. Don'ts

- ❌ Commits named `wip`, `update`, `fix stuff`, `final`.
- ❌ Committing directly to `main` (except the merge commits).
- ❌ `git push --force` on `main`, rewriting pushed history, fake commit dates.
- ❌ Committing the assignment PDF, `.env`, `bin/`, `obj/`, `node_modules/`, `dist/`.
- ❌ Merging a phase whose tests fail.
