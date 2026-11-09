# Setup.md

> The root `README.md` must contain at least §1–§3 of this file (the brief requires: how to run with Docker, which URL the frontend opens on, and how to run tests).

## 1. Quick start (only Docker required)

Prerequisite: Docker Engine/Desktop with Compose v2 (`docker compose version`). Ports **8080** and **5173** must be free.

```bash
git clone <repo-url> school-admission
cd school-admission
docker compose up --build
```

| What | URL |
|------|-----|
| **Frontend (open this)** | **http://localhost:5173** |
| Backend API | http://localhost:8080 |
| Health | http://localhost:8080/health |

First start takes a few minutes (image builds). The backend waits for Postgres to be healthy and applies migrations automatically.

Stop: `Ctrl+C` or `docker compose down` (data is kept). Wipe all data: `docker compose down -v`.

Try short holds for a demo:
```bash
HOLD_DURATION_SECONDS=45 docker compose up --build
```
(PowerShell: `$env:HOLD_DURATION_SECONDS=45; docker compose up --build`)

## 2. Running tests (Docker only)

```bash
docker compose --profile test run --rm backend-tests     # xUnit: unit + integration (real Postgres) incl. concurrency
docker compose --profile test run --rm frontend-tests    # Vitest + React Testing Library
```

## 3. Using the app (60-second tour)

1. **Admin: Create Class** → "Class 6 - Morning Shift", 120 seats.
2. Enter a 17-digit birth registration number in the top bar (e.g. `20152691234567890`).
3. **Classes** → **Apply** → you get a held seat with a countdown.
4. **Pay Admission Fee** (simulated) → CONFIRMED, or let the timer run out → EXPIRED.
5. **My Applications** lists everything for that number, even after closing the browser.

## 4. Environment variables

| Variable | Service | Default | Purpose |
|----------|---------|---------|---------|
| `ConnectionStrings__Default` | backend | compose value (`Host=db;...`) | PostgreSQL connection (brief: must be changeable via env) |
| `HOLD_DURATION_SECONDS` | backend | `600` | Hold length in seconds (1–86400) |
| `HOLD_SWEEP_INTERVAL_SECONDS` | backend | `5` | How often stored statuses are updated to EXPIRED |
| `HOLD_SWEEPER_ENABLED` | backend | `true` | Tests set `false` |
| `CORS_ALLOWED_ORIGINS` | backend | `http://localhost:5173,http://127.0.0.1:5173` | Comma-separated allowed browser origins |
| `ASPNETCORE_HTTP_PORTS` | backend | `8080` | Listen port inside the container |
| `API_BASE_URL` | frontend (runtime) | `http://localhost:8080` | Backend URL the **browser** calls (written to `/config.js` at container start) |
| `VITE_API_BASE_URL` | frontend (dev/build) | — | Used by `npm run dev` if `/config.js` has no value |
| `TEST_DB_CONNECTION` | tests | `Host=localhost;Port=5433;Database=postgres;Username=admission;Password=admission` | Admin connection used to create throwaway test DBs |
| `POSTGRES_DB/USER/PASSWORD` | db | `admission` | Dev-only credentials |

`.env.example` (copy to `.env` to override; compose reads it automatically):
```dotenv
HOLD_DURATION_SECONDS=600
HOLD_SWEEP_INTERVAL_SECONDS=5
CORS_ALLOWED_ORIGINS=http://localhost:5173,http://127.0.0.1:5173
API_BASE_URL=http://localhost:8080
```

Changing the backend URL for the frontend (no rebuild needed):
```bash
API_BASE_URL=http://localhost:9090 docker compose up -d frontend
```

## 5. Local development (optional; needs .NET 10 SDK + Node 22)

`docker-compose.dev.yml`:
```yaml
services:
  db:
    ports:
      - "5433:5432"
```

```bash
# Postgres only, exposed on localhost:5433
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db

# Backend (http://localhost:8080)
cd backend
export ConnectionStrings__Default="Host=localhost;Port=5433;Database=admission;Username=admission;Password=admission"
export ASPNETCORE_HTTP_PORTS=8080
dotnet run --project src/SchoolAdmission.Api

# Backend tests
dotnet test

# Frontend (http://localhost:5173, hot reload)
cd ../frontend
npm ci
VITE_API_BASE_URL=http://localhost:8080 npm run dev
npm test            # watch mode; `npm test -- --run` for single run
npm run typecheck
```

EF tooling:
```bash
dotnet tool install --global dotnet-ef
cd backend
dotnet ef migrations add <Name> -p src/SchoolAdmission.Infrastructure -s src/SchoolAdmission.Api -o Persistence/Migrations
```

## 6. Two backend instances (bonus B1)

`docker-compose.multi.yml`:
```yaml
services:
  backend-2:
    build: ./backend
    environment:
      ASPNETCORE_HTTP_PORTS: "8080"
      ConnectionStrings__Default: "Host=db;Port=5432;Database=admission;Username=admission;Password=admission;Maximum Pool Size=100;Timeout=15;Command Timeout=30"
      HOLD_DURATION_SECONDS: ${HOLD_DURATION_SECONDS:-600}
      CORS_ALLOWED_ORIGINS: ${CORS_ALLOWED_ORIGINS:-http://localhost:5173,http://127.0.0.1:5173}
    ports:
      - "8081:8080"
    depends_on:
      db:
        condition: service_healthy
```

```bash
docker compose -f docker-compose.yml -f docker-compose.multi.yml up -d --build
./scripts/concurrency-smoke.sh            # SEATS=20 N=300 by default
```

`scripts/concurrency-smoke.sh` (needs bash + curl; LF line endings):
```bash
#!/usr/bin/env bash
set -euo pipefail
API1="${API1:-http://localhost:8080}"
API2="${API2:-http://localhost:8081}"
SEATS="${SEATS:-20}"
N="${N:-300}"

CLASS_ID=$(curl -fsS -X POST "$API1/classes" -H 'Content-Type: application/json' \
  -d "{\"name\":\"Smoke $(date +%s%N)\",\"totalSeats\":$SEATS}" | sed -E 's/.*"id":"([^"]+)".*/\1/')
echo "Class $CLASS_ID with $SEATS seats; firing $N requests at two instances..."
export CLASS_ID API1 API2

seq 1 "$N" | xargs -P 100 -I{} bash -c '
  i={}; if (( i % 2 )); then api=$API1; else api=$API2; fi
  sid=$(printf "2016%013d" "$i")
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "$api/classes/$CLASS_ID/holds" -H "X-Student-Id: $sid"
' | sort | uniq -c

echo "Final state:"; curl -fsS "$API1/classes/$CLASS_ID"; echo
```
Expected: exactly `$SEATS` × `201`, the rest `409`, no `5xx`, and `heldSeats == $SEATS`.

## 7. Useful commands

```bash
docker compose ps
docker compose logs -f backend
docker compose exec db psql -U admission -d admission
docker compose restart backend
docker compose build --no-cache frontend
```
