using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Abstractions.Persistence;

/// <summary>Seats in use in one class at a given instant (counts are always derived, never stored).</summary>
public readonly record struct SeatCounts(int Held, int Confirmed);

/// <summary>Write-side access to holds (commands only).</summary>
public interface IHoldRepository
{
    void Add(Hold hold);

    /// <summary><c>SELECT ... FOR UPDATE</c> on one hold row (confirm/cancel). Returns a tracked entity.</summary>
    Task<Hold?> GetByIdForUpdateAsync(Guid holdId, CancellationToken ct);

    /// <summary>
    /// Marks this student's own stored-HELD-but-past-expiry hold in this class as EXPIRED, so the partial
    /// unique index accepts a re-application. Returns the number of rows changed (0 or 1).
    /// </summary>
    Task<int> ExpireStaleHoldForStudentAsync(Guid classId, string studentId, DateTimeOffset now, CancellationToken ct);

    /// <summary>The student's CONFIRMED hold, or HELD hold with <c>expiresAt &gt; now</c>, in this class.</summary>
    Task<Hold?> FindActiveOrConfirmedAsync(Guid classId, string studentId, DateTimeOffset now, CancellationToken ct);

    /// <summary>Held = HELD and <c>expiresAt &gt; now</c>; Confirmed = CONFIRMED.</summary>
    Task<SeatCounts> GetSeatCountsAsync(Guid classId, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Sweeper: stores EXPIRED on up to <paramref name="batchSize"/> HELD holds with <c>expiresAt &lt;= now</c>,
    /// skipping rows locked by other transactions. Returns the number of rows changed.
    /// </summary>
    Task<int> ExpireStaleBatchAsync(DateTimeOffset now, int batchSize, CancellationToken ct);
}
