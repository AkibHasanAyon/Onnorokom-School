using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Abstractions.ReadModels;
using SchoolAdmission.Application.Classes;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Enums;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure.ReadStores;

internal sealed class ClassReadStore(AdmissionDbContext db) : IClassReadStore
{
    public async Task<IReadOnlyList<ClassSeatSummaryDto>> GetAllAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Order the entities *before* projecting: EF can't translate ORDER BY on a constructor-projected member.
        var classes = db.Classes.AsNoTracking().OrderBy(c => c.CreatedAt).ThenBy(c => c.Id);
        var rows = await Project(classes, now).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<ClassSeatSummaryDto?> GetByIdAsync(Guid classId, DateTimeOffset now, CancellationToken ct)
    {
        var row = await Project(db.Classes.AsNoTracking().Where(c => c.Id == classId), now).SingleOrDefaultAsync(ct);
        return row is null ? null : ToDto(row);
    }

    /// <summary>One SQL statement: counts are correlated subqueries over the (class_id, status, expires_at) index.</summary>
    private IQueryable<Row> Project(IQueryable<SchoolClass> classes, DateTimeOffset now) =>
        classes.Select(c => new Row(
            c.Id, c.Name, c.TotalSeats,
            db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.Held && h.ExpiresAt > now),
            db.Holds.Count(h => h.ClassId == c.Id && h.Status == HoldStatus.Confirmed)));

    private static ClassSeatSummaryDto ToDto(Row r) => ClassSeatSummaryDto.From(r.Id, r.Name, r.TotalSeats, r.Held, r.Confirmed);

    private sealed record Row(Guid Id, string Name, int TotalSeats, int Held, int Confirmed);
}
