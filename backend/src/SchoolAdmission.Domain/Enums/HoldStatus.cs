namespace SchoolAdmission.Domain.Enums;

/// <summary>Stored hold status. Serialized/stored as the uppercase name ("HELD", "CONFIRMED", ...).</summary>
public enum HoldStatus
{
    Held,
    Confirmed,
    Expired,
    Cancelled,
}
