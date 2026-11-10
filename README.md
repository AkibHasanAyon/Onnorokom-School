# School Admission System

First-come-first-served online school admission. An admin creates classes (each shift is its own class, e.g. *Class 6 - Morning Shift*, 120 seats). A parent types the student's 17-digit birth registration number and applies: that **holds** one seat for a limited time (10 minutes by default). Paying the (simulated) admission fee within that time **confirms** the admission; otherwise the hold **expires on its own** and the seat goes back to the pool. Seats are never oversold, even when thousands of parents apply at the same moment, and even with two backend processes sharing one database.

Stack: .NET 10 (ASP.NET Core, MediatR CQRS, EF Core + Npgsql) · PostgreSQL 17 · React 19 + TypeScript + Vite (TanStack Query) · nginx · Docker Compose.

## Quick start (only Docker needed)

Requires Docker Desktop / Docker Engine with Compose v2. Ports **5173** and **8080** must be free.

```bash
git clone <this-repo-url> school-admission
cd school-admission
docker compose up --build
```

| What | URL |
|------|-----|
| **Frontend — open this** | **http://localhost:5173** |
| Backend API | http://localhost:8080 |
| Health check | http://localhost:8080/health |

The first start takes a few minutes (images are built). The backend waits for PostgreSQL and creates the schema itself.

- Stop: `Ctrl+C`, or `docker compose down`. **Data is kept** (named volume) and is there again after the next `docker compose up`.
- Delete all data: `docker compose down -v`.

**Short holds for a demo** (watch a hold expire live):

```bash
HOLD_DURATION_SECONDS=45 docker compose up --build          # bash / zsh
$env:HOLD_DURATION_SECONDS=45; docker compose up --build    # PowerShell
```

## Running the tests (Docker only)

```bash
docker compose --profile test run --rm backend-tests    # xUnit: unit + integration tests against a real PostgreSQL
docker compose --profile test run --rm frontend-tests   # TypeScript typecheck + Vitest / React Testing Library
```

Both commands exit non-zero if any test fails. The backend suite starts the database itself; it includes the concurrency tests (hundreds of simultaneous applications never produce more holds than seats), expiry with a controllable clock, the API contract, and a matrix of malformed inputs that must never return a 500. The frontend suite runs against a mocked API (MSW).

**Two backend instances on one database** (bonus):

```bash
docker compose -f docker-compose.yml -f docker-compose.multi.yml up -d --build   # second backend on :8081
./scripts/concurrency-smoke.sh      # 300 simultaneous applications for 20 seats, alternating :8080 / :8081
```

The script prints `PASS` only if exactly 20 applications got a seat, all others got a clean "class full" 409, and nothing failed (needs bash and curl; on Windows use Git Bash). Why this holds is explained in [ASSUMPTIONS.md](ASSUMPTIONS.md#bonus-why-the-seat-limit-holds-with-two-backend-processes).

## Using the app (one-minute tour)

1. **Admin: Create Class** → "Class 6 - Morning Shift", 2 seats.
2. Type a birth registration number in the box at the top, e.g. `20152691234567890` (Bengali digits work too). It is remembered on this device.
3. **Classes** → **Apply**. You land on the application page: status `HELD` and a countdown.
4. **Pay Admission Fee** (simulated: the click counts as payment) → `CONFIRMED`. Or wait: at 0 the page shows `EXPIRED` and disables Pay without a reload, and the seat is free again.
5. **My Applications** lists every application for the number, even after closing the browser.

Seat numbers refresh every 3 seconds, so a second browser window (with another number) shows other parents' holds appear and disappear. If the backend stops, a banner says so and the last data stays on screen; it disappears by itself when the backend is back.

## API

Base URL `http://localhost:8080`, JSON, no authentication. The student is identified by the `X-Student-Id` header (17 digits). Errors are `application/problem+json` with a stable `code` (e.g. `NO_SEATS_AVAILABLE`). Full contract: [docs/API.md](docs/API.md).

| Method | Path | Purpose | Success |
|--------|------|---------|---------|
| POST | `/classes` | Create a class `{ "name", "totalSeats" }` | 201 |
| GET | `/classes` | All classes with `totalSeats`, `heldSeats`, `confirmedSeats`, `availableSeats` | 200 |
| GET | `/classes/{classId}` | One class with seat counts | 200 |
| POST | `/classes/{classId}/holds` | Apply: hold a seat (`X-Student-Id`) | 201, **409 when full** |
| GET | `/holds/{holdId}` | Hold status: `HELD`, `CONFIRMED`, `EXPIRED` or `CANCELLED` | 200 |
| POST | `/holds/{holdId}/confirm` | Pay the fee (`X-Student-Id`); repeating it is harmless | 200 |
| DELETE | `/holds/{holdId}` | Cancel an application (`X-Student-Id`); the seat is released at once | 204 |
| GET | `/me/holds` | All applications of the student (`X-Student-Id`) | 200 |
| GET | `/health` | Liveness + database check, server time | 200 / 503 |

```bash
curl -s -X POST localhost:8080/classes -H 'Content-Type: application/json' -d '{"name":"Class 6 - Morning Shift","totalSeats":2}'
curl -s -X POST localhost:8080/classes/<classId>/holds -H 'X-Student-Id: 20152691234567890'
curl -s -X POST localhost:8080/holds/<holdId>/confirm -H 'X-Student-Id: 20152691234567890'
```

## How "never oversold" is guaranteed

Every application runs in one PostgreSQL transaction that first locks the class row (`SELECT … FOR UPDATE`), then counts held-and-not-expired plus confirmed holds, and inserts only if a seat is left. All applicants for a class, from any backend process, queue on that one lock, so the count can't be read twice. A partial unique index enforces one active application per student per class. Expiry needs no job: counts are computed from `expiresAt`, so a seat is free the moment its hold runs out; a background sweeper only updates the stored status. The backend keeps no state in memory, which is why two instances work. Details: [docs/Architecture.md §6](docs/Architecture.md).

## Configuration

Set in the shell, or in a `.env` file next to `docker-compose.yml` (see [.env.example](.env.example)).

| Variable | Default | Purpose |
|----------|---------|---------|
| `HOLD_DURATION_SECONDS` | `600` | How long a hold lasts (1–86400) |
| `HOLD_SWEEP_INTERVAL_SECONDS` | `5` | How often expired holds get their stored status updated |
| `CORS_ALLOWED_ORIGINS` | `http://localhost:5173,http://127.0.0.1:5173` | Browser origins allowed to call the API |
| `API_BASE_URL` | `http://localhost:8080` | Backend URL the **browser** uses; applied at container start, no rebuild needed |
| `ConnectionStrings__Default` | compose value | PostgreSQL connection string of the backend |

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `port is already allocated` (5173 or 8080) | Stop whatever uses the port, or change the left side of `ports:` in `docker-compose.yml` (for 8080 also set `API_BASE_URL` and `CORS_ALLOWED_ORIGINS`). |
| Page loads but shows "Can't reach the server" | The backend is still starting or stopped: `docker compose ps`, `docker compose logs backend`. |
| Want a clean slate | `docker compose down -v && docker compose up --build` |
| `concurrency-smoke.sh` says SETUP PROBLEM | Start both backends with the multi-instance command above first. |

## Repository layout

```
backend/    .NET solution: src/{Domain, Application, Infrastructure, Api}, tests/{UnitTests, IntegrationTests}
frontend/   React app (src/api, hooks, pages, components, student, i18n), nginx config, tests next to the code
scripts/    concurrency-smoke.sh (two-instance check)
docs/       specification: PRD, API contract, architecture, database, design, testing
docker-compose.yml        db + backend (:8080) + frontend (:5173); test services under the "test" profile
docker-compose.multi.yml  adds backend-2 on :8081 (same database)
docker-compose.dev.yml    exposes PostgreSQL on localhost:5433 for local, non-Docker development
```

Local development without Docker for the app itself (needs .NET 10 SDK and Node 22) is described in [docs/Setup.md §5](docs/Setup.md).

## More

- [ASSUMPTIONS.md](ASSUMPTIONS.md) — what was decided where the brief is silent, including the two-instance explanation
- [AI_USAGE.md](AI_USAGE.md) — how AI tools were used and where they were wrong
- [docs/](docs/) — full specification
