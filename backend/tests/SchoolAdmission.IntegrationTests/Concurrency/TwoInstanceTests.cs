using System.Net;
using Npgsql;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Concurrency;

/// <summary>
/// Two independent API hosts (separate DI containers, separate Npgsql pools, separate clocks) on ONE
/// database — the in-process equivalent of <c>docker-compose.multi.yml</c> (bonus B1).
/// </summary>
public sealed class TwoInstanceFixture : IAsyncLifetime
{
    public const int HoldSeconds = 60;

    public PostgresDatabase Database { get; } = new();
    public AdmissionApiFactory A { get; private set; } = default!;
    public AdmissionApiFactory B { get; private set; } = default!;
    public HttpClient ClientA { get; private set; } = default!;
    public HttpClient ClientB { get; private set; } = default!;

    /// <summary>A distinct Application Name per instance: Npgsql pools by connection string, so they get separate pools.</summary>
    public string ConnectionStringFor(string instance) =>
        new NpgsqlConnectionStringBuilder(Database.ConnectionString) { ApplicationName = instance }.ConnectionString;

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        A = new AdmissionApiFactory(ConnectionStringFor("instance-a"), HoldSeconds);
        B = new AdmissionApiFactory(ConnectionStringFor("instance-b"), HoldSeconds);

        // Start both at the same time on the empty database: both race to apply the migrations.
        var clients = await Task.WhenAll(Task.Run(A.CreateClient), Task.Run(B.CreateClient));
        ClientA = clients[0];
        ClientB = clients[1];
    }

    /// <summary>Moves both instances' clocks together (each host has its own FakeTimeProvider).</summary>
    public void Advance(TimeSpan by)
    {
        A.Clock.Advance(by);
        B.Clock.Advance(by);
    }

    public DateTimeOffset Now => A.Clock.GetUtcNow() > B.Clock.GetUtcNow() ? A.Clock.GetUtcNow() : B.Clock.GetUtcNow();

    public async Task DisposeAsync()
    {
        ClientA.Dispose();
        ClientB.Dispose();
        await A.DisposeAsync();
        await B.DisposeAsync();
        await Database.DisposeAsync();
    }
}

[Collection(ConcurrencyCollection.Name)]
public sealed class TwoInstanceTests(TwoInstanceFixture fx) : IClassFixture<TwoInstanceFixture>
{
    private HttpClient Instance(int i) => i % 2 == 0 ? fx.ClientA : fx.ClientB;

    [Fact]
    public async Task BothInstances_StartTogether_OnAnEmptyDatabase_AndApplyMigrationsOnce()
    {
        Assert.Equal(HttpStatusCode.OK, (await fx.ClientA.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fx.ClientB.GetAsync("/health")).StatusCode);

        var rows = await fx.Database.ScalarAsync<long>("""SELECT count(*) FROM "__EFMigrationsHistory" """);
        var distinct = await fx.Database.ScalarAsync<long>("""SELECT count(DISTINCT "MigrationId") FROM "__EFMigrationsHistory" """);
        Assert.True(rows > 0);
        Assert.Equal(distinct, rows);
    }

    [Fact]
    public async Task TwoInstances_UseSeparateConnectionPools()
    {
        await fx.ClientA.GetAsync("/classes");
        await fx.ClientB.GetAsync("/classes");

        var names = await fx.Database.ScalarAsync<string>(
            $"SELECT string_agg(DISTINCT application_name, ',' ORDER BY application_name) FROM pg_stat_activity " +
            $"WHERE datname = '{fx.Database.Name}' AND application_name LIKE 'instance-%'");

        Assert.Equal("instance-a,instance-b", names);
    }

    [Fact]
    public async Task TwoInstances_ShareDatabase_NeverOversell()
    {
        var classId = await fx.ClientA.CreateClassIdAsync(TestData.ClassName("Two Instances"), totalSeats: 30);

        // 200 different students, alternating between the two instances, released at the same moment.
        var results = await Burst.RunAsync(200, i => Instance(i).HoldAsync(classId, TestData.StudentId(10_000 + i)));

        results.AssertNoServerErrors();
        Assert.Equal(30, results.CountOf(HttpStatusCode.Created));
        Assert.Equal(170, results.CountOf(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE"));
        Assert.True(results.Where((r, i) => i % 2 == 0).Any(r => r.Status == HttpStatusCode.Created) &&
                    results.Where((r, i) => i % 2 == 1).Any(r => r.Status == HttpStatusCode.Created),
            "Both instances should have won some seats; otherwise the race wasn't really across instances.");
        Assert.Equal((30, 0, 0), await fx.ClientA.SeatCountsAsync(classId));
        Assert.Equal((30, 0, 0), await fx.ClientB.SeatCountsAsync(classId));
        await Db.AssertInvariantAsync(fx.Database.ConnectionString, fx.Now);
    }

    [Fact]
    public async Task TwoInstances_LastSeat_ExactlyOneWinner()
    {
        var classId = await fx.ClientB.CreateClassIdAsync(TestData.ClassName("Two Instances Last Seat"), totalSeats: 1);

        var results = await Burst.RunAsync(100, i => Instance(i).HoldAsync(classId, TestData.StudentId(20_000 + i)));

        results.AssertNoServerErrors();
        Assert.Equal(1, results.CountOf(HttpStatusCode.Created));
        Assert.Equal(99, results.CountOf(HttpStatusCode.Conflict, "NO_SEATS_AVAILABLE"));
        await Db.AssertInvariantAsync(fx.Database.ConnectionString, fx.Now);
    }

    [Fact]
    public async Task TwoInstances_SameStudent_OnlyOneActiveHold()
    {
        var classId = await fx.ClientA.CreateClassIdAsync(TestData.ClassName("Two Instances Same Student"), totalSeats: 10);
        var student = TestData.StudentId(30_000);

        var results = await Burst.RunAsync(60, i => Instance(i).HoldAsync(classId, student));

        results.AssertNoServerErrors();
        Assert.Equal(1, results.CountOf(HttpStatusCode.Created));
        Assert.Equal(59, results.CountOf(HttpStatusCode.Conflict, "ACTIVE_HOLD_EXISTS"));
        Assert.Equal((1, 0, 9), await fx.ClientB.SeatCountsAsync(classId));
    }

    [Fact]
    public async Task TwoInstances_ConcurrentConfirms_CountOnce()
    {
        var classId = await fx.ClientA.CreateClassIdAsync(TestData.ClassName("Two Instances Confirm"), totalSeats: 5);
        var student = TestData.StudentId(40_000);
        var holdId = await fx.ClientA.CreateHoldIdAsync(classId, student);

        var results = await Burst.RunAsync(40, i => Instance(i).ConfirmAsync(holdId, student));

        results.AssertNoServerErrors();
        Assert.Equal(40, results.CountOf(HttpStatusCode.OK)); // idempotent on both instances
        Assert.Equal((0, 1, 4), await fx.ClientB.SeatCountsAsync(classId));
        Assert.Equal("CONFIRMED", await fx.ClientA.HoldStatusAsync(holdId));
        await Db.AssertInvariantAsync(fx.Database.ConnectionString, fx.Now);
    }

    [Fact]
    public async Task TwoInstances_ConfirmRacingCancel_ExactlyOneOutcome()
    {
        var classId = await fx.ClientA.CreateClassIdAsync(TestData.ClassName("Two Instances Pay vs Cancel"), totalSeats: 20);
        var students = Enumerable.Range(0, 20).Select(i => TestData.StudentId(50_000 + i)).ToArray();
        var holds = new Guid[students.Length];
        for (var i = 0; i < students.Length; i++) holds[i] = await fx.ClientA.CreateHoldIdAsync(classId, students[i]);

        // For every hold: pay on instance A and cancel on instance B at the same moment.
        var results = await Burst.RunAsync(40, i => i % 2 == 0
            ? fx.ClientA.ConfirmAsync(holds[i / 2], students[i / 2])
            : fx.ClientB.CancelAsync(holds[i / 2], students[i / 2]));

        results.AssertNoServerErrors();
        var confirmed = 0;
        for (var h = 0; h < holds.Length; h++)
        {
            var pay = results[2 * h];
            var cancel = results[2 * h + 1];
            var status = await fx.ClientB.HoldStatusAsync(holds[h]);
            if (status == "CONFIRMED")
            {
                confirmed++;
                Assert.Equal(HttpStatusCode.OK, pay.Status);
                Assert.Equal((HttpStatusCode.Conflict, "HOLD_ALREADY_CONFIRMED"), (cancel.Status, cancel.Code));
            }
            else
            {
                Assert.Equal("CANCELLED", status);
                Assert.Equal(HttpStatusCode.NoContent, cancel.Status);
                Assert.Equal((HttpStatusCode.Conflict, "HOLD_CANCELLED"), (pay.Status, pay.Code));
            }
        }

        Assert.Equal((0, confirmed, 20 - confirmed), await fx.ClientA.SeatCountsAsync(classId));
        await Db.AssertInvariantAsync(fx.Database.ConnectionString, fx.Now);
    }

    [Fact]
    public async Task SeatReleasedOnOneInstance_IsAvailableOnTheOtherAtOnce()
    {
        var classId = await fx.ClientA.CreateClassIdAsync(TestData.ClassName("Two Instances Release"), totalSeats: 1);
        var first = TestData.StudentId(60_000);
        var second = TestData.StudentId(60_001);
        var holdId = await fx.ClientA.CreateHoldIdAsync(classId, first);
        Assert.Equal(HttpStatusCode.Conflict, (await fx.ClientB.HoldAsync(classId, second)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await fx.ClientA.CancelAsync(holdId, first)).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await fx.ClientB.HoldAsync(classId, second)).StatusCode);
        Assert.Equal((1, 0, 0), await fx.ClientA.SeatCountsAsync(classId));
    }

    [Fact]
    public async Task HoldMadeOnOneInstance_ExpiresForTheOther_WithoutAnyApiCall()
    {
        var classId = await fx.ClientA.CreateClassIdAsync(TestData.ClassName("Two Instances Expiry"), totalSeats: 1);
        var first = TestData.StudentId(70_000);
        var second = TestData.StudentId(70_001);
        var holdId = await fx.ClientA.CreateHoldIdAsync(classId, first);

        fx.Advance(TimeSpan.FromSeconds(TwoInstanceFixture.HoldSeconds + 1));

        Assert.Equal("EXPIRED", await fx.ClientB.HoldStatusAsync(holdId));
        Assert.Equal((0, 0, 1), await fx.ClientB.SeatCountsAsync(classId));
        var late = await fx.ClientB.ConfirmAsync(holdId, first);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await fx.ClientB.HoldAsync(classId, second)).StatusCode);
        await Db.AssertInvariantAsync(fx.Database.ConnectionString, fx.Now);
    }
}
