using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.ReadModels;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Application.Holds.GetHoldById;

/// <summary>GET /holds/{holdId}. No header needed (the brief lists none); malformed or unknown id → 404.</summary>
public sealed record GetHoldByIdQuery(string HoldId) : IQuery<HoldDto>;

/// <summary>
/// Contract fields: holdId, classId, status (effective), expiresAt.
/// Extras: className, createdAt, confirmedAt, cancelledAt, serverTime.
/// </summary>
public sealed record HoldDto(
    Guid HoldId,
    Guid ClassId,
    string Status,
    DateTimeOffset ExpiresAt,
    string ClassName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset ServerTime);

public sealed class GetHoldByIdQueryHandler(IHoldReadStore store, TimeProvider clock)
    : IRequestHandler<GetHoldByIdQuery, HoldDto>
{
    public async Task<HoldDto> Handle(GetHoldByIdQuery query, CancellationToken cancellationToken)
    {
        if (!RouteId.TryParse(query.HoldId, out var holdId)) throw NotFound();
        return await store.GetByIdAsync(holdId, clock.GetUtcNow(), cancellationToken) ?? throw NotFound();
    }

    private static NotFoundException NotFound() => new(ErrorCodes.HoldNotFound, "This application does not exist.");
}
