namespace SchoolAdmission.IntegrationTests.Infrastructure;

public static class TestData
{
    /// <summary>A valid, distinct 17-digit birth registration number for index <paramref name="i"/> (0 ≤ i &lt; 10^13).</summary>
    public static string StudentId(int i) => $"2015{i:D13}";

    /// <summary>A class name unique across test runs, so tests sharing a database never collide.</summary>
    public static string ClassName(string prefix = "Class") => $"{prefix} {Guid.NewGuid():N}"[..Math.Min(100, prefix.Length + 33)];
}
