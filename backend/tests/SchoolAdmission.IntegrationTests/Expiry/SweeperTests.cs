using System.Net;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Holds.ExpireStaleHolds;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Expiry;

/// <summary>A host with the real background sweeper on a 1-second interval, driven by the fake clock.</summary>
public sealed class SweeperOnFixture : ApiFixture
{
    protected override bool SweeperEnabled => true;
    protected override int SweepIntervalSeconds => 1;
}

public sealed class BackgroundSweeperTests(SweeperOnFixture api) : IClassFixture<SweeperOnFixture>
{
    [Fact]
    public async Task BackgroundSweeper_StoresExpired_OnItsNextTickAfterExpiry()
    {
        var classId = await api.Client.CreateClassIdAsync(TestData.ClassName(), 2);
        var holdId = await api.Client.CreateHoldIdAsync(classId, TestData.StudentId(1));

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(601)); // past expiresAt; fires the 1 s periodic timer

        var stored = await PollAsync(() => api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{holdId}'"),
            s => s == "EXPIRED");
        Assert.Equal("EXPIRED", stored);
    }

    /// <summary>The timer fires on the fake clock, but the sweep itself runs asynchronously on a real thread.</summary>
    private static async Task<T> PollAsync<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        T value;
        do
        {
            value = await read();
            if (done(value)) return value;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);
        return value;
    }
}

public sealed class SweeperOffTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task DisabledSweeper_NeverTouchesRows_ButSeatsStillReturn()
    {
        var classId = await api.Client.CreateClassIdAsync(TestData.ClassName(), 1);
        var holdId = await api.Client.CreateHoldIdAsync(classId, TestData.StudentId(1));

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(601));
        await Task.Delay(1500); // give a (wrongly) running sweeper time to act

        Assert.Equal("HELD", await api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{holdId}'"));
        Assert.Equal((0, 0, 1), await api.Client.SeatCountsAsync(classId)); // R2 holds without any sweeper
    }
}

/// <summary>Several sweepers at once (e.g. two backend instances): SKIP LOCKED splits the work, nothing blocks.</summary>
[Collection(ConcurrencyCollection.Name)]
public sealed class ParallelSweepTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task ParallelSweeps_ExpireEveryStaleHoldExactlyOnce()
    {
        var classId = await api.Client.CreateClassIdAsync(TestData.ClassName("Sweep Race"), 300);
        var results = await Burst.RunAsync(300, i => api.Client.HoldAsync(classId, TestData.StudentId(60_000 + i)));
        Assert.Equal(300, results.CountOf(HttpStatusCode.Created));
        api.Factory.Clock.Advance(TimeSpan.FromSeconds(600));

        var sweeps = Enumerable.Range(0, 6).Select(async _ =>
        {
            var total = 0;
            int expired;
            do
            {
                await using var scope = api.Factory.Services.CreateAsyncScope();
                expired = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireStaleHoldsCommand(25));
                total += expired;
            } while (expired > 0);
            return total;
        });
        var totals = await Task.WhenAll(sweeps);

        Assert.Equal(300, totals.Sum()); // each row expired by exactly one sweeper
        Assert.Equal(300L, await api.Database.ScalarAsync<long>(
            $"SELECT count(*) FROM holds WHERE class_id = '{classId}' AND status = 'EXPIRED' AND expired_at IS NOT NULL"));
    }

    [Fact]
    public async Task ConfirmRacingSweep_AtExpiry_AlwaysEndsExpired()
    {
        var classId = await api.Client.CreateClassIdAsync(TestData.ClassName("Expiry Race"), 1);
        var student = TestData.StudentId(61_000);
        var holdId = await api.Client.CreateHoldIdAsync(classId, student);
        api.Factory.Clock.Advance(TimeSpan.FromSeconds(600)); // exactly expiresAt

        var confirms = Burst.RunAsync(10, _ => api.Client.ConfirmAsync(holdId, student));
        var sweeps = Enumerable.Range(0, 5).Select(async _ =>
        {
            await using var scope = api.Factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ExpireStaleHoldsCommand(500));
        }).ToArray();
        var confirmResults = await confirms;
        await Task.WhenAll(sweeps);

        confirmResults.AssertNoServerErrors();
        Assert.All(confirmResults, r => Assert.Equal("HOLD_EXPIRED", r.Code));
        Assert.Equal("EXPIRED", await api.Client.HoldStatusAsync(holdId));
        Assert.Equal((0, 0, 1), await api.Client.SeatCountsAsync(classId));
    }
}
