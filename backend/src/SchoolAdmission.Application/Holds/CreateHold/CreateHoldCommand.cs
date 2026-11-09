using FluentValidation;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Common.Validation;

namespace SchoolAdmission.Application.Holds.CreateHold;

/// <summary>
/// POST /classes/{classId}/holds — apply for a seat. <see cref="ClassId"/> is the raw route value
/// (parsed in the handler so a bad header is 400 before an unknown class is 404).
/// </summary>
public sealed record CreateHoldCommand(string ClassId, string? StudentId) : ICommand<HoldCreatedDto>, IStudentRequest;

public sealed class CreateHoldCommandValidator : AbstractValidator<CreateHoldCommand>
{
    public CreateHoldCommandValidator() => RuleFor(x => x.StudentId).ValidStudentId();
}

/// <summary>
/// 201 body. Contract fields: holdId, classId, studentId, status, expiresAt.
/// Extras: className, createdAt, serverTime (lets the UI correct client clock skew).
/// </summary>
public sealed record HoldCreatedDto(
    Guid HoldId,
    Guid ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt,
    string ClassName,
    DateTimeOffset CreatedAt,
    DateTimeOffset ServerTime);
