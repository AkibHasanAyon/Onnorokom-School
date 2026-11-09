namespace SchoolAdmission.Domain.Entities;

/// <summary>A bookable unit: one grade + one shift (e.g. "Class 6 - Morning Shift") with a fixed seat count.</summary>
public sealed class SchoolClass
{
    public const int MaxNameLength = 100;
    public const int MinSeats = 1;
    public const int MaxSeats = 10_000;

    private SchoolClass() { } // EF

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;

    /// <summary><c>UPPER(TRIM(name))</c>; unique, so names are unique case-insensitively.</summary>
    public string NormalizedName { get; private set; } = default!;

    public int TotalSeats { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static SchoolClass Create(Guid id, string name, int totalSeats, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(name);
        var trimmed = name.Trim();
        if (trimmed.Length is 0 or > MaxNameLength)
            throw new ArgumentException($"Name must be 1-{MaxNameLength} characters after trimming.", nameof(name));
        if (totalSeats is < MinSeats or > MaxSeats)
            throw new ArgumentOutOfRangeException(nameof(totalSeats), $"Total seats must be {MinSeats}-{MaxSeats}.");

        return new SchoolClass
        {
            Id = id,
            Name = trimmed,
            NormalizedName = NormalizeName(trimmed),
            TotalSeats = totalSeats,
            CreatedAt = now,
        };
    }

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();
}
