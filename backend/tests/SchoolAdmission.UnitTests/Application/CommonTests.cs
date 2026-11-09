using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SchoolAdmission.Application;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Common.Options;
using SchoolAdmission.Application.Common.Time;
using SchoolAdmission.Application.Common.Validation;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.UnitTests.Application;

public sealed class TruncateToSecondsTests
{
    [Fact]
    public void DropsFractionalSeconds() =>
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 4, 3, 12, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 1, 4, 3, 12, 999, TimeSpan.Zero).AddTicks(9_999).TruncateToSeconds());

    [Fact]
    public void NormalizesToUtc()
    {
        var result = new DateTimeOffset(2026, 11, 1, 10, 3, 12, 500, TimeSpan.FromHours(6)).TruncateToSeconds();

        Assert.Equal(TimeSpan.Zero, result.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 4, 3, 12, TimeSpan.Zero), result);
    }
}

public sealed class RouteIdTests
{
    [Fact]
    public void ValidUuid_Parses()
    {
        Assert.True(RouteId.TryParse("0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e", out var id));
        Assert.Equal(Guid.Parse("0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e"), id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("c_123")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("0193a6f2-7c1e-7b4a-9d3e-2f1a5c6b7d8e-extra")]
    public void AnythingElse_IsRejected(string? raw) => Assert.False(RouteId.TryParse(raw, out _));
}

public sealed class StudentIdRuleTests
{
    private sealed record Req(string? StudentId);
    private sealed class V : AbstractValidator<Req> { public V() => RuleFor(x => x.StudentId).ValidStudentId(); }

    [Theory]
    [InlineData("20152691234567890")]
    [InlineData("  20152691234567890\t")] // surrounding whitespace is trimmed
    public void Accepts17Digits(string value) => Assert.True(new V().Validate(new Req(value)).IsValid);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2015269123456789")]
    [InlineData("201526912345678901")]
    [InlineData("2015 2691234567890")]
    [InlineData("abcdefghijklmnopq")]
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০")]
    public void RejectsWithInvalidStudentIdCode(string? value)
    {
        var result = new V().Validate(new Req(value));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(ErrorCodes.InvalidStudentId, failure.ErrorCode);
        Assert.Equal("X-Student-Id", failure.PropertyName);
    }
}

public sealed class HoldOptionsTests
{
    private static HoldOptions Resolve(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddApplication();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<HoldOptions>>().Value;
    }

    [Fact]
    public void Defaults_Are600s_5s_Enabled()
    {
        var o = Resolve([]);

        Assert.Equal(600, o.HoldDurationSeconds);
        Assert.Equal(5, o.SweepIntervalSeconds);
        Assert.True(o.SweeperEnabled);
    }

    [Fact]
    public void ReadsEnvironmentStyleKeys()
    {
        var o = Resolve(new()
        {
            ["HOLD_DURATION_SECONDS"] = "30",
            ["HOLD_SWEEP_INTERVAL_SECONDS"] = "2",
            ["HOLD_SWEEPER_ENABLED"] = "false",
        });

        Assert.Equal(30, o.HoldDurationSeconds);
        Assert.Equal(2, o.SweepIntervalSeconds);
        Assert.False(o.SweeperEnabled);
    }

    [Theory]
    [InlineData("HOLD_DURATION_SECONDS", "0")]
    [InlineData("HOLD_DURATION_SECONDS", "86401")]
    [InlineData("HOLD_SWEEP_INTERVAL_SECONDS", "0")]
    [InlineData("HOLD_SWEEP_INTERVAL_SECONDS", "301")]
    public void OutOfRangeValues_FailFast(string key, string value) =>
        Assert.Throws<OptionsValidationException>(() => Resolve(new() { [key] = value }));
}
