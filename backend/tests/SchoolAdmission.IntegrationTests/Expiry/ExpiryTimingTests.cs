using System.Net;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Holds.ExpireStaleHolds;
using SchoolAdmission.IntegrationTests.Contract;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Expiry;

/// <summary>
/// R2: a hold expires exactly at expiresAt by itself — statuses and seat counts flip at that instant with the
/// sweeper disabled. The sweeper only rewrites stored rows. Time is driven by FakeTimeProvider.
/// </summary>
public sealed class ExpiryTimingTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private static int _student = 50_000;
    private static string NewStudent() => TestData.StudentId(Interlocked.Increment(ref _student));
    private static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(600);

    private Task<string?> StoredStatusAsync(Guid holdId) =>
        api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{holdId}'");

    private async Task<int> SweepAsync()
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireStaleHoldsCommand(500));
    }

    private async Task<string> MyStatusAsync(string student, Guid holdId)
    {
        var list = await (await _client.MyHoldsAsync(student)).ReadJsonAsync();
        return list.EnumerateArray().Single(e => e.GetProperty("holdId").GetString() == holdId.ToString())
            .GetProperty("status").GetString()!;
    }

    [Fact]
    public async Task Hold_ExpiresAtDeadline_WithoutSweeper()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 2);
        var student = NewStudent();
        var holdId = await _client.CreateHoldIdAsync(classId, student);
        Assert.Equal((1, 0, 1), await _client.SeatCountsAsync(classId));

        api.Factory.Clock.Advance(HoldDuration);

        Assert.Equal("EXPIRED", await _client.HoldStatusAsync(holdId));
        Assert.Equal("EXPIRED", await MyStatusAsync(student, holdId));
        Assert.Equal((0, 0, 2), await _client.SeatCountsAsync(classId)); // seat returned by itself
        Assert.Equal("HELD", await StoredStatusAsync(holdId));            // nothing touched the row
    }

    [Fact]
    public async Task Hold_StillHeld_OneSecondBefore()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 2);
        var student = NewStudent();
        var holdId = await _client.CreateHoldIdAsync(classId, student);

        api.Factory.Clock.Advance(HoldDuration - TimeSpan.FromSeconds(1));

        Assert.Equal("HELD", await _client.HoldStatusAsync(holdId));
        Assert.Equal("HELD", await MyStatusAsync(student, holdId));
        Assert.Equal((1, 0, 1), await _client.SeatCountsAsync(classId));
    }

    [Fact]
    public async Task ConfirmAndCancel_AfterExpiry_Return409HoldExpired()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 2);
        var student = NewStudent();
        var holdId = await _client.CreateHoldIdAsync(classId, student);
        api.Factory.Clock.Advance(HoldDuration);

        using var confirm = await _client.ConfirmAsync(holdId, student);
        using var cancel = await _client.CancelAsync(holdId, student);

        await confirm.AssertProblemAsync(HttpStatusCode.Conflict, "HOLD_EXPIRED");
        await cancel.AssertProblemAsync(HttpStatusCode.Conflict, "HOLD_EXPIRED");
        Assert.Equal((0, 0, 2), await _client.SeatCountsAsync(classId));
    }

    [Fact]
    public async Task Reapply_AfterExpiry_Succeeds_ForSameStudentAndOthers()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 1);
        var student = NewStudent();
        var first = await _client.CreateHoldIdAsync(classId, student);
        using (var full = await _client.HoldAsync(classId, NewStudent()))
            await full.AssertProblemAsync(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");

        api.Factory.Clock.Advance(HoldDuration);
        var second = await _client.CreateHoldIdAsync(classId, student); // stale row materialized, index satisfied

        Assert.NotEqual(first, second);
        Assert.Equal("EXPIRED", await StoredStatusAsync(first));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, api.Factory.Clock.GetUtcNow());
    }

    [Fact]
    public async Task Sweeper_MaterializesExpiredRows_WithoutChangingWhatClientsSee()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Sweep"), 10);
        var stale = await _client.CreateHoldIdAsync(classId, NewStudent());
        var confirmedStudent = NewStudent();
        var confirmed = await _client.CreateHoldIdAsync(classId, confirmedStudent);
        var cancelledStudent = NewStudent();
        var cancelled = await _client.CreateHoldIdAsync(classId, cancelledStudent);
        (await _client.ConfirmAsync(confirmed, confirmedStudent)).Dispose();
        (await _client.CancelAsync(cancelled, cancelledStudent)).Dispose();
        api.Factory.Clock.Advance(HoldDuration);
        var fresh = await _client.CreateHoldIdAsync(classId, NewStudent()); // created after the others expired

        var before = await (await _client.GetAsync($"/classes/{classId}")).Content.ReadAsStringAsync();
        var staleBefore = await _client.HoldStatusAsync(stale);

        Assert.True(await SweepAsync() >= 1);

        Assert.Equal("EXPIRED", await StoredStatusAsync(stale));
        Assert.Equal("CONFIRMED", await StoredStatusAsync(confirmed));
        Assert.Equal("CANCELLED", await StoredStatusAsync(cancelled));
        Assert.Equal("HELD", await StoredStatusAsync(fresh));
        // expired_at is the sweep's clock time (PostgreSQL keeps microseconds; the fake clock has 100 ns ticks).
        var expiredAt = await api.Database.ScalarAsync<DateTime>($"SELECT expired_at FROM holds WHERE id = '{stale}'");
        Assert.InRange((expiredAt - api.Factory.Clock.GetUtcNow().UtcDateTime).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));

        // Clients see exactly the same thing before and after the sweep.
        Assert.Equal(before, await (await _client.GetAsync($"/classes/{classId}")).Content.ReadAsStringAsync());
        Assert.Equal(staleBefore, await _client.HoldStatusAsync(stale));

        // Nothing left to do: a second sweep changes nothing.
        Assert.Equal(0, await SweepAsync());
    }

    [Fact]
    public async Task Sweeper_LeavesHoldsThatHaveNotExpiredYet()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 2);
        var holdId = await _client.CreateHoldIdAsync(classId, NewStudent());

        api.Factory.Clock.Advance(HoldDuration - TimeSpan.FromSeconds(1));
        await SweepAsync();

        Assert.Equal("HELD", await StoredStatusAsync(holdId));
        Assert.Equal("HELD", await _client.HoldStatusAsync(holdId));
    }
}

/// <summary>Expiry follows HOLD_DURATION_SECONDS (30 s here).</summary>
public sealed class ConfiguredDurationExpiryTests(ShortHoldFixture api) : IClassFixture<ShortHoldFixture>
{
    [Fact]
    public async Task Hold_ExpiresAfterConfiguredDuration()
    {
        var classId = await api.Client.CreateClassIdAsync(TestData.ClassName(), 1);
        var holdId = await api.Client.CreateHoldIdAsync(classId, TestData.StudentId(1));

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal("HELD", await api.Client.HoldStatusAsync(holdId));

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("EXPIRED", await api.Client.HoldStatusAsync(holdId));
        Assert.Equal((0, 0, 1), await api.Client.SeatCountsAsync(classId));
    }
}
