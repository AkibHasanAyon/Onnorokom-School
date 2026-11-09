using SchoolAdmission.Application.Classes.CreateClass;

namespace SchoolAdmission.UnitTests.Application;

public sealed class CreateClassCommandValidatorTests
{
    private static readonly CreateClassCommandValidator Validator = new();

    [Theory]
    [InlineData("Class 6 - Morning Shift", 120)]
    [InlineData("  padded  ", 1)]
    [InlineData("x", 10_000)]
    public void Valid(string name, int seats) => Assert.True(Validator.Validate(new CreateClassCommand(name, seats)).IsValid);

    [Fact]
    public void Name_100CharsAfterTrimming_IsValid() =>
        Assert.True(Validator.Validate(new CreateClassCommand("   " + new string('a', 100) + "   ", 5)).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Name_MissingOrBlank_IsRequired(string? name) =>
        AssertSingleError(new CreateClassCommand(name, 5), "Name", "Name is required.");

    [Fact]
    public void Name_101Chars_IsTooLong() =>
        AssertSingleError(new CreateClassCommand(new string('a', 101), 5), "Name", "Name must be at most 100 characters.");

    [Theory]
    [InlineData("a\0b")]
    [InlineData("Class\u0007 6")]
    [InlineData("Class\u001b[31m6")]
    [InlineData("Class\n6")]
    [InlineData("Class\u007f")]
    [InlineData("Class\u0085 6")]
    public void Name_WithControlCharacters_IsRejected(string name) =>
        AssertSingleError(new CreateClassCommand(name, 5), "Name", "Name must not contain control characters.");

    [Theory]
    [InlineData("\tClass 6 - Morning Shift\n")]   // edge whitespace is trimmed, not rejected
    [InlineData("৬ষ্ঠ শ্রেণি - প্রভাতী শাখা")]     // Bengali
    [InlineData("Class 6 – Day Shift 🌞")]       // en dash + emoji (surrogate pair)
    public void Name_PrintableUnicode_IsValid(string name) =>
        Assert.True(Validator.Validate(new CreateClassCommand(name, 5)).IsValid);

    [Fact]
    public void TotalSeats_Missing_IsRequired() =>
        AssertSingleError(new CreateClassCommand("Class", null), "TotalSeats", "Total seats is required.");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    [InlineData(int.MaxValue)]
    public void TotalSeats_OutOfRange(int seats) =>
        AssertSingleError(new CreateClassCommand("Class", seats), "TotalSeats", "Total seats must be between 1 and 10000.");

    [Fact]
    public void BothMissing_ReportsBothFields()
    {
        var result = Validator.Validate(new CreateClassCommand(null, null));

        Assert.Equal(["Name", "TotalSeats"], result.Errors.Select(e => e.PropertyName).Distinct().Order());
    }

    private static void AssertSingleError(CreateClassCommand command, string property, string message)
    {
        var error = Assert.Single(Validator.Validate(command).Errors);
        Assert.Equal(property, error.PropertyName);
        Assert.Equal(message, error.ErrorMessage);
    }
}
