using SchoolAdmission.Application.Holds;
using SchoolAdmission.Domain.Enums;

namespace SchoolAdmission.UnitTests.Application;

public sealed class HoldStatusTextTests
{
    private static readonly DateTimeOffset Expiry = new(2026, 11, 1, 4, 10, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(HoldStatus.Held, "HELD")]
    [InlineData(HoldStatus.Confirmed, "CONFIRMED")]
    [InlineData(HoldStatus.Expired, "EXPIRED")]
    [InlineData(HoldStatus.Cancelled, "CANCELLED")]
    public void Of_UsesUppercaseContractStrings(HoldStatus status, string expected) =>
        Assert.Equal(expected, HoldStatusText.Of(status));

    [Fact]
    public void Effective_HeldBeforeExpiry_IsHeld() =>
        Assert.Equal("HELD", HoldStatusText.Effective(HoldStatus.Held, Expiry, Expiry.AddTicks(-1)));

    [Fact]
    public void Effective_HeldAtExpiry_IsExpired() =>
        Assert.Equal("EXPIRED", HoldStatusText.Effective(HoldStatus.Held, Expiry, Expiry));

    [Theory]
    [InlineData(HoldStatus.Confirmed, "CONFIRMED")]
    [InlineData(HoldStatus.Cancelled, "CANCELLED")]
    [InlineData(HoldStatus.Expired, "EXPIRED")]
    public void Effective_TerminalStatuses_NeverChange(HoldStatus status, string expected) =>
        Assert.Equal(expected, HoldStatusText.Effective(status, Expiry, Expiry.AddDays(1)));
}
