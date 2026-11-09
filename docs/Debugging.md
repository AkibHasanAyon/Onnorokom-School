# Debugging.md

## 1. Protocol for AI agents

1. **Reproduce** with the smallest command (one `curl`, one test via `dotnet test --filter`, one Vitest file).
2. **Read the actual error** (logs, problem+json `code` and `traceId`, test output). Don't guess.
3. **Write a failing test** that captures the bug (if it's a rule violation, it belongs in the integration suite).
4. **Fix the cause**, not the symptom. Never "fix" a contract test by changing the expected field name or status code — the contract is frozen.
5. Run the full suites. Report what was wrong and suggest an `AI_USAGE.md` entry if your earlier code caused it.

## 2. First-look commands

```bash
docker compose ps                                   # all three Up? db (healthy)?
docker compose logs -f backend                      # startup, migrations, exceptions
docker compose logs frontend | grep runtime-config  # which API_BASE_URL the SPA got
curl -i http://localhost:8080/health
curl -s http://localhost:5173/config.js
docker compose exec db psql -U admission -d admission -c '\dt'
```

## 3. Symptom → cause → fix

### Startup / Docker
| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Another process on 8080/5173 | Stop it; the 8080 port is mandated, don't change it |
| Backend exits: `ConnectionStrings__Default is not set` | Env var missing/misspelled (double underscore!) | Fix compose env |
| Backend loops `Failed to connect to db:5432` | DB not healthy yet / wrong host | Check `depends_on: condition: service_healthy`; use `Host=db` inside compose, `localhost:5433` outside |
| `OptionsValidationException: HOLD_DURATION_SECONDS must be 1..86400` | Bad env value | Fix value; this is intended fail-fast |
| `The model for context 'AdmissionDbContext' has pending changes` | Model changed without migration (EF 9+ blocks migrate) | `dotnet ef migrations add <Name> ...` and commit it |
| `relation "classes" does not exist` | Migrations not applied / not committed | Ensure `MigrateDatabaseAsync` runs before `app.Run()`; migrations folder committed |
| Data gone after restart | Used `down -v`, or volume path wrong | Only `down` (no `-v`); volume at `/var/lib/postgresql/data` with `postgres:17` |
| Postgres 18 container ignores the volume | v18 moved PGDATA | We pin `postgres:17-alpine` |
| Frontend container: `/docker-entrypoint.d/40-runtime-config.sh: not found` or `$'\r': command not found` | CRLF line endings (Windows checkout) | `.gitattributes`: `*.sh text eol=lf`; re-checkout; `chmod +x` in Dockerfile |
| `npm ci` fails in build | `package-lock.json` missing/out of sync | Commit the lockfile; run `npm install` locally once |

### Frontend ↔ backend
| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Browser console: `blocked by CORS policy` | Origin not in `CORS_ALLOWED_ORIGINS`, missing `X-Student-Id` in allowed headers, or `UseCors` after `MapControllers` | See `Integration.md §4` |
| All requests go to `http://backend:8080` and fail | Container hostname used for browser traffic | `API_BASE_URL=http://localhost:8080` |
| Changing `API_BASE_URL` has no effect | Cached `config.js` or value baked at build time | `config.js` is `no-store`; ensure `index.html` loads `/config.js`; restart frontend container |
| Refresh on `/applications/<id>` → nginx 404 | Missing SPA fallback | `try_files $uri $uri/ /index.html;` |
| Countdown off by minutes | Client clock skew | Use `serverTime` offset (`Integration.md §7`) |
| Counts don't refresh | `refetchInterval` missing, or tab in background (`refetchIntervalInBackground: false` is intended) | Check hook config; focus the tab |
| Blank page | Uncaught render error | `ErrorBoundary` must wrap routes; check console for the original error |
| Error shows `HTTP_409` text | Unknown `code` in `messages.ts` | Add the mapping |

### API behavior
| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| 500 on malformed JSON | Default model-binding error not mapped, or non-nullable request properties | `InvalidModelStateResponseFactory`; nullable request DTO properties |
| 500 `Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'` | Non-UTC timestamp | Use `TimeProvider.GetUtcNow()` (`DateTimeOffset`, offset 0) everywhere |
| `expiresAt` like `2026-11-01T04:10:00.1234567+00:00` | Default serializer | Register `UtcDateTimeOffsetConverter` (`yyyy-MM-dd'T'HH:mm:ss'Z'`) |
| `status` is `"Held"` or `0` | Enum serialized directly | DTOs carry status as uppercase string |
| 400 instead of 404 for unknown id (or vice versa) | Check order mismatch | Follow `Implementation.md §R8` (validator first, then parse id in handler) |
| 500 `23505 duplicate key ... ux_holds_class_student_active` | Unique violation not translated | `UnitOfWork.SaveChangesAsync` maps it to 409 |
| Re-apply after expiry returns 409 `ACTIVE_HOLD_EXISTS` | Stale stored-HELD row blocks unique index | Handler must call `ExpireStaleHoldForStudentAsync` before checking/inserting |

### Concurrency
| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Concurrency test: more than N × 201 | Lock not taken or taken outside the transaction; count before lock; EF `FromSql` composed so `FOR UPDATE` was dropped | Verify order in `CreateHoldCommandHandler`; `TransactionBehavior` wraps it; use `.ToListAsync()` without LINQ operators on `FromSql` |
| Fewer than N × 201 (some 503) | Pool timeout / `too many clients` | Pool 100 per host; `max_connections=300`; concurrency tests in one xUnit collection |
| `40P01 deadlock detected` | Multi-row lock in wrong order | Sweeper must use `FOR UPDATE SKIP LOCKED`; create must lock class first |
| `40001 could not serialize access` | Someone set SERIALIZABLE | Use default READ COMMITTED |
| `InvalidOperationException: execution strategy ... does not support user-initiated transactions` | `EnableRetryOnFailure()` enabled | Remove it |
| Flaky expiry tests | Real clock used somewhere | Search for `DateTime.UtcNow`/`DateTimeOffset.UtcNow`; inject `TimeProvider` |

## 4. Diagnostic SQL

```sql
-- Invariant violations (must be empty) — see Database.md §10 for full query
-- Who holds/waits on locks right now
SELECT pid, state, wait_event_type, wait_event, left(query, 80) AS query, now() - query_start AS age
FROM pg_stat_activity WHERE datname = 'admission' ORDER BY query_start;

SELECT l.locktype, l.relation::regclass, l.mode, l.granted, a.pid, left(a.query, 60)
FROM pg_locks l JOIN pg_stat_activity a USING (pid)
WHERE a.datname = 'admission' AND l.relation IS NOT NULL;

-- Connection usage
SELECT count(*) FROM pg_stat_activity;

-- A student's history
SELECT id, class_id, status, created_at, expires_at, confirmed_at, cancelled_at
FROM holds WHERE student_id = '20152691234567890' ORDER BY created_at DESC;
```

## 5. Logging switches

- EF SQL: set `Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information` on the backend service.
- Verbose app logs: `Logging__LogLevel__SchoolAdmission=Debug`.
- Every problem response includes `traceId`; search logs for it.
- Student IDs appear masked as `*************7890`.

## 6. Resetting

```bash
docker compose down -v && docker compose up --build   # clean slate
docker compose build --no-cache backend               # stale image
```
