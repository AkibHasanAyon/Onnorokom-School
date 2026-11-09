using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure.Persistence;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Persistence;

public sealed class MigrationTests : IAsyncLifetime
{
    private readonly PostgresDatabase _db = new();

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    [Fact]
    public async Task Startup_AppliesInitialMigration_ToEmptyDatabase()
    {
        await using var factory = new AdmissionApiFactory(_db.ConnectionString);
        using var client = factory.CreateClient();

        Assert.Equal(1L, await _db.ScalarAsync<long>(
            "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '%_InitialCreate'"));
        Assert.Equal(2L, await _db.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('classes','holds')"));
        Assert.Equal(5L, await _db.ScalarAsync<long>(
            """
            SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname IN
              ('ux_classes_normalized_name','ux_holds_class_student_active','ix_holds_class_status_expires',
               'ix_holds_student_created','ix_holds_held_expires')
            """));
    }

    [Fact]
    public async Task TwoInstancesStartingTogether_BothMigrateSafely_AndMigrationRunsOnce()
    {
        await using var a = new AdmissionApiFactory(_db.ConnectionString);
        await using var b = new AdmissionApiFactory(_db.ConnectionString);

        // Starting a host runs the migrator; start both at the same moment.
        var clients = await Task.WhenAll(Task.Run(a.CreateClient), Task.Run(b.CreateClient));

        foreach (var client in clients)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
            client.Dispose();
        }
        Assert.Equal(1L, await _db.ScalarAsync<long>("SELECT count(*) FROM \"__EFMigrationsHistory\""));
    }

    [Fact]
    public async Task Restart_OnMigratedDatabase_IsANoOp_AndKeepsData()
    {
        var classId = Guid.CreateVersion7();
        await using (var first = new AdmissionApiFactory(_db.ConnectionString))
        {
            first.CreateClient().Dispose();
            await using var scope = first.Services.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
            ctx.Classes.Add(SchoolClass.Create(classId, "Class 6 - Morning Shift", 120, DateTimeOffset.UtcNow));
            await ctx.SaveChangesAsync();
        }

        await using var second = new AdmissionApiFactory(_db.ConnectionString);
        second.CreateClient().Dispose();

        Assert.Equal(1L, await _db.ScalarAsync<long>("SELECT count(*) FROM \"__EFMigrationsHistory\""));
        Assert.Equal("Class 6 - Morning Shift", await _db.ScalarAsync<string>($"SELECT name FROM classes WHERE id = '{classId}'"));
    }

    [Fact]
    public async Task Schema_StoresStatusAsContractString_AndEnforcesOneActiveHoldPerStudentPerClass()
    {
        await using var factory = new AdmissionApiFactory(_db.ConnectionString);
        factory.CreateClient().Dispose();
        await using var scope = factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

        var now = new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero);
        var cls = SchoolClass.Create(Guid.CreateVersion7(now), "Class 7 - Day Shift", 10, now);
        var first = Hold.Create(Guid.CreateVersion7(now), cls.Id, "20152691234567890", now, TimeSpan.FromMinutes(10));
        ctx.Classes.Add(cls);
        ctx.Holds.Add(first);
        await ctx.SaveChangesAsync();

        Assert.Equal("HELD", await _db.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{first.Id}'"));

        // A second HELD hold for the same student and class violates the partial unique index (R3 backstop).
        ctx.Holds.Add(Hold.Create(Guid.CreateVersion7(now), cls.Id, "20152691234567890", now, TimeSpan.FromMinutes(10)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        var pg = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);
        Assert.Equal("ux_holds_class_student_active", pg.ConstraintName);
        ctx.ChangeTracker.Clear();

        // Once the first hold is no longer HELD/CONFIRMED, the same student may hold a seat again.
        var reloaded = await ctx.Holds.SingleAsync(h => h.Id == first.Id);
        reloaded.Cancel(now.AddMinutes(1));
        ctx.Holds.Add(Hold.Create(Guid.CreateVersion7(now), cls.Id, "20152691234567890", now.AddMinutes(1), TimeSpan.FromMinutes(10)));
        await ctx.SaveChangesAsync();

        Assert.Equal(2L, await _db.ScalarAsync<long>($"SELECT count(*) FROM holds WHERE class_id = '{cls.Id}'"));
    }

    [Fact]
    public async Task Schema_RejectsDuplicateClassNamesCaseInsensitively()
    {
        await using var factory = new AdmissionApiFactory(_db.ConnectionString);
        factory.CreateClient().Dispose();
        await using var scope = factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var now = DateTimeOffset.UtcNow;

        ctx.Classes.Add(SchoolClass.Create(Guid.CreateVersion7(), "Class 8 - Morning Shift", 5, now));
        await ctx.SaveChangesAsync();
        ctx.Classes.Add(SchoolClass.Create(Guid.CreateVersion7(), "  class 8 - MORNING shift ", 5, now));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.Equal("ux_classes_normalized_name", Assert.IsType<PostgresException>(ex.InnerException).ConstraintName);
    }
}
