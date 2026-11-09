using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Concurrency;

/// <summary>R5 (confirm counted once) and R1 while confirms, cancels and applications race each other.</summary>
[Collection(ConcurrencyCollection.Name)]
public sealed class LifecycleConcurrencyTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private DateTimeOffset Now => api.Factory.Clock.GetUtcNow();

    [Fact]
    public async Task ConcurrentConfirms_AllReturn200_AndCountOnce()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Confirm Race"), 3);
        var student = TestData.StudentId(40_000);
        var holdId = await _client.CreateHoldIdAsync(classId, student);

        var bodies = new System.Collections.Concurrent.ConcurrentBag<string>();
        var results = await Burst.RunAsync(30, async _ =>
        {
            var res = await _client.ConfirmAsync(holdId, student);
            bodies.Add(await res.Content.ReadAsStringAsync());
            return res;
        });

        results.AssertNoServerErrors();
        Assert.Equal(30, results.CountOf(HttpStatusCode.OK));
        Assert.Single(bodies.Distinct()); // same holdId, status and confirmedAt every time
        Assert.Equal((0, 1, 2), await _client.SeatCountsAsync(classId));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }

    [Fact]
    public async Task ConfirmRacingCancel_ExactlyOneOutcomeWins()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Confirm vs Cancel"), 3);
        var student = TestData.StudentId(40_001);
        var holdId = await _client.CreateHoldIdAsync(classId, student);

        var results = await Burst.RunAsync(40, i => i % 2 == 0
            ? _client.ConfirmAsync(holdId, student)
            : _client.CancelAsync(holdId, student));

        results.AssertNoServerErrors();
        var confirms = results.Where(r => r.Index % 2 == 0).ToList();
        var cancels = results.Where(r => r.Index % 2 == 1).ToList();
        var final = await _client.HoldStatusAsync(holdId);
        if (final == "CONFIRMED")
        {
            Assert.All(confirms, r => Assert.Equal(HttpStatusCode.OK, r.Status));
            Assert.All(cancels, r => Assert.Equal("HOLD_ALREADY_CONFIRMED", r.Code));
            Assert.Equal((0, 1, 2), await _client.SeatCountsAsync(classId));
        }
        else
        {
            Assert.Equal("CANCELLED", final);
            Assert.All(cancels, r => Assert.Equal(HttpStatusCode.NoContent, r.Status));
            Assert.All(confirms, r => Assert.Equal("HOLD_CANCELLED", r.Code));
            Assert.Equal((0, 0, 3), await _client.SeatCountsAsync(classId));
        }
    }

    [Fact]
    public async Task MixedBurst_ApplyConfirmCancel_KeepsInvariant()
    {
        const int seats = 10;
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Mixed Lifecycle"), seats);
        var holders = new List<(Guid holdId, string student)>();
        for (var i = 0; i < seats; i++)
        {
            var student = TestData.StudentId(41_000 + i);
            holders.Add((await _client.CreateHoldIdAsync(classId, student), student));
        }

        // 300 requests at once: the 10 holders confirm (even) or cancel (odd), several times each,
        // while 100 newcomers apply twice each for the seats that cancellations free.
        var results = await Burst.RunAsync(300, i =>
        {
            if (i < 100)
            {
                var (holdId, student) = holders[i % seats];
                return i % seats % 2 == 0 ? _client.ConfirmAsync(holdId, student) : _client.CancelAsync(holdId, student);
            }
            return _client.HoldAsync(classId, TestData.StudentId(42_000 + (i - 100) / 2));
        });

        results.AssertNoServerErrors();
        var newHolds = results.CountOf(HttpStatusCode.Created);
        Assert.InRange(newHolds, 0, 5);                                   // at most the 5 cancelled seats
        Assert.Equal((newHolds, 5, 5 - newHolds), await _client.SeatCountsAsync(classId));
        Assert.All(results.Where(r => r.Index >= 100 && r.Status != HttpStatusCode.Created),
            r => Assert.Contains(r.Code, new[] { "NO_SEATS_AVAILABLE", "ACTIVE_HOLD_EXISTS" }));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }
}
