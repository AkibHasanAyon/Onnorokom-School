using MediatR;
using Microsoft.Extensions.Options;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Application.Common.Options;
using SchoolAdmission.Application.Common.Time;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Application.Holds.CreateHold;

/// <summary>
/// The only operation that can increase held + confirmed, so the only one that takes the class lock.
/// Runs inside TransactionBehavior's READ COMMITTED transaction:
/// lock class row → expire own stale hold → R3 check → R1 count → insert → COMMIT (releases the lock).
/// Every applicant to the same class, from any backend process, queues on that row lock in PostgreSQL.
/// </summary>
public sealed class CreateHoldCommandHandler(
    IClassRepository classes,
    IHoldRepository holds,
    TimeProvider clock,
    IOptions<HoldOptions> options) : IRequestHandler<CreateHoldCommand, HoldCreatedDto>
{
    public async Task<HoldCreatedDto> Handle(CreateHoldCommand command, CancellationToken cancellationToken)
    {
        var studentId = command.StudentId!.Trim(); // validated by CreateHoldCommandValidator

        if (!RouteId.TryParse(command.ClassId, out var classId))
            throw ClassNotFound();

        // 1. Serialize every hold creation for this class. Held until COMMIT/ROLLBACK.
        var schoolClass = await classes.GetByIdForUpdateAsync(classId, cancellationToken) ?? throw ClassNotFound();

        // Read the clock *after* acquiring the lock: a request that waited in the queue must not
        // count seats (or stamp createdAt) with a time from before it got its turn.
        var now = clock.GetUtcNow().TruncateToSeconds();

        // 2. A stored-HELD-but-expired hold of this student would block the partial unique index on re-apply.
        await holds.ExpireStaleHoldForStudentAsync(classId, studentId, now, cancellationToken);

        // 3. R3: one active-or-confirmed hold per student per class (ALREADY_ADMITTED wins over ACTIVE_HOLD_EXISTS).
        var existing = await holds.FindActiveOrConfirmedAsync(classId, studentId, now, cancellationToken);
        if (existing is not null)
        {
            var extra = new Dictionary<string, object?> { ["existingHoldId"] = existing.Id };
            throw existing.Status == HoldStatus.Confirmed
                ? new ConflictException(ErrorCodes.AlreadyAdmitted,
                    "This student is already admitted to this class.", extra)
                : new ConflictException(ErrorCodes.ActiveHoldExists,
                    "This student already has an active application for this class.", extra);
        }

        // 4. R1: never exceed total seats. Counts are derived from rows at `now`.
        var (held, confirmed) = await holds.GetSeatCountsAsync(classId, now, cancellationToken);
        if (held + confirmed >= schoolClass.TotalSeats)
            throw new ConflictException(ErrorCodes.NoSeatsAvailable,
                $"All seats in '{schoolClass.Name}' are currently held or confirmed.");

        // 5. Insert. TransactionBehavior saves and commits, which releases the class lock.
        var hold = Hold.Create(Guid.CreateVersion7(now), schoolClass.Id, studentId, now,
            TimeSpan.FromSeconds(options.Value.HoldDurationSeconds));
        holds.Add(hold);

        return new HoldCreatedDto(hold.Id, hold.ClassId, hold.StudentId, "HELD", hold.ExpiresAt,
            schoolClass.Name, hold.CreatedAt, now);
    }

    private static NotFoundException ClassNotFound() => new(ErrorCodes.ClassNotFound, "This class does not exist.");
}
