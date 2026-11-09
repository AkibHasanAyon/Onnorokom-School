namespace SchoolAdmission.Domain;

/// <summary>The student's 17-digit birth registration number — the only identity in the system.</summary>
public static class StudentIdentifier
{
    public const int Length = 17;

    /// <summary>True when <paramref name="value"/> is exactly 17 ASCII digits (callers trim first).</summary>
    public static bool IsValid(string? value) =>
        value is { Length: Length } && value.All(char.IsAsciiDigit);
}
