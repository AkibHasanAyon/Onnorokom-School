using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.ReadModels;
using SchoolAdmission.Application.Common;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Application.Classes.GetClassById;

/// <summary>GET /classes/{classId}. The raw route value is parsed here: malformed or unknown → 404 CLASS_NOT_FOUND.</summary>
public sealed record GetClassByIdQuery(string ClassId) : IQuery<ClassSeatSummaryDto>;

public sealed class GetClassByIdQueryHandler(IClassReadStore store, TimeProvider clock)
    : IRequestHandler<GetClassByIdQuery, ClassSeatSummaryDto>
{
    public async Task<ClassSeatSummaryDto> Handle(GetClassByIdQuery query, CancellationToken cancellationToken)
    {
        if (!RouteId.TryParse(query.ClassId, out var classId))
            throw NotFound();

        return await store.GetByIdAsync(classId, clock.GetUtcNow(), cancellationToken) ?? throw NotFound();
    }

    private static NotFoundException NotFound() => new(ErrorCodes.ClassNotFound, "This class does not exist.");
}
