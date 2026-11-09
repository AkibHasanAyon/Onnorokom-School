using SchoolAdmission.Application.Common.Logging;

namespace SchoolAdmission.Api.Contracts;

/// <summary>
/// The raw X-Student-Id header value. A wrapper instead of a plain string because MVC logs action arguments
/// (ControllerActionInvoker, Trace level) with ToString(): this type always prints masked
/// (<c>*************7890</c>), so the full birth registration number can't reach logs even with Trace enabled.
/// MVC binds it via the static <see cref="TryParse"/> (any value is accepted; validation happens in the
/// Application layer so the error is the contract's 400 INVALID_STUDENT_ID).
/// </summary>
public sealed class StudentIdHeader
{
    public const string Name = "X-Student-Id";

    private StudentIdHeader(string? value) => Value = value;

    public string? Value { get; }

    public static bool TryParse(string? value, out StudentIdHeader result)
    {
        result = new StudentIdHeader(value);
        return true;
    }

    public override string ToString() => StudentIdMask.Mask(Value);
}

public static class StudentIdHeaderExtensions
{
    /// <summary>The raw header value, or null when the header was missing.</summary>
    public static string? Raw(this StudentIdHeader? header) => header?.Value;
}
