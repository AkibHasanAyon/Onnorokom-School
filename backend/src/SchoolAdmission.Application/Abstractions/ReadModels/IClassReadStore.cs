using SchoolAdmission.Application.Classes;

namespace SchoolAdmission.Application.Abstractions.ReadModels;

/// <summary>Read side for classes: no-tracking projections, seat counts derived at <paramref name="now"/>.</summary>
public interface IClassReadStore
{
    /// <summary>All classes, oldest first.</summary>
    Task<IReadOnlyList<ClassSeatSummaryDto>> GetAllAsync(DateTimeOffset now, CancellationToken ct);

    Task<ClassSeatSummaryDto?> GetByIdAsync(Guid classId, DateTimeOffset now, CancellationToken ct);
}
