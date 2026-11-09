using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Infrastructure.Persistence;

namespace SchoolAdmission.Infrastructure.Repositories;

internal sealed class ClassRepository(AdmissionDbContext db) : IClassRepository
{
    public void Add(SchoolClass schoolClass) => db.Classes.Add(schoolClass);

    public async Task<SchoolClass?> GetByIdForUpdateAsync(Guid classId, CancellationToken ct)
    {
        // No LINQ operators on FromSql: EF must send this SQL as-is, or FOR UPDATE could be lost in a subquery.
        var rows = await db.Classes
            .FromSqlInterpolated($"SELECT * FROM classes WHERE id = {classId} FOR UPDATE")
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }
}
