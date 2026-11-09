using SchoolAdmission.Application.Holds.GetHoldById;
using SchoolAdmission.Application.Holds.GetMyHolds;

namespace SchoolAdmission.Application.Abstractions.ReadModels;

/// <summary>Read side for holds: no-tracking projections with the effective status at <c>now</c>.</summary>
public interface IHoldReadStore
{
    Task<HoldDto?> GetByIdAsync(Guid holdId, DateTimeOffset now, CancellationToken ct);

    /// <summary>All holds of the student (any status), newest first.</summary>
    Task<IReadOnlyList<MyHoldDto>> GetByStudentAsync(string studentId, DateTimeOffset now, CancellationToken ct);
}
