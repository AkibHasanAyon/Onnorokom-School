using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure.Persistence;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Persistence;

/// <summary>
/// The sweep batch is global (all classes), so it gets its own database: holds seeded by other tests
/// would otherwise change the counts.
/// </summary>
public sealed class SweepBatchTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly DateTimeOffset Now = new(2030, 1, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExpireStaleBatch_ExpiresOnlyStaleHeldRows_InBatches()
    {
        Hold[] holds;
        await using (var seed = api.Factory.Services.CreateAsyncScope())
        {
            var cls = SchoolClass.Create(Guid.CreateVersion7(), "Sweep Class", 10, Now);
            seed.ServiceProvider.GetRequiredService<IClassRepository>().Add(cls);
            Hold New(int student, int minutesAgo) =>
                Hold.Create(Guid.CreateVersion7(), cls.Id, TestData.StudentId(student), Now.AddMinutes(-minutesAgo), TimeSpan.FromMinutes(10));
            holds = [New(1, 30), New(2, 20), New(3, 10) /* expires exactly now → stale */, New(4, 0) /* active */, New(5, 30)];
            holds[4].Confirm(Now.AddMinutes(-29));
            var repo = seed.ServiceProvider.GetRequiredService<IHoldRepository>();
            foreach (var h in holds) repo.Add(h);
            await seed.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        }

        await using var scope = api.Factory.Services.CreateAsyncScope();
        var holdsRepo = scope.ServiceProvider.GetRequiredService<IHoldRepository>();

        Assert.Equal(2, await holdsRepo.ExpireStaleBatchAsync(Now, batchSize: 2, default));
        Assert.Equal(1, await holdsRepo.ExpireStaleBatchAsync(Now, batchSize: 2, default));
        Assert.Equal(0, await holdsRepo.ExpireStaleBatchAsync(Now, batchSize: 2, default));

        var statuses = await Task.WhenAll(holds.Select(h =>
            api.Database.ScalarAsync<string>($"SELECT status FROM holds WHERE id = '{h.Id}'")));
        Assert.Equal("EXPIRED,EXPIRED,EXPIRED,HELD,CONFIRMED", string.Join(',', statuses));

        var ctx = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var expiredAt = await ctx.Holds.AsNoTracking().Where(h => h.Id == holds[0].Id).Select(h => h.ExpiredAt).SingleAsync();
        Assert.Equal(Now, expiredAt);
        await Db.AssertInvariantAsync(api.Database.ConnectionString, Now);
    }
}
