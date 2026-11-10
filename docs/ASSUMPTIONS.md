# ASSUMPTIONS.md

Where the brief is silent, these are the choices made and why. (A copy of this file lives at the repository root, where reviewers expect it.)

## Identity & access
1. **No authentication anywhere, including admin endpoints.** The brief says "no login" and the API is tested directly; `X-Student-Id` is trusted after format checks.
2. **A valid student ID is exactly 17 ASCII digits** (surrounding whitespace trimmed). No checksum or birth-year check — the format rules weren't given and over-validation could reject real numbers. The UI also accepts Bengali digits (০–৯) and converts them.
3. **Missing/invalid `X-Student-Id` → 400**, not 401, since there is no auth scheme to challenge.
4. **`GET /holds/{id}` needs no header** (none listed in the brief). Hold IDs are random UUIDv7, so they aren't guessable.
5. **Confirming or cancelling someone else's hold → 403.**

## Classes
6. Name is required, trimmed, 1–100 characters, **unique case-insensitively** (two classes with the same name would be indistinguishable to parents), and may not contain control characters (PostgreSQL cannot store NUL, and line breaks or escape codes make no sense in a name shown to parents). `totalSeats` is an integer 1–10,000.
7. Classes cannot be edited or deleted — the contract has no such endpoints.
8. `GET /classes` is ordered by creation time.

## Holds
9. **One active-or-confirmed hold per student per class** (rule 3). Holds in *different* classes at the same time are allowed — the brief restricts only "in a class", and the API has no notion of grade.
10. After **cancel or expiry** a student may apply again to the same class (new hold, back of the line).
11. A hold is active while `now < expiresAt`; at exactly `expiresAt` it is expired.
12. **Seats return at `expiresAt` itself**, not when a background job runs: counts and statuses are computed from `expiresAt` at query time. A sweeper (every 5 s) only updates stored rows.
13. **Confirm is idempotent:** repeating it on a CONFIRMED hold returns 200 with the same body; it is never counted twice.
14. Confirm on an expired hold → 409 `HOLD_EXPIRED`; on a cancelled hold → 409 `HOLD_CANCELLED`.
15. **DELETE** cancels a HELD hold (204). Repeating it → 204. Cancelling an EXPIRED hold → 409; cancelling a CONFIRMED admission → 409 (refunds are out of scope). Rows are kept with status CANCELLED, never physically deleted, so "My Applications" shows history.
16. Payment is simulated with no amount — pressing "Pay" means success.

## API details
17. IDs are UUIDv7 strings (the brief allows any format). A malformed ID in a path → 404, as for an unknown ID.
18. Errors use RFC 9457 `application/problem+json` with an extra stable `code` field.
19. Only *additional* response fields were added (`className`, `createdAt`, `confirmedAt`, `serverTime`, …); no contract field was renamed or removed. `serverTime` lets the UI correct client clock skew in the countdown.
20. Timestamps are UTC ISO-8601 with `Z` and whole seconds, matching the brief's example; `expiresAt = createdAt (truncated to the second) + HOLD_DURATION_SECONDS`.
21. Database unavailable → 503 (it's not an input error, so rule 7 doesn't apply, but it's never a bare 500).

## Platform
22. **.NET 10 (LTS)** instead of .NET 8: the brief allows newer, and .NET 8 support ends on 10 Nov 2026 — one day before the deadline.
23. MediatR is pinned to **12.x** (Apache-2.0) and AutoMapper isn't used; newer versions require a commercial license key.
24. PostgreSQL 17 with a named volume; `docker compose down` keeps data, only `down -v` deletes it. The DB port isn't published to the host to avoid clashing with a local Postgres.
25. The frontend is served by nginx on **http://localhost:5173**; the backend URL is injected at container start (`API_BASE_URL` → `/config.js`), so it can be changed without rebuilding.

## UI
26. UI text is English (all strings centralized for easy translation); Bengali digits are accepted in the ID box.
27. The admin "Create Class" screen is an unprotected page in the same app.
28. Seat counts poll every 3 s, an open application every 5 s, and the countdown ticks locally every 250 ms, switching to EXPIRED the moment it reaches zero.
29. **The application page decides "is this mine?" from My Applications**, because `GET /holds/{id}` has no owner field (and the contract can't change). If the saved number's list doesn't contain the hold, the page is read-only ("belongs to a different birth registration number"); with no number saved it asks for one. The server's 403 `HOLD_NOT_OWNED` stays the real check.

## Bonus: why the seat limit holds with two backend processes
All coordination happens inside PostgreSQL; the backend keeps no in-memory locks, caches or counters.
- **Creating a hold** runs in a transaction that first locks the class row (`SELECT … FROM classes WHERE id = $1 FOR UPDATE`). Every applicant to that class — from any process, on any port — waits on that same row lock. Inside the lock we count `HELD (not expired) + CONFIRMED` and insert only if below `totalSeats`. The lock is released at commit, so the next applicant's count sees the new row.
- Confirm, cancel and expiry never *increase* `held + confirmed`, so they don't need the class lock; they lock only the hold row (which also makes confirm idempotent under races).
- A **partial unique index** on `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` enforces rule 3 at the database level.
- The expiry sweeper uses `FOR UPDATE SKIP LOCKED`, so several instances can sweep concurrently without blocking or deadlocking.
- Startup migrations are serialized with a PostgreSQL advisory lock.

Demonstrated by `docker-compose.multi.yml` (second backend on :8081), `scripts/concurrency-smoke.sh`, and the `TwoInstances_ShareDatabase_NeverOversell` integration test.
