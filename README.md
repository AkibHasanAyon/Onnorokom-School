# School Admission System

First-come-first-served online school admission: admins create classes (one per shift) with a fixed seat count; parents apply with the student's 17-digit birth registration number, which holds a seat for a limited time until the (simulated) admission fee is paid. Seats are never oversold, even under thousands of simultaneous applications.

Stack: .NET 10 (ASP.NET Core, MediatR CQRS, EF Core) · PostgreSQL 17 · React 19 + TypeScript + Vite · Docker Compose.

> 🚧 Work in progress. Full run and test instructions are added in the final delivery phase.

## Quick start (only Docker required)

```bash
docker compose up --build
```

| What | URL |
|------|-----|
| Frontend | http://localhost:5173 *(added in a later phase)* |
| Backend API | http://localhost:8080 |
| Health | http://localhost:8080/health |

## Running tests

```bash
docker compose --profile test run --rm backend-tests
docker compose --profile test run --rm frontend-tests
```

## Documentation

- [ASSUMPTIONS.md](ASSUMPTIONS.md) — decisions where the brief is silent
- [AI_USAGE.md](AI_USAGE.md) — how AI tools were used
- [docs/](docs/) — specification, API contract, architecture
