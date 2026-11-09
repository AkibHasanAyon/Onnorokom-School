# API.md — HTTP Contract (FROZEN)

> The evaluators run automated tests directly against this API. **Paths, methods, field names, casing, status values and success status codes are fixed by the assignment.** You may add fields to responses. You may never remove or rename the fields marked *(contract)*.

---

## 1. Conventions

| Item | Rule |
|------|------|
| Base URL | `http://localhost:8080` |
| Format | JSON in, JSON out. Request `Content-Type: application/json` where a body exists. JSON property names are `camelCase`. |
| Identity | Header `X-Student-Id`: the student's 17-digit birth registration number. Required on endpoints 4, 6, 7, 8. Surrounding whitespace is trimmed; the rest must match `^[0-9]{17}$`. |
| IDs | UUID v7 strings, lowercase with hyphens, e.g. `0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e`. (The brief's `c_123`/`h_789` are only examples; format is our choice.) A path ID that isn't a valid UUID → **404**. |
| Timestamps | UTC ISO-8601 with `Z`, **whole seconds**: `2026-11-01T04:10:00Z`. |
| Hold status | Exactly one of `"HELD"`, `"CONFIRMED"`, `"EXPIRED"`, `"CANCELLED"` (uppercase strings). |
| Effective status | A stored `HELD` hold whose `expiresAt` ≤ now is **always reported as `EXPIRED`**, even if the background sweeper hasn't updated the row yet. |
| Errors | `application/problem+json` (RFC 9457) with an extra `code` field. See §3. |
| Unknown JSON fields in requests | Ignored. |
| CORS | Allowed origins from `CORS_ALLOWED_ORIGINS`; allowed headers `Content-Type`, `X-Student-Id`; methods `GET, POST, DELETE`. |

---

## 2. Endpoint summary

| # | Method & Path | Header | Success | Purpose |
|---|---------------|--------|---------|---------|
| 1 | `POST /classes` | — | **201** | Create class |
| 2 | `GET /classes` | — | **200** | List classes with seat accounting |
| 3 | `GET /classes/{classId}` | — | **200** | One class's seat accounting |
| 4 | `POST /classes/{classId}/holds` | `X-Student-Id` | **201** | Apply (hold a seat). **409 if no seat** |
| 5 | `GET /holds/{holdId}` | — | **200** | Current hold status |
| 6 | `POST /holds/{holdId}/confirm` | `X-Student-Id` | **200** | Pay fee → confirm |
| 7 | `DELETE /holds/{holdId}` | `X-Student-Id` | **204** | Cancel application |
| 8 | `GET /me/holds` | `X-Student-Id` | **200** | All of this student's applications |
| + | `GET /health` | — | **200** / 503 | Extra (not in brief): liveness + server time |

---

## 3. Error format and catalog

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "No seats available",
  "status": 409,
  "detail": "All seats in 'Class 6 - Morning Shift' are currently held or confirmed.",
  "code": "NO_SEATS_AVAILABLE",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

Validation errors additionally carry `errors` (field → messages):

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more fields are invalid.",
  "code": "VALIDATION_FAILED",
  "errors": { "totalSeats": ["Total seats must be between 1 and 10000."] },
  "traceId": "..."
}
```

Some errors carry extra context fields (documented per endpoint), e.g. `existingHoldId`.

| HTTP | `code` | When |
|------|--------|------|
| 400 | `VALIDATION_FAILED` | Body fields missing/invalid |
| 400 | `MALFORMED_JSON` | Body is not valid JSON or a field has the wrong JSON type |
| 400 | `INVALID_STUDENT_ID` | `X-Student-Id` missing, empty, or not exactly 17 digits |
| 403 | `HOLD_NOT_OWNED` | Hold belongs to a different student |
| 404 | `CLASS_NOT_FOUND` | Unknown or malformed `classId` |
| 404 | `HOLD_NOT_FOUND` | Unknown or malformed `holdId` |
| 404 | `NOT_FOUND` | Unknown route |
| 405 | `METHOD_NOT_ALLOWED` | Wrong HTTP method on a known route |
| 409 | `CLASS_NAME_EXISTS` | A class with the same name (case-insensitive, trimmed) exists |
| 409 | `NO_SEATS_AVAILABLE` | `held + confirmed == totalSeats` |
| 409 | `ACTIVE_HOLD_EXISTS` | Student already has an active hold in this class (`existingHoldId` included) |
| 409 | `ALREADY_ADMITTED` | Student already has a CONFIRMED admission in this class (`existingHoldId` included) |
| 409 | `HOLD_EXPIRED` | Confirm/cancel on an expired hold |
| 409 | `HOLD_CANCELLED` | Confirm on a cancelled hold |
| 409 | `HOLD_ALREADY_CONFIRMED` | Cancel on a confirmed hold |
| 413 | `PAYLOAD_TOO_LARGE` | Body > 64 KB |
| 415 | `UNSUPPORTED_MEDIA_TYPE` | Body sent with a non-JSON content type |
| 500 | `INTERNAL_ERROR` | Bug. Must never be caused by client input |
| 503 | `SERVICE_UNAVAILABLE` | Database unreachable / connection pool exhausted / timeout |

---

## 4. Endpoints

### 4.1 `POST /classes` — create class

Request *(contract)*:
```json
{ "name": "Class 6 - Morning Shift", "totalSeats": 120 }
```

Validation:
- `name`: required string, trimmed, 1–100 characters after trimming.
- `totalSeats`: required JSON integer, 1–10000. (`"120"`, `120.5`, `null` → 400.)

Response **201 Created** *(contract)*, with `Location: /classes/{id}`:
```json
{ "id": "0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e", "name": "Class 6 - Morning Shift", "totalSeats": 120 }
```
`name` is returned trimmed.

Errors: 400 `VALIDATION_FAILED` / `MALFORMED_JSON`, 409 `CLASS_NAME_EXISTS`, 415.

```bash
curl -i -X POST http://localhost:8080/classes \
  -H 'Content-Type: application/json' \
  -d '{"name":"Class 6 - Morning Shift","totalSeats":120}'
```

---

### 4.2 `GET /classes` — list classes

Response **200** *(contract)* — array ordered by creation time ascending (empty array if none):
```json
[
  {
    "id": "0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e",
    "name": "Class 6 - Morning Shift",
    "totalSeats": 120,
    "heldSeats": 15,
    "confirmedSeats": 80,
    "availableSeats": 25
  }
]
```
- `heldSeats` = holds with status HELD and `expiresAt > now`.
- `confirmedSeats` = holds with status CONFIRMED.
- `availableSeats` = `max(0, totalSeats − heldSeats − confirmedSeats)`.

---

### 4.3 `GET /classes/{classId}` — one class

Response **200** *(contract)* — same object shape as one element of §4.2:
```json
{ "id": "...", "name": "Class 6 - Morning Shift", "totalSeats": 120, "heldSeats": 15, "confirmedSeats": 80, "availableSeats": 25 }
```
Errors: 404 `CLASS_NOT_FOUND`.

---

### 4.4 `POST /classes/{classId}/holds` — apply (hold a seat)

Header: `X-Student-Id: 20152691234567890`. No request body required (any body is ignored).

Response **201 Created** *(contract fields + extras)*, with `Location: /holds/{holdId}`:
```json
{
  "holdId": "0193a6f3-0a11-7c22-8e33-445566778899",
  "classId": "0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e",
  "studentId": "20152691234567890",
  "status": "HELD",
  "expiresAt": "2026-11-01T04:10:00Z",

  "className": "Class 6 - Morning Shift",
  "createdAt": "2026-11-01T04:00:00Z",
  "serverTime": "2026-11-01T04:00:00Z"
}
```
Contract fields: `holdId`, `classId`, `studentId`, `status`, `expiresAt`. Extras: `className`, `createdAt`, `serverTime`.

`expiresAt = createdAt + HOLD_DURATION_SECONDS`, where `createdAt` is server time truncated to whole seconds.

Check order (first failure wins):
1. 400 `INVALID_STUDENT_ID`
2. 404 `CLASS_NOT_FOUND`
3. 409 `ALREADY_ADMITTED` (+ `existingHoldId`)
4. 409 `ACTIVE_HOLD_EXISTS` (+ `existingHoldId`)
5. **409 `NO_SEATS_AVAILABLE`** *(contract: "Seat না থাকলে 409")*

```bash
curl -i -X POST http://localhost:8080/classes/<classId>/holds -H 'X-Student-Id: 20152691234567890'
```

---

### 4.5 `GET /holds/{holdId}` — hold status

No header required (the brief lists none). The header is ignored if sent.

Response **200** *(contract fields + extras)*:
```json
{
  "holdId": "0193a6f3-0a11-7c22-8e33-445566778899",
  "classId": "0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e",
  "status": "HELD",
  "expiresAt": "2026-11-01T04:10:00Z",

  "className": "Class 6 - Morning Shift",
  "createdAt": "2026-11-01T04:00:00Z",
  "confirmedAt": null,
  "cancelledAt": null,
  "serverTime": "2026-11-01T04:03:12Z"
}
```
Contract fields: `holdId`, `classId`, `status`, `expiresAt`. `status` is the **effective** status (§1).

Errors: 404 `HOLD_NOT_FOUND`.

---

### 4.6 `POST /holds/{holdId}/confirm` — pay fee, confirm admission

Header: `X-Student-Id`. No body. Payment is simulated — calling this means payment succeeded.

Response **200** *(contract fields + extras)*:
```json
{ "holdId": "0193a6f3-0a11-7c22-8e33-445566778899", "status": "CONFIRMED", "classId": "0193a6f2-...", "confirmedAt": "2026-11-01T04:05:41Z" }
```
Contract fields: `holdId`, `status`.

**Idempotent:** confirming an already-CONFIRMED hold (by its owner) returns **200** with the same body; `confirmedSeats` does not change (R5).

Check order:
1. 400 `INVALID_STUDENT_ID`
2. 404 `HOLD_NOT_FOUND`
3. 403 `HOLD_NOT_OWNED`
4. Already CONFIRMED → 200 (idempotent)
5. 409 `HOLD_CANCELLED`
6. 409 `HOLD_EXPIRED` (stored EXPIRED, or HELD with `expiresAt ≤ now`)
7. HELD and active → CONFIRMED, 200

---

### 4.7 `DELETE /holds/{holdId}` — cancel application

Header: `X-Student-Id`. Response **204 No Content** *(contract)*, empty body.

Check order:
1. 400 `INVALID_STUDENT_ID`
2. 404 `HOLD_NOT_FOUND`
3. 403 `HOLD_NOT_OWNED`
4. Already CANCELLED → 204 (idempotent)
5. 409 `HOLD_ALREADY_CONFIRMED`
6. 409 `HOLD_EXPIRED`
7. HELD and active → CANCELLED, 204 (seat returns immediately)

The row is kept (status CANCELLED), never physically deleted.

---

### 4.8 `GET /me/holds` — my applications

Header: `X-Student-Id`.

Response **200** *(contract fields + extras)* — newest first; empty array if none:
```json
[
  {
    "holdId": "0193a6f3-0a11-7c22-8e33-445566778899",
    "classId": "0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e",
    "className": "Class 6 - Morning Shift",
    "status": "HELD",
    "expiresAt": "2026-11-01T04:10:00Z",

    "createdAt": "2026-11-01T04:00:00Z"
  }
]
```
Contract fields: `holdId`, `classId`, `className`, `status`, `expiresAt`. Includes all statuses. `status` is effective.

Errors: 400 `INVALID_STUDENT_ID`.

---

### 4.9 `GET /health` (extra)

```json
{ "status": "ok", "serverTime": "2026-11-01T04:03:12Z" }
```
Runs `SELECT 1`. If the DB is unreachable → 503 `{ "status": "unavailable", "serverTime": "..." }`.

---

## 5. Status code matrix

| Situation | 1 POST classes | 2 GET classes | 3 GET class | 4 POST hold | 5 GET hold | 6 confirm | 7 DELETE | 8 me/holds |
|---|---|---|---|---|---|---|---|---|
| Success | 201 | 200 | 200 | 201 | 200 | 200 | 204 | 200 |
| Bad body / JSON | 400 | – | – | – | – | – | – | – |
| Bad/missing header | – | – | – | 400 | – | 400 | 400 | 400 |
| Unknown/malformed ID | – | – | 404 | 404 | 404 | 404 | 404 | – |
| Not owner | – | – | – | – | – | 403 | 403 | – |
| Business conflict | 409 | – | – | 409 | – | 409 | 409 | – |
| DB down | 503 | 503 | 503 | 503 | 503 | 503 | 503 | 503 |

---

## 6. Contract compliance checklist (run before every commit touching the API)

- [ ] Field names exactly: `id, name, totalSeats, heldSeats, confirmedSeats, availableSeats, holdId, classId, studentId, className, status, expiresAt`.
- [ ] `status` values uppercase.
- [ ] `expiresAt` ends with `Z`, no fractional seconds.
- [ ] 201 on both creates; 204 with empty body on delete; 409 when full.
- [ ] Contract integration tests (`tests/.../Contract/`) pass.
- [ ] `frontend/src/api/types.ts` still matches.
