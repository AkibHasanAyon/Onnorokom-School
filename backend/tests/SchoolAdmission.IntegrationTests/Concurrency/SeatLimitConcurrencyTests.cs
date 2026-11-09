using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Concurrency;

/// <summary>
/// R1 (never oversell) and R3 (one active hold per student per class) under simultaneous apply bursts.
/// The required brief test is <see cref="ConcurrentHolds_NeverExceedTotalSeats"/>.
/// </summary>
[Collection(ConcurrencyCollection.Name)]
public sealed class SeatLimitConcurrencyTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private DateTimeOffset Now => api.Factory.Clock.GetUtcNow();

    private async Task<(int held, int confirmed, int available)> CountsAsync(Guid classId)
    {
        var json = await (await _client.GetAsync($"/classes/{classId}")).ReadJsonAsync();
        return (json.GetProperty("heldSeats").GetInt32(), json.GetProperty("confirmedSeats").GetInt32(),
            json.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task ConcurrentHolds_NeverExceedTotalSeats()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Concurrency Class"), totalSeats: 25);

        var results = await Burst.RunAsync(200, i => _client.HoldAsync(classId, TestData.StudentId(i)));

        results.AssertNoServerErrors();
        Assert.Equal(25, results.CountOf(HttpStatusCode.Created));
        Assert.Equal(175, results.CountOf(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE"));
        Assert.Equal(25, results.Where(r => r.HoldId is not null).Select(r => r.HoldId).Distinct().Count());
        Assert.Equal((25, 0, 0), await CountsAsync(classId));
        Assert.Equal(25L, await api.Database.ScalarAsync<long>($"SELECT count(*) FROM holds WHERE class_id = '{classId}'"));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }

    [Fact]
    public async Task ConcurrentHolds_OneSeat_ExactlyOneWinner()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Last Seat"), totalSeats: 1);

        var results = await Burst.RunAsync(100, i => _client.HoldAsync(classId, TestData.StudentId(1_000 + i)));

        results.AssertNoServerErrors();
        Assert.Equal(1, results.CountOf(HttpStatusCode.Created));
        Assert.Equal(99, results.CountOf(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE"));
        Assert.Equal((1, 0, 0), await CountsAsync(classId));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }

    [Fact]
    public async Task ConcurrentHolds_SameStudent_OnlyOneSucceeds()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Same Student"), totalSeats: 10);
        var student = TestData.StudentId(2_000);

        var results = await Burst.RunAsync(50, _ => _client.HoldAsync(classId, student));

        results.AssertNoServerErrors();
        var winner = Assert.Single(results, r => r.Status == HttpStatusCode.Created);
        Assert.Equal(49, results.CountOf(HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS"));
        Assert.Equal((1, 0, 9), await CountsAsync(classId));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
        Assert.NotNull(winner.HoldId);
    }

    [Fact]
    public async Task ConcurrentHolds_AcrossClasses_EachClassCappedIndependently()
    {
        var seats = new[] { 5, 10, 1 };
        var classIds = new List<Guid>();
        foreach (var s in seats) classIds.Add(await _client.CreateClassIdAsync(TestData.ClassName("Multi"), s));

        // 300 requests: 100 students, each applying to all three classes, all at once.
        var results = await Burst.RunAsync(300, i => _client.HoldAsync(classIds[i % 3], TestData.StudentId(3_000 + i / 3)));

        results.AssertNoServerErrors();
        for (var c = 0; c < 3; c++)
        {
            var forClass = results.Where(r => r.Index % 3 == c).ToList();
            Assert.Equal(seats[c], forClass.CountOf(HttpStatusCode.Created));
            Assert.Equal(100 - seats[c], forClass.CountOf(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE"));
            Assert.Equal((seats[c], 0, 0), await CountsAsync(classIds[c]));
        }
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }

    [Fact]
    public async Task ConcurrentHolds_MixedNewAndRepeatApplicants_KeepBothRules()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Mixed"), totalSeats: 8);

        // 120 requests from only 40 students (each student fires 3 times).
        var results = await Burst.RunAsync(120, i => _client.HoldAsync(classId, TestData.StudentId(4_000 + i % 40)));

        results.AssertNoServerErrors();
        Assert.Equal(8, results.CountOf(HttpStatusCode.Created));
        Assert.All(results.Where(r => r.Status != HttpStatusCode.Created), r =>
        {
            Assert.Equal(HttpStatusCode.Conflict, r.Status);
            Assert.Contains(r.Code, new[] { "NO_SEATS_AVAILABLE", "ACTIVE_HOLD_EXISTS" });
        });
        var winners = results.Where(r => r.Status == HttpStatusCode.Created).Select(r => r.Index % 40).ToList();
        Assert.Equal(winners.Count, winners.Distinct().Count()); // no student won twice
        Assert.Equal((8, 0, 0), await CountsAsync(classId));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }

    [Fact]
    public async Task Burst_AfterHoldsExpire_ReusesExactlyTheFreedSeats()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName("Refill"), totalSeats: 10);
        var first = await Burst.RunAsync(30, i => _client.HoldAsync(classId, TestData.StudentId(5_000 + i)));
        Assert.Equal(10, first.CountOf(HttpStatusCode.Created));

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(600)); // every hold reaches expiresAt; no sweeper runs

        // The 30 original students (including the 10 whose holds just expired) plus 70 new ones.
        var second = await Burst.RunAsync(100, i => _client.HoldAsync(classId, TestData.StudentId(5_000 + i)));

        second.AssertNoServerErrors();
        Assert.Equal(10, second.CountOf(HttpStatusCode.Created));
        Assert.Equal(90, second.CountOf(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE"));
        Assert.Equal((10, 0, 0), await CountsAsync(classId));
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }
}
