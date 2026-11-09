using Npgsql;
using SchoolAdmission.Domain;
using Xunit.Sdk;

namespace SchoolAdmission.IntegrationTests.Infrastructure;

/// <summary>Tests for the test harness itself: if these helpers were wrong, every other test would be meaningless.</summary>
public sealed class HarnessTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly DateTimeOffset At = new(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StudentIds_AreValidAndDistinct()
    {
        var ids = Enumerable.Range(0, 1000).Select(TestData.StudentId).ToList();

        Assert.All(ids, id => Assert.True(StudentIdentifier.IsValid(id), id));
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void ClassNames_AreUniqueAndWithinLimit()
    {
        var a = TestData.ClassName("Morning");
        var b = TestData.ClassName("Morning");

        Assert.NotEqual(a, b);
        Assert.StartsWith("Morning ", a);
        Assert.True(a.Length <= 100);
    }

    [Fact]
    public async Task Fixture_DatabaseIsMigrated_AndApiResponds()
    {
        using var res = await api.Client.GetAsync("/health");

        Assert.True(res.IsSuccessStatusCode);
        Assert.Equal(1L, await api.Database.ScalarAsync<long>("SELECT count(*) FROM \"__EFMigrationsHistory\""));
    }

    [Fact]
    public async Task Invariant_PassesWhenSeatsAreNotOversold()
    {
        await using var db = new PostgresDatabase();
        await db.InitializeAsync();
        await CreateSchemaWithHoldsAsync(db, seats: 2, activeHolds: 2, expiredHolds: 3);

        await Db.AssertInvariantAsync(db.ConnectionString, At);
    }

    [Fact]
    public async Task Invariant_DetectsOversoldClass()
    {
        await using var db = new PostgresDatabase();
        await db.InitializeAsync();
        await CreateSchemaWithHoldsAsync(db, seats: 1, activeHolds: 2, expiredHolds: 0);

        var ex = await Assert.ThrowsAnyAsync<XunitException>(() => Db.AssertInvariantAsync(db.ConnectionString, At));
        Assert.Contains("R1", ex.Message);
        Assert.Contains("2 held + 0 confirmed > 1 seats", ex.Message);
    }

    [Fact]
    public async Task Invariant_UsesTheGivenTime_NotTheDatabaseClock()
    {
        await using var db = new PostgresDatabase();
        await db.InitializeAsync();
        await CreateSchemaWithHoldsAsync(db, seats: 1, activeHolds: 2, expiredHolds: 0); // active until At + 10 min

        await Db.AssertInvariantAsync(db.ConnectionString, At.AddMinutes(10)); // both expired at that instant
    }

    [Fact]
    public async Task PostgresDatabase_IsIsolatedPerInstance_AndDroppedOnDispose()
    {
        string name;
        await using (var db = new PostgresDatabase())
        {
            await db.InitializeAsync();
            name = db.Name;
            Assert.NotEqual(api.Database.Name, name);
            Assert.Equal(1L, await CountDatabasesAsync(name));
        }

        Assert.Equal(0L, await CountDatabasesAsync(name));
    }

    private static async Task<long> CountDatabasesAsync(string name)
    {
        await using var connection = new NpgsqlConnection(PostgresDatabase.AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @n", connection);
        command.Parameters.AddWithValue("n", name);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Minimal copy of the two tables (no unique index), so an oversold state can be forced.</summary>
    private static async Task CreateSchemaWithHoldsAsync(PostgresDatabase db, int seats, int activeHolds, int expiredHolds)
    {
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        await connection.OpenAsync();
        var classId = Guid.CreateVersion7();
        var sql = $"""
            CREATE TABLE classes (id uuid PRIMARY KEY, name varchar(100) NOT NULL, total_seats int NOT NULL);
            CREATE TABLE holds (id uuid PRIMARY KEY, class_id uuid NOT NULL, student_id varchar(17) NOT NULL,
                                status varchar(16) NOT NULL, expires_at timestamptz NOT NULL);
            INSERT INTO classes VALUES ('{classId}', 'Forced', {seats});
            INSERT INTO holds SELECT gen_random_uuid(), '{classId}', lpad(i::text, 17, '0'), 'HELD', @active
              FROM generate_series(1, {activeHolds}) i;
            INSERT INTO holds SELECT gen_random_uuid(), '{classId}', lpad((100 + i)::text, 17, '0'), 'HELD', @expired
              FROM generate_series(1, {expiredHolds}) i;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("active", At.AddMinutes(10));
        command.Parameters.AddWithValue("expired", At);
        await command.ExecuteNonQueryAsync();
    }
}
