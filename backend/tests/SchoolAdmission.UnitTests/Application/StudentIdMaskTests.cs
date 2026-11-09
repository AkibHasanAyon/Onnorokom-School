using SchoolAdmission.Application.Common.Logging;

namespace SchoolAdmission.UnitTests.Application;

public sealed class StudentIdMaskTests
{
    [Theory]
    [InlineData("20152691234567890", "*************7890")]
    [InlineData("  20152691234567890 ", "*************7890")]
    [InlineData("00000000000000001", "*************0001")]
    public void ValidId_ShowsOnlyTheLastFourDigits(string id, string expected) =>
        Assert.Equal(expected, StudentIdMask.Mask(id));

    [Theory]
    [InlineData(null, "(none)")]
    [InlineData("", "(none)")]
    [InlineData("   ", "(none)")]
    [InlineData("123", "(invalid)")]
    [InlineData("201526912345678901", "(invalid)")]  // 18 digits: not echoed even partially
    [InlineData("password123", "(invalid)")]
    public void MissingOrInvalidId_IsNeverEchoed(string? id, string expected) =>
        Assert.Equal(expected, StudentIdMask.Mask(id));
}
