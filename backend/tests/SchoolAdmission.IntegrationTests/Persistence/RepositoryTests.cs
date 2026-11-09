using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Domain.Errors;
using SchoolAdmission.Infrastructure.Persistence;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Persistence;

/// <summary>Unit of work and repositories against real PostgreSQL (row locks, partial index, raw SQL).</summary>
public sealed class RepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private readonly PostgresDatabase _db = new();
    private AdmissionApiFactory _factory = default!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _factory = new AdmissionApiFactory(_db.ConnectionString);
        _factory.CreateClient().Dispose(); // start host → migrate
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    private static string Student(int i) => $"2030{i:D13}";

    private async Task<SchoolClass> SeedClassAsync(string name, int seats = 10)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var cls = SchoolClass.Create(Guid.CreateVersion7(), name, seats, Now);
        scope.ServiceProvider.GetRequiredService<IClassRepository>().Add(cls);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        return cls;
    }

    /// <summary>Adds holds; each action may confirm/cancel/expire the hold before saving.</summary>
    private async Task<Hold[]> SeedHoldsAsync(params (Guid classId, string student, DateTimeOffset createdAt, Action<Hold>? change)[] specs)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IHoldRepository>();
        var holds = specs.Select(s =>
        {
            var hold = Hold.Create(Guid.CreateVersion7(), s.classId, s.student, s.createdAt, TenMinutes);
            s.change?.Invoke(hold);
            repo.Add(hold);
            return hold;
        }).ToArray();
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        return holds;
    }

    private Task<string?> StatusOfAsync(Guid holdId) => _db.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{holdId}'");

    /// <summary>Tries to lock a row from an independent connection; true if it had to wait (lock held elsewhere).</summary>
    private async Task<bool> RowIsLockedAsync(string table, Guid id)
    {
        await using var connection = new NpgsqlConnection(_db.ConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        await using (var timeout = new NpgsqlCommand("SET LOCAL lock_timeout = '300ms'", connection, tx))
            await timeout.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand($"SELECT 1 FROM {table} WHERE id = @id FOR UPDATE", connection, tx);
        command.Parameters.AddWithValue("id", id);
        try
        {
            await command.ExecuteScalarAsync();
            return false;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            return true;
        }
    }

    // ---- Unit of work -------------------------------------------------------------

    [Fact]
    public async Task SaveChanges_DuplicateClassName_IsTranslatedTo409ClassNameExists()
    {
        await SeedClassAsync("Class 6 - Morning Shift");

        var ex = await Assert.ThrowsAsync<ConflictException>(() => SeedClassAsync("  CLASS 6 - morning shift "));
        Assert.Equal(ErrorCodes.ClassNameExists, ex.Code);
    }

    [Fact]
    public async Task SaveChanges_SecondActiveHoldForSameStudentAndClass_IsTranslatedTo409ActiveHoldExists()
    {
        var cls = await SeedClassAsync("Dup Hold Class");
        await SeedHoldsAsync((cls.Id, Student(1), Now, null));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => SeedHoldsAsync((cls.Id, Student(1), Now, null)));
        Assert.Equal(ErrorCodes.ActiveHoldExists, ex.Code);
    }

    [Fact]
    public async Task Transaction_DisposedWithoutCommit_RollsBack()
    {
        var id = Guid.CreateVersion7();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using var tx = await uow.BeginTransactionAsync(default);
            scope.ServiceProvider.GetRequiredService<IClassRepository>().Add(SchoolClass.Create(id, "Rolled Back", 5, Now));
            await uow.SaveChangesAsync(default);
            // no CommitAsync
        }

        Assert.Equal(0L, await _db.ScalarAsync<long>($"SELECT count(*) FROM classes WHERE id = '{id}'"));
    }

    [Fact]
    public async Task Transaction_Committed_Persists()
    {
        var id = Guid.CreateVersion7();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using var tx = await uow.BeginTransactionAsync(default);
            scope.ServiceProvider.GetRequiredService<IClassRepository>().Add(SchoolClass.Create(id, "Committed", 5, Now));
            await uow.SaveChangesAsync(default);
            await tx.CommitAsync(default);
        }

        Assert.Equal(1L, await _db.ScalarAsync<long>($"SELECT count(*) FROM classes WHERE id = '{id}'"));
    }

    // ---- Row locks ------------------------------------------------------------------

    [Fact]
    public async Task ClassGetByIdForUpdate_HoldsRowLockUntilTransactionEnds()
    {
        var cls = await SeedClassAsync("Lock Class");
        Assert.False(await RowIsLockedAsync("classes", cls.Id));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using var tx = await uow.BeginTransactionAsync(default);

            var locked = await scope.ServiceProvider.GetRequiredService<IClassRepository>().GetByIdForUpdateAsync(cls.Id, default);

            Assert.NotNull(locked);
            Assert.Equal("Lock Class", locked.Name);
            Assert.True(await RowIsLockedAsync("classes", cls.Id)); // a second applicant would wait here
            await tx.CommitAsync(default);
        }

        Assert.False(await RowIsLockedAsync("classes", cls.Id)); // released at COMMIT
    }

    [Fact]
    public async Task HoldGetByIdForUpdate_HoldsRowLock_AndReturnsTrackedEntity()
    {
        var cls = await SeedClassAsync("Hold Lock Class");
        var hold = (await SeedHoldsAsync((cls.Id, Student(1), Now, null)))[0];

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await using var tx = await uow.BeginTransactionAsync(default);
            var locked = await scope.ServiceProvider.GetRequiredService<IHoldRepository>().GetByIdForUpdateAsync(hold.Id, default);

            Assert.NotNull(locked);
            Assert.Equal(HoldStatus.Held, locked.Status);
            Assert.True(await RowIsLockedAsync("holds", hold.Id));

            locked.Confirm(Now.AddMinutes(1));
            await uow.SaveChangesAsync(default);
            await tx.CommitAsync(default);
        }

        Assert.Equal("CONFIRMED", await StatusOfAsync(hold.Id));
    }

    [Fact]
    public async Task GetByIdForUpdate_UnknownId_ReturnsNull()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IClassRepository>().GetByIdForUpdateAsync(Guid.CreateVersion7(), default));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IHoldRepository>().GetByIdForUpdateAsync(Guid.CreateVersion7(), default));
    }

    // ---- Counting and lookups ---------------------------------------------------------

    [Fact]
    public async Task GetSeatCounts_CountsOnlyActiveHeldAndConfirmed()
    {
        var cls = await SeedClassAsync("Count Class");
        var other = await SeedClassAsync("Other Class");
        await SeedHoldsAsync(
            (cls.Id, Student(1), Now, null),                                        // held, active
            (cls.Id, Student(2), Now.AddMinutes(-5), null),                         // held, active (expires +5 min)
            (cls.Id, Student(3), Now.AddMinutes(-10), null),                        // held, expires exactly now → not counted
            (cls.Id, Student(4), Now.AddMinutes(-20), null),                        // held, long expired → not counted
            (cls.Id, Student(5), Now.AddMinutes(-30), h => h.Confirm(Now.AddMinutes(-29))), // confirmed (counts forever)
            (cls.Id, Student(6), Now, h => h.Cancel(Now)),                          // cancelled
            (cls.Id, Student(7), Now.AddMinutes(-30), h => h.Expire(Now)),          // stored expired
            (other.Id, Student(1), Now, null));                                     // other class

        await using var scope = _factory.Services.CreateAsyncScope();
        var counts = await scope.ServiceProvider.GetRequiredService<IHoldRepository>().GetSeatCountsAsync(cls.Id, Now, default);

        Assert.Equal(new SeatCounts(Held: 2, Confirmed: 1), counts);
    }

    [Fact]
    public async Task FindActiveOrConfirmed_IgnoresExpiredAndCancelled()
    {
        var cls = await SeedClassAsync("Find Class");
        var holds = await SeedHoldsAsync(
            (cls.Id, Student(1), Now, null),                                        // active
            (cls.Id, Student(2), Now.AddMinutes(-20), null),                        // logically expired
            (cls.Id, Student(3), Now.AddMinutes(-20), h => h.Confirm(Now.AddMinutes(-19))),
            (cls.Id, Student(4), Now, h => h.Cancel(Now)));

        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IHoldRepository>();

        Assert.Equal(holds[0].Id, (await repo.FindActiveOrConfirmedAsync(cls.Id, Student(1), Now, default))?.Id);
        Assert.Null(await repo.FindActiveOrConfirmedAsync(cls.Id, Student(2), Now, default));
        Assert.Equal(holds[2].Id, (await repo.FindActiveOrConfirmedAsync(cls.Id, Student(3), Now, default))?.Id);
        Assert.Null(await repo.FindActiveOrConfirmedAsync(cls.Id, Student(4), Now, default));
        Assert.Null(await repo.FindActiveOrConfirmedAsync(cls.Id, Student(9), Now, default));
    }

    // ---- Expiry -----------------------------------------------------------------------

    [Fact]
    public async Task ExpireStaleHoldForStudent_TouchesOnlyThatStudentsStaleHoldInThatClass()
    {
        var cls = await SeedClassAsync("Stale Class");
        var other = await SeedClassAsync("Stale Other Class");
        var holds = await SeedHoldsAsync(
            (cls.Id, Student(1), Now.AddMinutes(-20), null),   // target: stale
            (cls.Id, Student(2), Now.AddMinutes(-20), null),   // other student, stale
            (other.Id, Student(1), Now.AddMinutes(-20), null), // same student, other class, stale
            (cls.Id, Student(3), Now, null));                  // active

        int changed;
        await using (var scope = _factory.Services.CreateAsyncScope())
            changed = await scope.ServiceProvider.GetRequiredService<IHoldRepository>()
                .ExpireStaleHoldForStudentAsync(cls.Id, Student(1), Now, default);

        Assert.Equal(1, changed);
        Assert.Equal("EXPIRED", await StatusOfAsync(holds[0].Id));
        Assert.Equal("HELD", await StatusOfAsync(holds[1].Id));
        Assert.Equal("HELD", await StatusOfAsync(holds[2].Id));
        Assert.Equal("HELD", await StatusOfAsync(holds[3].Id));

        // Now the same student can hold a seat in that class again (partial unique index no longer blocks).
        await SeedHoldsAsync((cls.Id, Student(1), Now, null));
    }

    [Fact]
    public async Task ExpireStaleBatch_ExpiresOnlyStaleHeldRows_InBatches()
    {
        var cls = await SeedClassAsync("Sweep Class");
        var holds = await SeedHoldsAsync(
            (cls.Id, Student(1), Now.AddMinutes(-30), null),
            (cls.Id, Student(2), Now.AddMinutes(-20), null),
            (cls.Id, Student(3), Now.AddMinutes(-10), null),                        // expires exactly now → stale
            (cls.Id, Student(4), Now, null),                                        // active
            (cls.Id, Student(5), Now.AddMinutes(-30), h => h.Confirm(Now.AddMinutes(-29))));

        await using var scope = _factory.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IHoldRepository>();

        Assert.Equal(2, await repo.ExpireStaleBatchAsync(Now, batchSize: 2, default));
        Assert.Equal(1, await repo.ExpireStaleBatchAsync(Now, batchSize: 2, default));
        Assert.Equal(0, await repo.ExpireStaleBatchAsync(Now, batchSize: 2, default));

        var statuses = await Task.WhenAll(holds.Select(h => StatusOfAsync(h.Id)));
        Assert.Equal("EXPIRED,EXPIRED,EXPIRED,HELD,CONFIRMED", string.Join(',', statuses));
        var ctx = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var expiredAt = await ctx.Holds.AsNoTracking().Where(h => h.Id == holds[0].Id).Select(h => h.ExpiredAt).SingleAsync();
        Assert.Equal(Now, expiredAt);
    }
}
