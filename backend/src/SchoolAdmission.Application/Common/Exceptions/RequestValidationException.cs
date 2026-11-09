using FluentValidation.Results;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Application.Common.Exceptions;

/// <summary>
/// 400 — input failed validation. <see cref="AppException.Code"/> is <c>INVALID_STUDENT_ID</c> when the
/// first failure is about the X-Student-Id header, otherwise <c>VALIDATION_FAILED</c>.
/// </summary>
public sealed class RequestValidationException : AppException
{
    public RequestValidationException(IReadOnlyCollection<ValidationFailure> failures)
        : base(CodeFor(failures), DetailFor(failures))
    {
        Errors = failures
            .GroupBy(f => ToCamelCase(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());
    }

    /// <summary>Field (camelCase) → messages; serialized as the problem+json <c>errors</c> member.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    private static string CodeFor(IReadOnlyCollection<ValidationFailure> failures) =>
        failures.FirstOrDefault()?.ErrorCode == ErrorCodes.InvalidStudentId
            ? ErrorCodes.InvalidStudentId
            : ErrorCodes.ValidationFailed;

    private static string DetailFor(IReadOnlyCollection<ValidationFailure> failures) =>
        CodeFor(failures) == ErrorCodes.InvalidStudentId
            ? failures.First().ErrorMessage
            : "One or more fields are invalid.";

    // Body fields are camelCase like the JSON ("TotalSeats" → "totalSeats"); header names ("X-Student-Id") keep their spelling.
    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) || name.Contains('-')
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}
