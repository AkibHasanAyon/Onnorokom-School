# ASSUMPTIONS.md

Where the brief is silent, these are the choices made and why. (The same list is kept in `docs/ASSUMPTIONS.md` with the specification.)

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
30. **The number in the ID box is the identity.** A valid number is saved on this device (`localStorage`) and used at once; an empty or invalid box means "no student", so nothing is ever done for a number the parent can't see. Other open tabs follow a change.

## Bonus: why the seat limit holds with two backend processes
All coordination happens inside PostgreSQL; a backend process keeps no locks, caches or counters in memory, so any number of them can share one database without sticky sessions.
- **Creating a hold** runs in one READ COMMITTED transaction that first locks the class row (`SELECT … FROM classes WHERE id = $1 FOR UPDATE`). Every applicant to that class — from any process, on any port — waits on that same row lock. Inside the lock the backend counts `HELD (not expired) + CONFIRMED` and inserts only if that is below `totalSeats`. The lock is released at commit, so the next applicant's count already sees the new row. Different classes don't wait for each other.
- **Confirm, cancel and expiry never increase** `held + confirmed`, so they don't need the class lock; they lock only the hold row, which also makes concurrent confirms count once.
- A **partial unique index** on `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` enforces "one active application per student per class" in the database itself.
- The **expiry sweeper** in every instance uses `FOR UPDATE SKIP LOCKED`, so instances sweep side by side without blocking or deadlocking. Seat counts never depend on it (item 12).
- **Start-up migrations** are serialized with a PostgreSQL advisory lock, so two instances can start at the same moment on an empty database.
- **Assumption:** the instances' clocks agree (same host, or NTP-synced servers). "Now" comes from each backend's clock; a clock that runs *ahead* would treat a hold as expired early and could give its seat away. In Docker on one host they share the host clock.

Demonstrated by:
- `docker-compose.multi.yml` (a second backend on :8081) with `scripts/concurrency-smoke.sh`: 300 simultaneous applications for 20 seats, alternating between the two ports → exactly 20 × 201, 280 × 409, no 5xx.
- The `TwoInstanceTests` integration tests (two independent hosts, each with its own connection pool, on one database), including `TwoInstances_ShareDatabase_NeverOversell`: 200 applications for 30 seats → exactly 30 holds. With the row lock removed, the same test gets 41 holds for 30 seats, so it really detects overselling.
