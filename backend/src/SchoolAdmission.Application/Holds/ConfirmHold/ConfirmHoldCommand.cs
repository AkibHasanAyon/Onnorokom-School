using FluentValidation;
using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common.Time;
using SchoolAdmission.Application.Common.Validation;

namespace SchoolAdmission.Application.Holds.ConfirmHold;

/// <summary>POST /holds/{holdId}/confirm — the (simulated) fee was paid.</summary>
public sealed record ConfirmHoldCommand(string HoldId, string? StudentId) : ICommand<HoldConfirmedDto>, IStudentRequest;

public sealed class ConfirmHoldCommandValidator : AbstractValidator<ConfirmHoldCommand>
{
    public ConfirmHoldCommandValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}

/// <summary>Contract fields: holdId, status. Extras: classId, confirmedAt.</summary>
public sealed record HoldConfirmedDto(Guid HoldId, string Status, Guid ClassId, DateTimeOffset ConfirmedAt);

/// <summary>
/// Check order (API.md §4.6): 400 header (validator) → 404 → 403 → already CONFIRMED → 200 (idempotent, R5)
/// → 409 HOLD_CANCELLED → 409 HOLD_EXPIRED → confirm.
/// The hold row is locked, so concurrent confirms (or a confirm racing a cancel) are decided one at a time:
/// the second waits, then sees the committed status. Confirm never increases held + confirmed, so it does
/// not need the class lock.
/// </summary>
public sealed class ConfirmHoldCommandHandler(IHoldRepository holds, TimeProvider clock)
    : IRequestHandler<ConfirmHoldCommand, HoldConfirmedDto>
{
    public async Task<HoldConfirmedDto> Handle(ConfirmHoldCommand command, CancellationToken cancellationToken)
    {
        var hold = await HoldAccess.LockOwnedAsync(holds, command.HoldId, command.StudentId!, cancellationToken);

        // Read after the lock, so a request that waited is judged at the moment it actually runs.
        var now = clock.GetUtcNow().TruncateToSeconds();
        hold.Confirm(now); // false = already confirmed → still 200 with the original confirmedAt

        return new HoldConfirmedDto(hold.Id, "CONFIRMED", hold.ClassId, hold.ConfirmedAt!.Value);
    }
}
