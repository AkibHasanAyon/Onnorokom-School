using SchoolAdmission.Domain;

namespace SchoolAdmission.Application.Common.Logging;

/// <summary>
/// A birth registration number is personal data: logs only ever show the last 4 digits
/// (<c>20152691234567890</c> → <c>*************7890</c>). Anything that isn't a valid id is not echoed at all.
/// </summary>
public static class StudentIdMask
{
    public static string Mask(string? studentId)
    {
        if (string.IsNullOrWhiteSpace(studentId)) return "(none)";
        var trimmed = studentId.Trim();
        return StudentIdentifier.IsValid(trimmed)
            ? new string('*', StudentIdentifier.Length - 4) + trimmed[^4..]
            : "(invalid)";
    }
}
