using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.ReadModels;

namespace SchoolAdmission.Application.Classes.GetClasses;

/// <summary>GET /classes — every class with live seat accounting, ordered by creation time.</summary>
public sealed record GetClassesQuery : IQuery<IReadOnlyList<ClassSeatSummaryDto>>;

public sealed class GetClassesQueryHandler(IClassReadStore store, TimeProvider clock)
    : IRequestHandler<GetClassesQuery, IReadOnlyList<ClassSeatSummaryDto>>
{
    public Task<IReadOnlyList<ClassSeatSummaryDto>> Handle(GetClassesQuery query, CancellationToken cancellationToken) =>
        store.GetAllAsync(clock.GetUtcNow(), cancellationToken);
}
