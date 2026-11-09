using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(AdmissionDbContext db) : IDatabaseHealthCheck
{
    public async Task<bool> CanQueryAsync(CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return true;
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            return false;
        }
    }
}
