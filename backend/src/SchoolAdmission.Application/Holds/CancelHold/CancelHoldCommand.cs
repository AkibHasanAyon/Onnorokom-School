using FluentValidation;
using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common.Time;
using SchoolAdmission.Application.Common.Validation;

namespace SchoolAdmission.Application.Holds.CancelHold;

/// <summary>DELETE /holds/{holdId} — withdraw a HELD application; the seat returns immediately.</summary>
public sealed record CancelHoldCommand(string HoldId, string? StudentId) : ICommand<Unit>, IStudentRequest;

public sealed class CancelHoldCommandValidator : AbstractValidator<CancelHoldCommand>
{
    public CancelHoldCommandValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}

/// <summary>
/// Check order (API.md §4.7): 400 → 404 → 403 → already CANCELLED → 204 (idempotent)
/// → 409 HOLD_ALREADY_CONFIRMED → 409 HOLD_EXPIRED → cancel. The row is kept with status CANCELLED.
/// </summary>
public sealed class CancelHoldCommandHandler(IHoldRepository holds, TimeProvider clock)
    : IRequestHandler<CancelHoldCommand, Unit>
{
    public async Task<Unit> Handle(CancelHoldCommand command, CancellationToken cancellationToken)
    {
        var hold = await HoldAccess.LockOwnedAsync(holds, command.HoldId, command.StudentId!, cancellationToken);

        hold.Cancel(clock.GetUtcNow().TruncateToSeconds()); // false = already cancelled → still 204
        return Unit.Value;
    }
}
