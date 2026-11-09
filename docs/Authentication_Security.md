# Authentication_Security.md

## 1. Identity model (as mandated by the brief)

- **There is no login.** The brief says so explicitly, and the evaluators' automated tests call every endpoint without credentials.
- The student's identity is the **17-digit birth registration number**, typed into a box at the top of the UI and sent as the `X-Student-Id` header.
- `X-Student-Id` is an **identity claim, not authentication**. The server trusts it as-is after format validation.
- The school admin screen is an ordinary, unprotected page.

> 🚫 **Agents: do not add authentication, API keys, admin passwords, JWT, cookies, or per-IP rate limiting.** Any of these would break the frozen contract or the evaluators' concurrency bursts (which come from a single IP).

## 2. Header validation

| Input | Result |
|-------|--------|
| Missing / empty | 400 `INVALID_STUDENT_ID` |
| After trimming whitespace, not exactly 17 ASCII digits | 400 `INVALID_STUDENT_ID` |
| Valid | Used as-is (trimmed) |

No checksum/birth-year validation (format rules weren't given; over-validating could reject real numbers). The DB has a matching `CHECK (student_id ~ '^[0-9]{17}$')` as defense in depth. The UI converts Bengali digits to ASCII before sending; the API itself accepts only ASCII digits.

## 3. Authorization rules (ownership)

| Action | Who may do it |
|--------|---------------|
| Create class, list/get classes | Anyone |
| View one hold (`GET /holds/{id}`) | Anyone with the ID (brief lists no header). IDs are random UUIDv7 → not enumerable in practice. |
| Apply | Any valid student ID |
| Confirm / cancel | Only the student whose ID created the hold → otherwise 403 `HOLD_NOT_OWNED` |
| List my holds | Returns only rows matching the header |

## 4. Threat model (honest)

| Threat | Status in this project | Production mitigation (out of scope) |
|--------|------------------------|--------------------------------------|
| Impersonation (typing someone else's number) | **Accepted** — inherent to the brief's design | OTP to guardian's phone; verification against the birth registration system |
| Seat hoarding by bots with fake numbers | **Accepted** — limited by one-hold-per-student-per-class and expiry | CAPTCHA at admission open, verified identities, per-identity quotas |
| Anyone creating classes | **Accepted** — no admin auth per brief | Admin login + RBAC |
| Hold ID guessing | Mitigated: UUIDv7 has 74 random bits | Signed/opaque IDs |
| SQL injection | Mitigated: EF Core parameters; raw SQL only via interpolated `FormattableString` (parameterized) | — |
| XSS | Mitigated: React escapes output; no `dangerouslySetInnerHTML`; class names rendered as text | CSP header |
| Clickjacking | Mitigated: `X-Frame-Options: DENY` on nginx | CSP `frame-ancestors` |
| Oversized payloads | Mitigated: Kestrel `MaxRequestBodySize = 64 KB` → 413 | Reverse-proxy limits |
| Information leakage via errors | Mitigated: problem+json with stable codes; no stack traces, SQL, or exception messages in responses | — |
| DoS | Partially: connection-pool limits, timeouts; no rate limiting (would break evaluation) | Rate limiting at the edge, autoscaling |
| PII in logs | Mitigated: student IDs masked (last 4 digits) | Log retention policy |
| Secrets in repo | Dev-only Postgres credentials in compose (documented); no real secrets | Secret manager |
| Transport security | HTTP only on localhost | HTTPS everywhere |
| CORS abuse | Allowlist via `CORS_ALLOWED_ORIGINS`; only needed headers/methods | — |

## 5. Implemented controls checklist

Backend
- [ ] FluentValidation on every command/query input; nullable request DTO properties so missing fields produce 400, not defaults.
- [ ] `GlobalExceptionHandler` returns generic text for unexpected errors (`INTERNAL_ERROR`), logs details server-side with `traceId`.
- [ ] Unique and check constraints in the DB as defense in depth.
- [ ] Body size limit 64 KB.
- [ ] CORS policy restricted to configured origins, headers `Content-Type` + `X-Student-Id`, methods `GET/POST/DELETE`.
- [ ] Runtime image runs as non-root (`USER $APP_UID`).
- [ ] Masking helper: `StudentIdMask.Mask(id) => new string('*', 13) + id[^4..]`; used in all log statements.

Frontend
- [ ] No secrets in the bundle (only the public API URL).
- [ ] Only the student ID is stored in `localStorage` (needed for R11). Nothing else persistent.
- [ ] All API data rendered as text; IDs URL-encoded with `encodeURIComponent` in paths.
- [ ] nginx headers: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`.

## 6. Privacy note

A birth registration number is personal data. We store only what's needed (the number and application timestamps), never return it except in the create-hold response (contract field `studentId`), and mask it in logs. A "Change" button in the UI lets a parent clear it from `localStorage` on a shared computer.
