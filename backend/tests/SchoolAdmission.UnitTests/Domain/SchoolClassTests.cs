using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.UnitTests.Domain;

public sealed class SchoolClassTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsNameAndNormalizesForUniqueness()
    {
        var cls = SchoolClass.Create(Guid.NewGuid(), "  Class 6 - Morning Shift ", 120, T0);

        Assert.Equal("Class 6 - Morning Shift", cls.Name);
        Assert.Equal("CLASS 6 - MORNING SHIFT", cls.NormalizedName);
        Assert.Equal(120, cls.TotalSeats);
        Assert.Equal(T0, cls.CreatedAt);
    }

    [Fact]
    public void NormalizeName_IsCaseAndWhitespaceInsensitive() =>
        Assert.Equal(SchoolClass.NormalizeName("class 6 - morning shift"), SchoolClass.NormalizeName(" CLASS 6 - Morning Shift  "));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string name) =>
        Assert.Throws<ArgumentException>(() => SchoolClass.Create(Guid.NewGuid(), name, 10, T0));

    [Fact]
    public void Create_NameLength_MaxIs100AfterTrimming()
    {
        var ok = SchoolClass.Create(Guid.NewGuid(), " " + new string('a', 100) + " ", 10, T0);
        Assert.Equal(100, ok.Name.Length);

        Assert.Throws<ArgumentException>(() => SchoolClass.Create(Guid.NewGuid(), new string('a', 101), 10, T0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void Create_WithSeatsOutOfRange_Throws(int seats) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SchoolClass.Create(Guid.NewGuid(), "Class", seats, T0));

    [Theory]
    [InlineData(1)]
    [InlineData(10_000)]
    public void Create_WithSeatBoundaries_Succeeds(int seats) =>
        Assert.Equal(seats, SchoolClass.Create(Guid.NewGuid(), "Class", seats, T0).TotalSeats);
}
