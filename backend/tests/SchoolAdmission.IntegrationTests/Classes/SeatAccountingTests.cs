using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Classes;

/// <summary>
/// heldSeats / confirmedSeats / availableSeats derived at request time (API.md §4.2). Holds are seeded through the
/// repositories because the hold endpoints arrive in Phase 05.
/// </summary>
public sealed class SeatAccountingTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private readonly HttpClient _client = api.Client;
    private DateTimeOffset Now => api.Factory.Clock.GetUtcNow();

    private async Task SeedAsync(params Hold[] holds)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IHoldRepository>();
        foreach (var hold in holds) repo.Add(hold);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
    }

    private Hold NewHold(Guid classId, int student, TimeSpan createdAgo, int minutes = 10) =>
        Hold.Create(Guid.CreateVersion7(), classId, TestData.StudentId(student), Now - createdAgo, TimeSpan.FromMinutes(minutes));

    private async Task<(int held, int confirmed, int available)> CountsAsync(Guid classId)
    {
        var json = await (await _client.GetAsync($"/classes/{classId}")).ReadJsonAsync();
        return (json.GetProperty("heldSeats").GetInt32(), json.GetProperty("confirmedSeats").GetInt32(),
            json.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Counts_IncludeOnlyActiveHeldAndConfirmed()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 10);
        var confirmed = NewHold(classId, 3, TimeSpan.FromMinutes(5));
        confirmed.Confirm(Now);
        var cancelled = NewHold(classId, 4, TimeSpan.Zero);
        cancelled.Cancel(Now);
        var storedExpired = NewHold(classId, 5, TimeSpan.FromMinutes(30));
        storedExpired.Expire(Now);

        await SeedAsync(
            NewHold(classId, 1, TimeSpan.Zero),                 // held, active
            NewHold(classId, 2, TimeSpan.FromMinutes(9)),       // held, active for 1 more minute
            NewHold(classId, 6, TimeSpan.FromMinutes(11)),      // held but past expiresAt → not counted (no sweeper ran)
            confirmed, cancelled, storedExpired);

        Assert.Equal((2, 1, 7), await CountsAsync(classId));
    }

    [Fact]
    public async Task ExpiredHold_StopsCounting_ExactlyAtExpiresAt_WithoutSweeper()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 2);
        await SeedAsync(NewHold(classId, 1, TimeSpan.Zero, minutes: 10));

        Assert.Equal((1, 0, 1), await CountsAsync(classId));

        api.Factory.Clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        Assert.Equal((1, 0, 1), await CountsAsync(classId));   // one second before: still held

        api.Factory.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal((0, 0, 2), await CountsAsync(classId));   // at expiresAt: seat is back
    }

    [Fact]
    public async Task ListAndDetail_AgreeOnCounts()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 5);
        await SeedAsync(NewHold(classId, 1, TimeSpan.Zero), NewHold(classId, 2, TimeSpan.Zero));

        var list = await (await _client.GetAsync("/classes")).ReadJsonAsync();
        var item = list.EnumerateArray().Single(e => e.GetProperty("id").GetString() == classId.ToString());

        Assert.Equal(2, item.GetProperty("heldSeats").GetInt32());
        Assert.Equal(3, item.GetProperty("availableSeats").GetInt32());
        Assert.Equal((2, 0, 3), await CountsAsync(classId));
    }

    [Fact]
    public async Task AvailableSeats_NeverNegative()
    {
        // Only reachable by writing around the API (the create-hold lock prevents it); the clamp is defense in depth.
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 1);
        await SeedAsync(NewHold(classId, 1, TimeSpan.Zero), NewHold(classId, 2, TimeSpan.Zero));

        Assert.Equal((2, 0, 0), await CountsAsync(classId));
    }

    [Fact]
    public async Task Counts_AreJsonIntegers()
    {
        var classId = await _client.CreateClassIdAsync(TestData.ClassName(), 4);
        var json = await (await _client.GetAsync($"/classes/{classId}")).ReadJsonAsync();

        foreach (var field in new[] { "totalSeats", "heldSeats", "confirmedSeats", "availableSeats" })
            Assert.Equal(JsonValueKind.Number, json.GetProperty(field).ValueKind);
    }
}
