using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Abstractions.ReadModels;
using SchoolAdmission.Application.Holds;
using SchoolAdmission.Application.Holds.GetHoldById;
using SchoolAdmission.Application.Holds.GetMyHolds;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure.ReadStores;

internal sealed class HoldReadStore(AdmissionDbContext db) : IHoldReadStore
{
    public async Task<HoldDto?> GetByIdAsync(Guid holdId, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.Holds.AsNoTracking()
            .Where(h => h.Id == holdId)
            .Join(db.Classes, h => h.ClassId, c => c.Id, (h, c) => new
            {
                h.Id, h.ClassId, h.Status, h.ExpiresAt, ClassName = c.Name, h.CreatedAt, h.ConfirmedAt, h.CancelledAt,
            })
            .SingleOrDefaultAsync(ct);

        return row is null
            ? null
            : new HoldDto(row.Id, row.ClassId, HoldStatusText.Effective(row.Status, row.ExpiresAt, now), row.ExpiresAt,
                row.ClassName, row.CreatedAt, row.ConfirmedAt, row.CancelledAt, now);
    }

    public async Task<IReadOnlyList<MyHoldDto>> GetByStudentAsync(string studentId, DateTimeOffset now, CancellationToken ct)
    {
        // Uses ix_holds_student_created (student_id, created_at DESC). Ties within a second fall back to the
        // UUIDv7 id, which PostgreSQL orders by its embedded millisecond timestamp.
        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .Join(db.Classes, h => h.ClassId, c => c.Id, (h, c) => new
            {
                h.Id, h.ClassId, ClassName = c.Name, h.Status, h.ExpiresAt, h.CreatedAt,
            })
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .ToListAsync(ct);

        return rows
            .Select(r => new MyHoldDto(r.Id, r.ClassId, r.ClassName,
                HoldStatusText.Effective(r.Status, r.ExpiresAt, now), r.ExpiresAt, r.CreatedAt))
            .ToList();
    }
}
