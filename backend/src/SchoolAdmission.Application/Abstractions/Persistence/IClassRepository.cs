using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Abstractions.Persistence;

/// <summary>Write-side access to classes (commands only).</summary>
public interface IClassRepository
{
    void Add(SchoolClass schoolClass);

    /// <summary>
    /// <c>SELECT ... FOR UPDATE</c> on the class row. Must be called inside a transaction; every other
    /// caller locking the same class (in any process) waits until this transaction ends.
    /// </summary>
    Task<SchoolClass?> GetByIdForUpdateAsync(Guid classId, CancellationToken ct);
}
