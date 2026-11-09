using FluentValidation;
using SchoolAdmission.Domain;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Application.Common.Validation;

public static class StudentIdRules
{
    public const string Message = "X-Student-Id must be the 17-digit birth registration number.";

    /// <summary>X-Student-Id: required; after trimming, exactly 17 ASCII digits. Fails with INVALID_STUDENT_ID.</summary>
    public static IRuleBuilderOptions<T, string?> ValidStudentId<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => v is not null && StudentIdentifier.IsValid(v.Trim()))
            .WithErrorCode(ErrorCodes.InvalidStudentId)
            .WithMessage(Message)
            .OverridePropertyName("X-Student-Id");
}
