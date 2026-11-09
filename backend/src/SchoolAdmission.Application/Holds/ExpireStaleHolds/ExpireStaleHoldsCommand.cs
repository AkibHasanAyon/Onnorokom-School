using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Application.Holds.ExpireStaleHolds;

/// <summary>
/// Housekeeping: store EXPIRED on up to <see cref="BatchSize"/> HELD holds whose expiresAt has passed.
/// Purely cosmetic for the rules — every query already treats those holds as EXPIRED and stops counting
/// their seats at expiresAt — but it keeps stored data truthful for anyone reading the table.
/// </summary>
/// <returns>The number of holds updated.</returns>
public sealed record ExpireStaleHoldsCommand(int BatchSize) : ICommand<int>;

public sealed class ExpireStaleHoldsCommandHandler(IHoldRepository holds, TimeProvider clock)
    : IRequestHandler<ExpireStaleHoldsCommand, int>
{
    public Task<int> Handle(ExpireStaleHoldsCommand command, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(command.BatchSize, 1);
        // FOR UPDATE SKIP LOCKED: rows being confirmed/cancelled right now, or claimed by another
        // instance's sweeper, are skipped (picked up next tick) — no waiting, no deadlocks.
        return holds.ExpireStaleBatchAsync(clock.GetUtcNow(), command.BatchSize, cancellationToken);
    }
}
