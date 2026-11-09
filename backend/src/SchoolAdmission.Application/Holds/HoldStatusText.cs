using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.Application.Holds;

/// <summary>Contract status strings ("HELD", "CONFIRMED", "EXPIRED", "CANCELLED").</summary>
public static class HoldStatusText
{
    public static string Of(HoldStatus status) => status switch
    {
        HoldStatus.Held => "HELD",
        HoldStatus.Confirmed => "CONFIRMED",
        HoldStatus.Expired => "EXPIRED",
        HoldStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <summary>
    /// The status clients see: a stored HELD hold whose expiresAt ≤ now is reported as EXPIRED,
    /// even if the sweeper hasn't updated the row yet (API.md §1).
    /// </summary>
    public static string Effective(HoldStatus stored, DateTimeOffset expiresAt, DateTimeOffset now) =>
        Of(stored == HoldStatus.Held && expiresAt <= now ? HoldStatus.Expired : stored);
}
