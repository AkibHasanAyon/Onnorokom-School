namespace SchoolAdmission.Domain.Errors;

/// <summary>An illegal state transition (e.g. paying for an expired hold). Mapped to HTTP 409.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
