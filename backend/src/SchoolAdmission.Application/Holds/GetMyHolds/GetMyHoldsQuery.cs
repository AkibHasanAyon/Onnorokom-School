using FluentValidation;
using MediatR;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Abstractions.ReadModels;
using SchoolAdmission.Application.Common.Validation;

namespace SchoolAdmission.Application.Holds.GetMyHolds;

/// <summary>GET /me/holds — all of this student's applications, newest first, every status (effective).</summary>
public sealed record GetMyHoldsQuery(string? StudentId) : IQuery<IReadOnlyList<MyHoldDto>>;

public sealed class GetMyHoldsQueryValidator : AbstractValidator<GetMyHoldsQuery>
{
    public GetMyHoldsQueryValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}

/// <summary>Contract fields: holdId, classId, className, status, expiresAt. Extra: createdAt.</summary>
public sealed record MyHoldDto(
    Guid HoldId,
    Guid ClassId,
    string ClassName,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed class GetMyHoldsQueryHandler(IHoldReadStore store, TimeProvider clock)
    : IRequestHandler<GetMyHoldsQuery, IReadOnlyList<MyHoldDto>>
{
    public Task<IReadOnlyList<MyHoldDto>> Handle(GetMyHoldsQuery query, CancellationToken cancellationToken) =>
        store.GetByStudentAsync(query.StudentId!.Trim(), clock.GetUtcNow(), cancellationToken);
}
