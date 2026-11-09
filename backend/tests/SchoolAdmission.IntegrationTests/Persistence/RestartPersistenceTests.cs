using System.Net;
using System.Text.Json.Nodes;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Persistence;

/// <summary>
/// R6 at application level: stop the API (dispose the host) and start a fresh one on the same database —
/// everything is still there and the rules keep working. (`docker compose down`/`up` keeps the database
/// itself through the named volume; that part is verified on the real stack.)
/// </summary>
public sealed class RestartPersistenceTests : IAsyncLifetime
{
    private const string Alice = "20152691234500001";
    private const string Bob = "20152691234500002";
    private const string Carol = "20152691234500003";

    private readonly PostgresDatabase _db = new();

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    /// <summary>Response body without the volatile serverTime field.</summary>
    private static async Task<string> SnapshotAsync(HttpClient client, string path, string? studentId = null)
    {
        using var res = studentId is null ? await client.GetAsync(path) : await client.MyHoldsAsync(studentId);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var node = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
        if (node is JsonObject obj) obj.Remove("serverTime");
        return node.ToJsonString();
    }

    [Fact]
    public async Task DataAndRules_SurviveAnApplicationRestart()
    {
        Guid classId, heldId, confirmedId, cancelledId;
        string[] before;

        // ---- First run of the application --------------------------------------------------------
        await using (var first = new AdmissionApiFactory(_db.ConnectionString))
        {
            var c = first.CreateClient();
            classId = await c.CreateClassIdAsync("Class 6 - Morning Shift", 3);
            heldId = await c.CreateHoldIdAsync(classId, Alice);
            confirmedId = await c.CreateHoldIdAsync(classId, Bob);
            (await c.ConfirmAsync(confirmedId, Bob)).Dispose();
            cancelledId = await c.CreateHoldIdAsync(classId, Carol);
            (await c.CancelAsync(cancelledId, Carol)).Dispose();

            before =
            [
                await SnapshotAsync(c, "/classes"),
                await SnapshotAsync(c, $"/classes/{classId}"),
                await SnapshotAsync(c, $"/holds/{heldId}"),
                await SnapshotAsync(c, $"/holds/{confirmedId}"),
                await SnapshotAsync(c, $"/holds/{cancelledId}"),
                await SnapshotAsync(c, "/me/holds", Bob),
            ];
        } // host stopped: connection pools, DI container and in-memory state are gone

        // ---- Second, independent run on the same database ------------------------------------------
        await using var second = new AdmissionApiFactory(_db.ConnectionString);
        var client = second.CreateClient();

        string[] after =
        [
            await SnapshotAsync(client, "/classes"),
            await SnapshotAsync(client, $"/classes/{classId}"),
            await SnapshotAsync(client, $"/holds/{heldId}"),
            await SnapshotAsync(client, $"/holds/{confirmedId}"),
            await SnapshotAsync(client, $"/holds/{cancelledId}"),
            await SnapshotAsync(client, "/me/holds", Bob),
        ];
        Assert.Equal(before, after);
        Assert.Equal(1L, await _db.ScalarAsync<long>("SELECT count(*) FROM \"__EFMigrationsHistory\"")); // not re-applied

        // The rules keep working on the old data.
        using (var dup = await client.HoldAsync(classId, Alice))
            await dup.AssertProblemAsync(HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS");
        using (var admitted = await client.HoldAsync(classId, Bob))
            await admitted.AssertProblemAsync(HttpStatusCode.Conflict, "ALREADY_ADMITTED");
        await client.CreateHoldIdAsync(classId, Carol); // the cancelled seat is free → class now full
        using (var full = await client.HoldAsync(classId, "20152691234500004"))
            await full.AssertProblemAsync(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE");
        using (var pay = await client.ConfirmAsync(heldId, Alice))
            Assert.Equal(HttpStatusCode.OK, pay.StatusCode);   // a hold from before the restart can be paid
        Assert.Equal((1, 2, 0), await client.SeatCountsAsync(classId));
        await Db.AssertInvariantAsync(_db.ConnectionString, second.Clock.GetUtcNow());
    }

    [Fact]
    public async Task HoldThatExpiresWhileTheAppIsDown_IsExpiredAfterRestart_AndItsSeatIsFree()
    {
        Guid classId, holdId;
        DateTimeOffset expiresAt;
        await using (var first = new AdmissionApiFactory(_db.ConnectionString))
        {
            var c = first.CreateClient();
            classId = await c.CreateClassIdAsync("Class 7 - Day Shift", 1);
            using var res = await c.HoldAsync(classId, Alice);
            var json = await res.ReadJsonAsync();
            holdId = Guid.Parse(json.GetProperty("holdId").GetString()!);
            expiresAt = json.GetProperty("expiresAt").GetDateTimeOffset();
        }

        // The application comes back after the hold's deadline (no sweeper ever ran).
        await using var second = new AdmissionApiFactory(_db.ConnectionString);
        second.Clock.SetUtcNow(expiresAt.AddSeconds(1));
        var client = second.CreateClient();

        Assert.Equal("EXPIRED", await client.HoldStatusAsync(holdId));
        Assert.Equal((0, 0, 1), await client.SeatCountsAsync(classId));
        await client.CreateHoldIdAsync(classId, Bob); // the seat is available to someone else

        var mine = Assert.Single((await (await client.MyHoldsAsync(Alice)).ReadJsonAsync()).EnumerateArray());
        Assert.Equal(holdId.ToString(), mine.GetProperty("holdId").GetString());
        Assert.Equal("EXPIRED", mine.GetProperty("status").GetString()); // Alice still sees her application
    }
}
