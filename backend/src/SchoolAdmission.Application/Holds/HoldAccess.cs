using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Domain.Entities;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Application.Holds;

/// <summary>Shared steps 2–3 of confirm and cancel: find + lock the hold (404), then check ownership (403).</summary>
internal static class HoldAccess
{
    public static async Task<Hold> LockOwnedAsync(IHoldRepository holds, string rawHoldId, string studentId, CancellationToken ct)
    {
        if (!RouteId.TryParse(rawHoldId, out var holdId))
            throw NotFound();

        var hold = await holds.GetByIdForUpdateAsync(holdId, ct) ?? throw NotFound();

        if (hold.StudentId != studentId.Trim())
            throw new ForbiddenException(ErrorCodes.HoldNotOwned, "This application belongs to a different student.");

        return hold;
    }

    private static NotFoundException NotFound() => new(ErrorCodes.HoldNotFound, "This application does not exist.");
}
