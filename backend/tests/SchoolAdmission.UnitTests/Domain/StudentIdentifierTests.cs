using SchoolAdmission.Domain;

namespace SchoolAdmission.UnitTests.Domain;

public sealed class StudentIdentifierTests
{
    [Theory]
    [InlineData("20152691234567890")]
    [InlineData("00000000000000000")]
    public void IsValid_Exactly17AsciiDigits_True(string value) => Assert.True(StudentIdentifier.IsValid(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2015269123456789")]     // 16
    [InlineData("201526912345678901")]   // 18
    [InlineData(" 20152691234567890")]   // untrimmed (callers trim first)
    [InlineData("2015 269123456789")]
    [InlineData("2015269123456789O")]    // letter O
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০")]  // Bengali digits: the UI converts, the API does not
    [InlineData("２０１５２６９１２３４５６７８９０")] // full-width digits
    public void IsValid_Otherwise_False(string? value) => Assert.False(StudentIdentifier.IsValid(value));
}
