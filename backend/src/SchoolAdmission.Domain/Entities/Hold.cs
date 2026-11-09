using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Domain.Entities;

/// <summary>
/// A time-limited reservation of one seat for one student in one class.
/// HELD → CONFIRMED | CANCELLED | EXPIRED; terminal states never change.
/// A stored HELD hold whose <see cref="ExpiresAt"/> has passed is effectively EXPIRED.
/// </summary>
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
        if (!StudentIdentifier.IsValid(studentId))
            throw new ArgumentException("Invalid student id.", nameof(studentId));
        if (duration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "Hold duration must be positive.");

        return new Hold
        {
            Id = id,
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = now + duration,
            UpdatedAt = now,
        };
    }

    /// <summary>The status clients see: HELD at or after <see cref="ExpiresAt"/> is EXPIRED.</summary>
    public HoldStatus EffectiveStatus(DateTimeOffset now) =>
        Status == HoldStatus.Held && now >= ExpiresAt ? HoldStatus.Expired : Status;

    /// <summary>Pay the fee. Idempotent.</summary>
    /// <returns>true if the state changed; false if already confirmed.</returns>
    public bool Confirm(DateTimeOffset now) => EffectiveStatus(now) switch
    {
        HoldStatus.Confirmed => false,
        HoldStatus.Held => Apply(() => { Status = HoldStatus.Confirmed; ConfirmedAt = now; }, now),
        HoldStatus.Cancelled => throw new DomainException(ErrorCodes.HoldCancelled,
            "This application was cancelled and can no longer be paid."),
        _ => throw new DomainException(ErrorCodes.HoldExpired,
            "This application has expired and can no longer be paid."),
    };

    /// <summary>Withdraw the application. Idempotent.</summary>
    /// <returns>true if the state changed; false if already cancelled.</returns>
    public bool Cancel(DateTimeOffset now) => EffectiveStatus(now) switch
    {
        HoldStatus.Cancelled => false,
        HoldStatus.Held => Apply(() => { Status = HoldStatus.Cancelled; CancelledAt = now; }, now),
        HoldStatus.Confirmed => throw new DomainException(ErrorCodes.HoldAlreadyConfirmed,
            "A confirmed admission cannot be cancelled here."),
        _ => throw new DomainException(ErrorCodes.HoldExpired,
            "This application has already expired."),
    };

    /// <summary>Materialize logical expiry into the stored status (housekeeping only).</summary>
    /// <returns>true if the stored status changed.</returns>
    public bool Expire(DateTimeOffset now) =>
        Status == HoldStatus.Held && now >= ExpiresAt
        && Apply(() => { Status = HoldStatus.Expired; ExpiredAt = now; }, now);

    private bool Apply(Action change, DateTimeOffset now)
    {
        change();
        UpdatedAt = now;
        return true;
    }
}
