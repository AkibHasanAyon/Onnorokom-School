using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure.Repositories;

internal sealed class HoldRepository(AdmissionDbContext db) : IHoldRepository
{
    public void Add(Hold hold) => db.Holds.Add(hold);

    public async Task<Hold?> GetByIdForUpdateAsync(Guid holdId, CancellationToken ct)
    {
        var rows = await db.Holds
            .FromSqlInterpolated($"SELECT * FROM holds WHERE id = {holdId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public Task<int> ExpireStaleHoldForStudentAsync(Guid classId, string studentId, DateTimeOffset now, CancellationToken ct) =>
        db.Holds
            .Where(h => h.ClassId == classId && h.StudentId == studentId
                     && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.Status, HoldStatus.Expired)
                .SetProperty(h => h.ExpiredAt, now)
                .SetProperty(h => h.UpdatedAt, now), ct);

    public Task<Hold?> FindActiveOrConfirmedAsync(Guid classId, string studentId, DateTimeOffset now, CancellationToken ct) =>
        db.Holds.AsNoTracking()
            .Where(h => h.ClassId == classId && h.StudentId == studentId
                     && (h.Status == HoldStatus.Confirmed || (h.Status == HoldStatus.Held && h.ExpiresAt > now)))
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<SeatCounts> GetSeatCountsAsync(Guid classId, DateTimeOffset now, CancellationToken ct)
    {
        var held = await db.Holds.CountAsync(h => h.ClassId == classId
            && h.Status == HoldStatus.Held && h.ExpiresAt > now, ct);
        var confirmed = await db.Holds.CountAsync(h => h.ClassId == classId
            && h.Status == HoldStatus.Confirmed, ct);
        return new SeatCounts(held, confirmed);
    }

    public Task<int> ExpireStaleBatchAsync(DateTimeOffset now, int batchSize, CancellationToken ct) =>
        // SKIP LOCKED: never waits on a row another transaction (or another instance's sweeper) holds.
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE holds
               SET status = 'EXPIRED', expired_at = {now}, updated_at = {now}
             WHERE id IN (
                   SELECT id FROM holds
                    WHERE status = 'HELD' AND expires_at <= {now}
                    ORDER BY expires_at
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED)
            """, ct);
}
