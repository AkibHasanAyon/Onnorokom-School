# Testing_QA.md

## 1. Strategy

| Layer | Tooling | What it proves |
|-------|---------|----------------|
| Backend unit | xUnit | Domain transitions (`Hold`), validators, small helpers |
| Backend integration | xUnit + `WebApplicationFactory<Program>` + **real PostgreSQL** + `FakeTimeProvider` | Contract, rules R1–R7, concurrency, expiry, persistence, robustness |
| Frontend unit/component | Vitest + React Testing Library + MSW v2 + fake timers | R9–R12 behaviors, ID normalization, error mapping |
| Manual QA | Script below | R8–R12 end-to-end in a browser |
| Smoke (bonus) | `scripts/concurrency-smoke.sh` | R1 across two live backend processes |

**Never** use EF InMemory/SQLite for anything touching holds — they can't run `FOR UPDATE` and would give false confidence.

Minimum required by the brief: 3 backend tests including 1 concurrency test. Our target: ~35 backend tests, ~12 frontend tests.

---

## 2. Running tests

```bash
# Docker only (what reviewers run)
docker compose --profile test run --rm backend-tests
docker compose --profile test run --rm frontend-tests

# Local
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db
cd backend && TEST_DB_CONNECTION="Host=localhost;Port=5433;Database=postgres;Username=admission;Password=admission;Maximum Pool Size=100" dotnet test
cd frontend && npm test -- --run && npm run typecheck
```

---

## 3. Backend test infrastructure

### 3.1 `PostgresDatabase` (per test class, via `IClassFixture` or `IAsyncLifetime`)
- Reads `TEST_DB_CONNECTION` (default `Host=localhost;Port=5433;Database=postgres;Username=admission;Password=admission`).
- Creates `test_<guid:N>`; exposes its connection string; on dispose `DROP DATABASE ... WITH (FORCE)`.
- Each test class gets an isolated DB → tests can run in parallel safely.
- Put every test class under `Concurrency/` in one xUnit collection (`[Collection("Concurrency")]`) so bursts never run at the same time. Otherwise parallel bursts (≈ 200 connections each) can exceed Postgres `max_connections=300` → `53300 too many clients`.

### 3.2 `AdmissionApiFactory : WebApplicationFactory<Program>`
```csharp
public sealed class AdmissionApiFactory(string connectionString, int holdSeconds = 600) : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("HOLD_DURATION_SECONDS", holdSeconds.ToString());
        builder.UseSetting("HOLD_SWEEPER_ENABLED", "false");   // tests trigger sweeps explicitly
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
        });
    }
}
```
Migrations run on host start, so each fresh DB is ready.

### 3.3 Helpers
- `TestData.StudentId(int i) => $"2015{i:D13}"` (17 digits).
- `client.CreateClassAsync(name, seats)`, `client.HoldAsync(classId, studentId)`, `client.ConfirmAsync(...)`, `client.CancelAsync(...)` returning `HttpResponseMessage`.
- `Db.AssertInvariantAsync(connectionString)` runs the invariant SQL from `Database.md §10` and asserts zero rows.
- Use plain `Assert.*` (no FluentAssertions 8+).

---

## 4. Backend test catalog

### 4.1 Contract (`Contract/`)
| Test | Asserts |
|------|---------|
| `CreateClass_Returns201_WithExactFields` | 201, `Location`, JSON has `id`,`name`,`totalSeats` |
| `ListClasses_ReturnsExactFields` | each item has `id,name,totalSeats,heldSeats,confirmedSeats,availableSeats` |
| `CreateHold_Returns201_WithExactFields` | `holdId,classId,studentId,status="HELD",expiresAt` (ends with `Z`, no fraction) |
| `GetHold_ReturnsExactFields` | `holdId,classId,status,expiresAt` |
| `Confirm_Returns200_WithHoldIdAndStatus` | `{holdId, status:"CONFIRMED"}` |
| `Cancel_Returns204_EmptyBody` | 204, empty content |
| `MyHolds_ReturnsExactFields` | `holdId,classId,className,status,expiresAt` |
| `Errors_AreProblemJson_WithCode` | content type `application/problem+json`, has `code`, `status` |

Use `JsonDocument` and check property names explicitly (catches casing/renaming regressions).

### 4.2 Concurrency (`Concurrency/`) ⭐ — the required test
```csharp
[Fact]
public async Task ConcurrentHolds_NeverExceedTotalSeats()
{
    using var client = _factory.CreateClient();
    var classId = await client.CreateClassIdAsync("Concurrency Class", totalSeats: 25);
    const int attempts = 200;
    var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    var tasks = Enumerable.Range(0, attempts).Select(async i =>
    {
        await start.Task;                                    // release all at once
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        req.Headers.Add("X-Student-Id", TestData.StudentId(i));
        using var res = await client.SendAsync(req);
        return res.StatusCode;
    }).ToArray();
    start.SetResult();
    var codes = await Task.WhenAll(tasks);

    Assert.Equal(25, codes.Count(c => c == HttpStatusCode.Created));
    Assert.Equal(175, codes.Count(c => c == HttpStatusCode.Conflict));
    Assert.DoesNotContain(codes, c => (int)c >= 500);

    var cls = await client.GetFromJsonAsync<JsonElement>($"/classes/{classId}");
    Assert.Equal(25, cls.GetProperty("heldSeats").GetInt32());
    Assert.Equal(0, cls.GetProperty("availableSeats").GetInt32());
    await Db.AssertInvariantAsync(_db.ConnectionString);
}
```

| Test | Setup | Expect |
|------|-------|--------|
| `ConcurrentHolds_NeverExceedTotalSeats` | 25 seats, 200 distinct students | 25×201, 175×409, invariant |
| `ConcurrentHolds_SameStudent_OnlyOneSucceeds` | 10 seats, same student ×50 | 1×201, 49×409 (`ACTIVE_HOLD_EXISTS`) |
| `ConcurrentConfirms_CountOnce` | 1 hold, 30 parallel confirms | all 200, `confirmedSeats == 1` |
| `MixedBurst_KeepsInvariant` | 10 seats; parallel creates + cancels + confirms ×300 | no 5xx, invariant |
| `TwoInstances_ShareDatabase_NeverOversell` (B1) | two factories on the same DB, 100 requests each alternating, 30 seats | exactly 30×201 total, invariant |

### 4.3 Holds behavior (`Holds/`)
- Second apply same class → 409 `ACTIVE_HOLD_EXISTS` with `existingHoldId`.
- Apply after cancel → 201. Apply after confirm → 409 `ALREADY_ADMITTED`.
- Same student, two different classes → both 201.
- Confirm by other student → 403 `HOLD_NOT_OWNED`; cancel by other student → 403.
- Confirm cancelled → 409 `HOLD_CANCELLED`. Cancel confirmed → 409 `HOLD_ALREADY_CONFIRMED`. Cancel twice → 204, 204.
- Cancel releases seat immediately (`availableSeats` +1).
- `/me/holds` returns only this student's holds, newest first, all statuses.

### 4.4 Expiry (`Expiry/`) — using `FakeTimeProvider`
- `Hold_ExpiresAtDeadline_WithoutSweeper`: create; `Clock.Advance(600s)`; GET hold → `EXPIRED`; class counts restored.
- `Hold_StillHeld_OneSecondBefore`: advance 599 s → `HELD`.
- `Confirm_AfterExpiry_Returns409`.
- `Reapply_AfterExpiry_Succeeds` (exercises stale-row materialization + partial unique index).
- `Sweeper_MaterializesExpiredRows`: advance, `sender.Send(new ExpireStaleHoldsCommand(500))`, DB row status = `EXPIRED`.
- `HoldDuration_IsConfigurable`: factory with `holdSeconds: 30` → `expiresAt - createdAt == 30 s`.

### 4.5 Persistence (`Persistence/`)
- Create class + hold with factory A; dispose A; start factory B on the same DB; GET returns the same data (R6 at app level; the volume covers container level).

### 4.6 Robustness (`Robustness/`) — R7: assert `400 <= status < 500` for every case

| Endpoint | Bad inputs |
|----------|-----------|
| POST /classes | no body; `{}`; `not json`; `{"name":""}`; `{"name":"   ","totalSeats":5}`; `{"name":"x","totalSeats":0}`; `-1`; `10001`; `"5"`; `5.5`; `null`; `1e10`; `99999999999999999999`; name 101 chars; array body `[]`; `Content-Type: text/plain`; 100 KB body |
| GET /classes/{id} | `abc`; `123`; `00000000-0000-0000-0000-000000000000`; random UUID; very long string |
| POST /classes/{id}/holds | missing header; empty; 16 digits; 18 digits; letters; Bengali digits; `"2015 2691234567890"`; malformed class id; unknown class id |
| GET /holds/{id} | malformed id; unknown id |
| POST confirm / DELETE | missing header; bad header; malformed id; unknown id |
| GET /me/holds | missing header; bad header |
| Unknown | `GET /nope` → 404; `PUT /classes` → 405 |

Implement as `[Theory]` + `[MemberData]`.

### 4.7 Unit (`SchoolAdmission.UnitTests`)
- `Hold.EffectiveStatus` boundaries (now < expires, == expires, > expires).
- `Hold.Confirm`: from HELD → true; CONFIRMED → false; EXPIRED/CANCELLED → `DomainException` with right code.
- `Hold.Cancel`: same matrix.
- `CreateClassCommandValidator` and student-ID rule cases.
- `TruncateToSeconds`.

---

## 5. Frontend tests (B2)

Setup: `vitest` with `environment: 'jsdom'`, `setupFiles: ['src/test/setup.ts']` (imports `@testing-library/jest-dom/vitest`, starts MSW `setupServer(...handlers)` with `onUnhandledRequest: 'error'`, resets handlers and `localStorage` after each test). Render helper wraps `QueryClientProvider` (retry off), `StudentProvider`, `MemoryRouter`.

| Test | Asserts |
|------|---------|
| `studentId.normalize` | Bengali digits → ASCII; strips spaces/dashes |
| `studentId.isValid` | exactly 17 digits only |
| `StudentIdBar persists to localStorage` | valid ID saved; invalid shows error and isn't saved; reload (re-render) restores it (R11) |
| `ClassListPage renders counts` | table values from MSW |
| `ClassListPage polls` | fake timers; MSW returns new counts; advance 3 s → new values shown without reload (R10) |
| `Apply disabled without student ID` | button disabled + hint |
| `Apply shows friendly 409` | MSW 409 `NO_SEATS_AVAILABLE` → message from `messages.ts` (R12) |
| `Apply navigates on success` | lands on application page |
| `ApplicationPage expiry` | hold expires in 2 s; advance timers → badge `EXPIRED`, Pay disabled, no reload (R9) |
| `ApplicationPage confirm` | click Pay → `CONFIRMED` badge |
| `Network error banner` | MSW `HttpResponse.error()` → "Can't reach the server…" (R12) |
| `ErrorBoundary` | throwing child → fallback UI, not blank |
| `MyApplicationsPage` | lists holds for stored ID |

Use `vi.useFakeTimers({ shouldAdvanceTime: true })` with React Query polling; advance with `await vi.advanceTimersByTimeAsync(ms)`.

---

## 6. Manual QA script (run before submission, from a fresh clone)

Start with short holds: `HOLD_DURATION_SECONDS=45 docker compose up --build`. Open `http://localhost:5173`.

| # | Rule | Steps | Expected |
|---|------|-------|----------|
| 1 | R8 | Admin: create "Class 6 - Morning Shift", 2 seats; "Class 6 - Day Shift", 1 seat | Both appear in Classes within 3 s |
| 2 | R8 | Try seats `0`, empty name, duplicate name | Friendly inline errors, no crash |
| 3 | R12 | Student ID `123` | Inline "Must be exactly 17 digits" |
| 4 | — | Enter `২০১৫২৬৯১২৩৪৫৬৭৮৯০` (Bengali digits) | Accepted as `20152691234567890` |
| 5 | R4/FR-4 | Apply to Morning | Application page, `HELD`, countdown ~00:45 |
| 6 | R3 | Back to Classes, same class | Shows "View application" (no double apply) |
| 7 | R10 | Second browser/incognito: different ID, apply Morning | First window's counts update to Held 2 / Available 0 without reload |
| 8 | R12 | Third ID applies Morning | "Sorry, all seats … are taken" |
| 9 | R5 | First window: Pay | `CONFIRMED`; counts Held 1, Confirmed 1 |
| 10 | R9 | Second window: wait for 0 | `EXPIRED` appears live; Pay disabled; counts free one seat within 3 s |
| 11 | R11 | Refresh; close browser and reopen; open My Applications | ID still filled; all applications listed with correct status |
| 12 | R11 | Refresh on `/applications/<id>` deep link | Page loads (no nginx 404) |
| 13 | — | Cancel a fresh HELD application | Confirm dialog → `CANCELLED`; seat freed |
| 14 | R12 | `docker compose stop backend` | Banner "Can't reach the server…", last data still visible, actions show friendly error |
| 15 | — | `docker compose start backend` | Banner disappears automatically |
| 16 | R6 | `docker compose down && docker compose up` | All classes/applications still present |
| 17 | R7 | `curl -i -X POST localhost:8080/classes -H 'Content-Type: application/json' -d '{bad'` | 400 problem+json |
| 18 | B1 | `docker compose -f docker-compose.yml -f docker-compose.multi.yml up -d --build` then `./scripts/concurrency-smoke.sh` | 201 count == seats; rest 409; no 5xx |

---

## 7. Pre-submission checklist

- [ ] Fresh clone → `docker compose up --build` works with nothing but Docker.
- [ ] Both test commands pass in Docker.
- [ ] Manual QA script fully green.
- [ ] `README.md`: run steps, frontend URL (`http://localhost:5173`), test commands.
- [ ] `ASSUMPTIONS.md` at root, short, includes the B1 explanation.
- [ ] `AI_USAGE.md` at root, honest, includes concrete "AI was wrong" examples.
- [ ] Commit history shows incremental progress.
- [ ] No assignment PDF, secrets, `bin/obj/node_modules/dist` committed.
- [ ] Repo is **public**; link submitted before **11 Nov 2026 23:59:59**.
