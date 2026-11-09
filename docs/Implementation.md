# Implementation.md — Build Plan and Reference Code

The plan is ordered so the **graded core (R1–R7, concurrency test, Docker)** lands first. Each task ends in a commit. Do not merge tasks into one giant commit.

Legend: 🧪 = must add/extend tests in this task. 📌 = indicative commit message.

> **Git:** the authoritative phase numbering, branches and commit messages are in `Git_Workflow.md §5` (15 phases, ~50 commits). Use this file for *what to build* and the reference code; use `Git_Workflow.md` for *how to commit and push*.

---

## Phase 0 — Repository bootstrap

| Task | Details |
|------|---------|
| 0.1 | `git init`. Add `.gitignore` (dotnet + node: `bin/ obj/ node_modules/ dist/ .vs/ .idea/ *.user .env coverage/ TestResults/`), `.gitattributes` (`* text=auto`, `*.sh text eol=lf`), `docs/`, root `README.md` stub, `ASSUMPTIONS.md` (copy of `docs/ASSUMPTIONS.md`), `AI_USAGE.md` stub, `.env.example`. **Do not commit the assignment PDF.** |

📌 `chore: bootstrap repository with docs and project conventions`

---

## Phase 1 — Backend skeleton + Docker

| Task | Details |
|------|---------|
| 1.1 | `dotnet new sln -n SchoolAdmission`; class libraries Domain, Application, Infrastructure; `webapi` (controllers) Api; xunit UnitTests, IntegrationTests. Wire references per `Architecture.md §3`. `Directory.Build.props`: `net10.0`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `LangVersion latest`. |
| 1.2 | Packages: Application → `MediatR` **12.5.0**, `FluentValidation` 11.x, `FluentValidation.DependencyInjectionExtensions` 11.x, `Microsoft.Extensions.Options`. Infrastructure → `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x. Api → `Microsoft.EntityFrameworkCore.Design` 10.x (PrivateAssets all). Tests → `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing`, `Npgsql`. |
| 1.3 | `Program.cs` (see §R1), `HealthController` (`GET /health` without DB for now), `public partial class Program {}`. |
| 1.4 | `backend/Dockerfile` (§R9), `.dockerignore`, root `docker-compose.yml` with `db` + `backend` (§R10). Verify `curl localhost:8080/health`. |

📌 `chore(backend): scaffold clean-architecture solution and health endpoint`
📌 `build: add backend Dockerfile and docker-compose with postgres`

---

## Phase 2 — Domain + persistence

| Task | Details |
|------|---------|
| 2.1 | Domain: `SchoolClass`, `Hold`, `HoldStatus`, `ErrorCodes`, `DomainException`, `StudentIdentifier` (§R2). 🧪 unit tests for `Hold` transitions. |
| 2.2 | Infrastructure: `AdmissionDbContext`, configurations, `HoldStatusConverter` (`Database.md §4`). `InitialCreate` migration. `DatabaseMigrator` with advisory lock + retry. Call it from `Program.cs`. |
| 2.3 | Health endpoint runs `SELECT 1` → 503 on failure. |

📌 `feat(domain): add class and hold entities with state transitions`
📌 `feat(infra): add EF Core model, initial migration and startup migrator`

---

## Phase 3 — Application plumbing

| Task | Details |
|------|---------|
| 3.1 | `ICommand`/`IQuery`, behaviors (Logging, Validation, Transaction) (§R3), exceptions, `HoldOptions` + env binding with `ValidateOnStart` (§R4). |
| 3.2 | `IUnitOfWork` + `UnitOfWork` with unique-violation translation; repositories and read store interfaces. |
| 3.3 | Api: `GlobalExceptionHandler`, `AddProblemDetails` with `CustomizeProblemDetails` (adds `code` + `traceId`), `InvalidModelStateResponseFactory` (400 `MALFORMED_JSON` / `VALIDATION_FAILED`), `UseStatusCodePages`, `UtcDateTimeOffsetConverter`, CORS from `CORS_ALLOWED_ORIGINS`, Kestrel body limit 64 KB. |
| 3.4 | 🧪 Integration test infrastructure: `PostgresDatabase` (creates `test_<guid>` DB, drops `WITH (FORCE)`), `AdmissionApiFactory`, `TestData.StudentId(i)`. |

📌 `feat(app): add MediatR pipeline with validation and transaction behaviors`
📌 `feat(api): add problem-details error handling and JSON conventions`
📌 `test: add integration test harness with real PostgreSQL`

---

## Phase 4 — Classes

| Task | Details |
|------|---------|
| 4.1 | `CreateClass` (command, validator, handler), `GetClasses`, `GetClassById` (queries + `ClassReadStore`), `ClassesController`. |
| 4.2 | 🧪 Contract tests for endpoints 1–3 (exact property names), validation cases, duplicate name 409, unknown/malformed id 404. |

📌 `feat(classes): create, list and get classes with seat accounting`

---

## Phase 5 — Create hold + concurrency ⭐ (most important)

| Task | Details |
|------|---------|
| 5.1 | `CreateHoldCommand` + validator + handler (§R5), `HoldRepository` lock/count/expire methods, `HoldsController` POST action. |
| 5.2 | 🧪 `ConcurrencyTests`: (a) 200 distinct students vs 25 seats → exactly 25×201, 175×409, DB invariant query returns 0 rows; (b) 50 parallel same-student → exactly 1×201; (c) mixed burst of create/cancel/confirm → invariant holds. |
| 5.3 | 🧪 Rule-3 tests: second apply → 409 `ACTIVE_HOLD_EXISTS` with `existingHoldId`. |

📌 `feat(holds): create seat holds with class-row locking`
📌 `test(holds): prove seat limit under concurrent apply bursts`

---

## Phase 6 — Read, confirm, cancel, my holds

| Task | Details |
|------|---------|
| 6.1 | `GetHoldById` (effective status), `GetMyHolds` (+ validator). `MeController`. |
| 6.2 | `ConfirmHold` (§R6), `CancelHold`. |
| 6.3 | 🧪 Ownership 403, idempotent confirm, concurrent confirm ×30 → `confirmedSeats == 1`, cancel frees seat, re-apply after cancel, `ALREADY_ADMITTED` after confirm, cancel confirmed → 409. |

📌 `feat(holds): add hold status, my-holds, confirm and cancel`

---

## Phase 7 — Expiry

| Task | Details |
|------|---------|
| 7.1 | `ExpireStaleHoldsCommand` + handler, `HoldExpirySweeper` (§R7), `HOLD_SWEEPER_ENABLED`, `HOLD_SWEEP_INTERVAL_SECONDS`. |
| 7.2 | 🧪 With `FakeTimeProvider`: hold expires at `expiresAt` (GET shows EXPIRED, counts restored) before any sweep; confirm after expiry 409; re-apply after expiry 201; sweeper command materializes rows; `HOLD_DURATION_SECONDS` respected. |

📌 `feat(holds): expire holds automatically with background sweeper`

---

## Phase 8 — Hardening

| Task | Details |
|------|---------|
| 8.1 | 🧪 Robustness matrix (`Testing_QA.md §4.6`): every bad input → 4xx, never 500. |
| 8.2 | 🧪 Persistence test: dispose factory, create a new one on the same DB, data still there. |
| 8.3 | Masked student IDs in logs; structured logging in `LoggingBehavior`. |

📌 `test(api): guarantee 4xx for malformed input across all endpoints`

---

## Phase 9 — Frontend skeleton

| Task | Details |
|------|---------|
| 9.1 | `npm create vite@latest frontend -- --template react-ts`. Add `react-router`, `@tanstack/react-query`. Dev: `vitest`, `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `msw`. Scripts: `dev`, `build`, `preview`, `test` (`vitest`), `typecheck` (`tsc -b --noEmit` or `tsc --noEmit -p tsconfig.app.json`). |
| 9.2 | `config.ts`, `public/config.js`, `index.html` loads `/config.js` before the module script. |
| 9.3 | `Dockerfile` (multi-stage, nginx), `nginx.conf`, `docker/40-runtime-config.sh` (`Integration.md §3`). Add `frontend` + `frontend-tests` to compose. |
| 9.4 | `Layout`, routes, `ErrorBoundary`, `QueryClientProvider`. |

📌 `chore(frontend): scaffold Vite React TS app with runtime config`
📌 `build(frontend): serve SPA with nginx and add to compose`

---

## Phase 10 — Frontend features

| Task | Details |
|------|---------|
| 10.1 | `api/client.ts` (`ApiError`, timeout, problem parsing), `api/types.ts`, `api/classes.ts`, `api/holds.ts`, `i18n/messages.ts`. |
| 10.2 | `StudentContext`, `StudentIdBar` (normalize Bengali digits, validate 17 digits, persist). |
| 10.3 | `ClassListPage` (3 s polling, Apply flow, disabled states), `CreateClassPage`. |
| 10.4 | `ApplicationPage` (5 s polling, countdown, Pay/Cancel, live EXPIRED), `MyApplicationsPage`. |
| 10.5 | `ConnectionBanner`, `ErrorAlert` everywhere, stale-data indicator. |
| 10.6 | 🧪 Frontend tests (`Testing_QA.md §5`). |

📌 one commit per sub-task, e.g. `feat(frontend): add class list with live seat counts`

---

## Phase 11 — Bonus + delivery

| Task | Details |
|------|---------|
| 11.1 | `docker-compose.multi.yml`, `scripts/concurrency-smoke.sh`, 🧪 two-host integration test. |
| 11.2 | Final `README.md` (from `Setup.md`), final `ASSUMPTIONS.md` (incl. B1 section), honest `AI_USAGE.md`. |
| 11.3 | Run the full QA checklist (`Testing_QA.md §6–7`) from a **fresh clone**: `git clone … && cd … && docker compose up --build`. |
| 11.4 | Make repo public, submit the link before the deadline. |

📌 `feat: support multiple backend instances sharing one database`
📌 `docs: finalize README, assumptions and AI usage`

---

# Reference code

> These are the canonical shapes. Follow them; adjust only for compiler details.

## R1. `Program.cs`

```csharp
using SchoolAdmission.Api.ErrorHandling;
using SchoolAdmission.Api.Json;
using SchoolAdmission.Application;
using SchoolAdmission.Infrastructure;
using SchoolAdmission.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter()));
builder.Services.Configure<ApiBehaviorOptions>(o =>
    o.InvalidModelStateResponseFactory = ProblemDetailsSetup.FromInvalidModelState);
builder.Services.AddProblemDetails(ProblemDetailsSetup.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var origins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:5173,http://127.0.0.1:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(origins)
    .WithHeaders("Content-Type", "X-Student-Id")
    .WithMethods("GET", "POST", "DELETE")
    .WithExposedHeaders("Location")));

builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 64 * 1024);

var app = builder.Build();

await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("frontend");
app.MapControllers();
app.Run();

public partial class Program { }
```

## R2. Domain `Hold`

```csharp
namespace SchoolAdmission.Domain.Entities;

public sealed class Hold
{
    private Hold() { } // EF

    public Guid Id { get; private set; }
    public Guid ClassId { get; private set; }
    public string StudentId { get; private set; } = default!;
    public HoldStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Hold Create(Guid id, Guid classId, string studentId, DateTimeOffset now, TimeSpan duration)
    {
        if (!StudentIdentifier.IsValid(studentId)) throw new ArgumentException("Invalid student id.", nameof(studentId));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        return new Hold
        {
            Id = id, ClassId = classId, StudentId = studentId, Status = HoldStatus.Held,
            CreatedAt = now, ExpiresAt = now + duration, UpdatedAt = now
        };
    }

    public HoldStatus EffectiveStatus(DateTimeOffset now) =>
        Status == HoldStatus.Held && now >= ExpiresAt ? HoldStatus.Expired : Status;

    /// <returns>true if the state changed; false if already confirmed (idempotent).</returns>
    public bool Confirm(DateTimeOffset now) => EffectiveStatus(now) switch
    {
        HoldStatus.Confirmed => false,
        HoldStatus.Held => Apply(() => { Status = HoldStatus.Confirmed; ConfirmedAt = now; }, now),
        HoldStatus.Cancelled => throw new DomainException(ErrorCodes.HoldCancelled, "This application was cancelled and can no longer be paid."),
        _ => throw new DomainException(ErrorCodes.HoldExpired, "This application has expired and can no longer be paid."),
    };

    /// <returns>true if the state changed; false if already cancelled (idempotent).</returns>
    public bool Cancel(DateTimeOffset now) => EffectiveStatus(now) switch
    {
        HoldStatus.Cancelled => false,
        HoldStatus.Held => Apply(() => { Status = HoldStatus.Cancelled; CancelledAt = now; }, now),
        HoldStatus.Confirmed => throw new DomainException(ErrorCodes.HoldAlreadyConfirmed, "A confirmed admission cannot be cancelled here."),
        _ => throw new DomainException(ErrorCodes.HoldExpired, "This application has already expired."),
    };

    private bool Apply(Action change, DateTimeOffset now) { change(); UpdatedAt = now; return true; }
}
```

`StudentIdentifier.IsValid(s)` → `s.Length == 17 && s.All(char.IsAsciiDigit)`.

`ErrorCodes` (Domain) holds every code string from `API.md §3` as `public const string`.

## R3. Behaviors

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any()) return await next();
        var ctx = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(ctx, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count > 0) throw new RequestValidationException(failures);
        return await next();
    }
}

public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork uow)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not ICommandMarker) return await next();
        await using var tx = await uow.BeginTransactionAsync(ct);   // READ COMMITTED
        var response = await next();
        await uow.SaveChangesAsync(ct);                              // translates 23505 → ConflictException
        await tx.CommitAsync(ct);
        return response;
    }
}
```

`RequestValidationException` → 400. Its `code` is the first failure's `ErrorCode` if it is `INVALID_STUDENT_ID`, else `VALIDATION_FAILED`.

Registration (`Application/DependencyInjection.cs`):
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
});
services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
```

Student-ID rule (shared):
```csharp
public static IRuleBuilderOptions<T, string?> ValidStudentId<T>(this IRuleBuilder<T, string?> rule) =>
    rule.Must(v => v is not null && StudentIdentifier.IsValid(v.Trim()))
        .WithErrorCode(ErrorCodes.InvalidStudentId)
        .WithMessage("X-Student-Id must be the 17-digit birth registration number.");
```

## R4. Options

```csharp
public sealed class HoldOptions
{
    public int HoldDurationSeconds { get; set; } = 600;
    public int SweepIntervalSeconds { get; set; } = 5;
    public bool SweeperEnabled { get; set; } = true;
}

services.AddOptions<HoldOptions>()
    .Configure<IConfiguration>((o, c) =>
    {
        o.HoldDurationSeconds = c.GetValue("HOLD_DURATION_SECONDS", 600);
        o.SweepIntervalSeconds = c.GetValue("HOLD_SWEEP_INTERVAL_SECONDS", 5);
        o.SweeperEnabled = c.GetValue("HOLD_SWEEPER_ENABLED", true);
    })
    .Validate(o => o.HoldDurationSeconds is >= 1 and <= 86400, "HOLD_DURATION_SECONDS must be 1..86400")
    .Validate(o => o.SweepIntervalSeconds is >= 1 and <= 300, "HOLD_SWEEP_INTERVAL_SECONDS must be 1..300")
    .ValidateOnStart();
```

## R5. `CreateHoldCommandHandler` ⭐

```csharp
public sealed record CreateHoldCommand(string ClassId, string? StudentId) : ICommand<HoldCreatedDto>;

public sealed class CreateHoldCommandValidator : AbstractValidator<CreateHoldCommand>
{
    public CreateHoldCommandValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}

public sealed class CreateHoldCommandHandler(
    IClassRepository classes,
    IHoldRepository holds,
    TimeProvider clock,
    IOptions<HoldOptions> options) : IRequestHandler<CreateHoldCommand, HoldCreatedDto>
{
    public async Task<HoldCreatedDto> Handle(CreateHoldCommand cmd, CancellationToken ct)
    {
        var studentId = cmd.StudentId!.Trim();
        var now = clock.GetUtcNow().TruncateToSeconds();

        // 1. Serialize every hold creation for this class across all processes. Released at COMMIT.
        if (!RouteId.TryParse(cmd.ClassId, out var classId))
            throw new NotFoundException(ErrorCodes.ClassNotFound, "This class does not exist.");
        var schoolClass = await classes.GetByIdForUpdateAsync(classId, ct)
            ?? throw new NotFoundException(ErrorCodes.ClassNotFound, "This class does not exist.");

        // 2. Let the partial unique index accept a re-application after this student's own hold expired.
        await holds.ExpireStaleHoldForStudentAsync(classId, studentId, now, ct);

        // 3. R3: one active-or-confirmed hold per student per class.
        var existing = await holds.FindActiveOrConfirmedAsync(classId, studentId, now, ct);
        if (existing is not null)
        {
            throw existing.Status == HoldStatus.Confirmed
                ? new ConflictException(ErrorCodes.AlreadyAdmitted,
                    "This student is already admitted to this class.",
                    new Dictionary<string, object?> { ["existingHoldId"] = existing.Id })
                : new ConflictException(ErrorCodes.ActiveHoldExists,
                    "This student already has an active application for this class.",
                    new Dictionary<string, object?> { ["existingHoldId"] = existing.Id });
        }

        // 4. R1: never exceed total seats.
        var (held, confirmed) = await holds.GetSeatCountsAsync(classId, now, ct);
        if (held + confirmed >= schoolClass.TotalSeats)
            throw new ConflictException(ErrorCodes.NoSeatsAvailable,
                $"All seats in '{schoolClass.Name}' are currently held or confirmed.");

        // 5. Insert. TransactionBehavior saves and commits.
        var hold = Hold.Create(Guid.CreateVersion7(now), schoolClass.Id, studentId, now,
            TimeSpan.FromSeconds(options.Value.HoldDurationSeconds));
        holds.Add(hold);

        return new HoldCreatedDto(hold.Id, hold.ClassId, hold.StudentId, "HELD", hold.ExpiresAt,
            schoolClass.Name, hold.CreatedAt, now);
    }
}
```

`TruncateToSeconds`: `new DateTimeOffset(t.Ticks - t.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero)` (input must be UTC).

## R6. `ConfirmHoldCommandHandler`

```csharp
public sealed record ConfirmHoldCommand(string HoldId, string? StudentId) : ICommand<HoldConfirmedDto>;

public sealed class ConfirmHoldCommandHandler(IHoldRepository holds, TimeProvider clock)
    : IRequestHandler<ConfirmHoldCommand, HoldConfirmedDto>
{
    public async Task<HoldConfirmedDto> Handle(ConfirmHoldCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow().TruncateToSeconds();
        if (!RouteId.TryParse(cmd.HoldId, out var holdId))
            throw new NotFoundException(ErrorCodes.HoldNotFound, "This application does not exist.");
        var hold = await holds.GetByIdForUpdateAsync(holdId, ct)              // FOR UPDATE
            ?? throw new NotFoundException(ErrorCodes.HoldNotFound, "This application does not exist.");
        if (hold.StudentId != cmd.StudentId!.Trim())
            throw new ForbiddenException(ErrorCodes.HoldNotOwned, "This application belongs to a different student.");

        hold.Confirm(now);   // false = already confirmed → still 200 (R5)
        return new HoldConfirmedDto(hold.Id, "CONFIRMED", hold.ClassId, hold.ConfirmedAt!.Value);
    }
}
```

`CancelHoldCommandHandler` is identical in shape, calls `hold.Cancel(now)`, returns `Unit.Value`; the controller returns `NoContent()`.

## R7. Sweeper

```csharp
public sealed class HoldExpirySweeper(
    IServiceScopeFactory scopes, IOptions<HoldOptions> options,
    TimeProvider clock, ILogger<HoldExpirySweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SweeperEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var expired = await sender.Send(new ExpireStaleHoldsCommand(BatchSize: 500), stoppingToken);
                if (expired > 0) logger.LogInformation("Expired {Count} stale holds", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Hold expiry sweep failed; will retry next tick");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
```

## R8. Controller shape

**Decision (check order):** commands/queries carry the **raw route id as a string**. The `ValidationBehavior` runs first (→ 400 for a bad header), then the handler parses the id with `RouteId.TryParse` (→ 404 if malformed or unknown). This guarantees the `API.md` check order "400 header → 404 id → 403 owner → 409 state" everywhere. Controllers never parse ids.

```csharp
public static class RouteId
{
    public static bool TryParse(string? raw, out Guid id) => Guid.TryParse(raw, out id) && id != Guid.Empty;
}

[ApiController]
[Route("holds")]
public sealed class HoldsController(ISender sender) : ControllerBase
{
    [HttpGet("{holdId}")]
    public async Task<IActionResult> Get(string holdId, CancellationToken ct) =>
        Ok(await sender.Send(new GetHoldByIdQuery(holdId), ct));

    [HttpPost("{holdId}/confirm")]
    public async Task<IActionResult> Confirm(string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId, CancellationToken ct) =>
        Ok(await sender.Send(new ConfirmHoldCommand(holdId, studentId), ct));

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> Cancel(string holdId,
        [FromHeader(Name = "X-Student-Id")] string? studentId, CancellationToken ct)
    {
        await sender.Send(new CancelHoldCommand(holdId, studentId), ct);
        return NoContent();
    }
}
```

`POST /classes/{classId}/holds` returns `Created($"/holds/{dto.HoldId}", dto)`; `POST /classes` returns `Created($"/classes/{dto.Id}", dto)`.

`CreateClassRequest` uses nullable properties so missing fields reach FluentValidation:
```csharp
public sealed record CreateClassRequest(string? Name, int? TotalSeats);
```

## R9. `backend/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore SchoolAdmission.sln
RUN dotnet publish src/SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app/publish --no-restore

FROM build AS test
ENTRYPOINT ["dotnet", "test", "SchoolAdmission.sln", "--logger", "console;verbosity=normal"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
```
(`runtime` must stay the **last** stage — it's the default build target.)

## R10. Root `docker-compose.yml`

```yaml
name: school-admission

services:
  db:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: admission
      POSTGRES_USER: admission
      POSTGRES_PASSWORD: admission
    command: ["postgres", "-c", "max_connections=300"]
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U admission -d admission"]
      interval: 2s
      timeout: 3s
      retries: 30
    restart: unless-stopped

  backend:
    build: ./backend
    environment:
      ASPNETCORE_HTTP_PORTS: "8080"
      ConnectionStrings__Default: "Host=db;Port=5432;Database=admission;Username=admission;Password=admission;Maximum Pool Size=100;Timeout=15;Command Timeout=30"
      HOLD_DURATION_SECONDS: ${HOLD_DURATION_SECONDS:-600}
      HOLD_SWEEP_INTERVAL_SECONDS: ${HOLD_SWEEP_INTERVAL_SECONDS:-5}
      CORS_ALLOWED_ORIGINS: ${CORS_ALLOWED_ORIGINS:-http://localhost:5173,http://127.0.0.1:5173}
    ports:
      - "8080:8080"
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

  frontend:
    build: ./frontend
    environment:
      API_BASE_URL: ${API_BASE_URL:-http://localhost:8080}
    ports:
      - "5173:80"
    depends_on:
      - backend
    restart: unless-stopped

  backend-tests:
    profiles: ["test"]
    build:
      context: ./backend
      target: test
    environment:
      TEST_DB_CONNECTION: "Host=db;Port=5432;Database=postgres;Username=admission;Password=admission;Maximum Pool Size=100"
    depends_on:
      db:
        condition: service_healthy

  frontend-tests:
    profiles: ["test"]
    build:
      context: ./frontend
      target: test

volumes:
  pgdata:
```
