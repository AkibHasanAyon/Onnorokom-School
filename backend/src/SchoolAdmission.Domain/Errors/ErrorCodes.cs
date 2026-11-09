namespace SchoolAdmission.Domain.Errors;

/// <summary>Stable machine-readable error codes returned in the problem+json <c>code</c> field (API.md §3).</summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string MalformedJson = "MALFORMED_JSON";
    public const string InvalidStudentId = "INVALID_STUDENT_ID";
    public const string HoldNotOwned = "HOLD_NOT_OWNED";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string ClassNameExists = "CLASS_NAME_EXISTS";
    public const string NoSeatsAvailable = "NO_SEATS_AVAILABLE";
    public const string ActiveHoldExists = "ACTIVE_HOLD_EXISTS";
    public const string AlreadyAdmitted = "ALREADY_ADMITTED";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string HoldAlreadyConfirmed = "HOLD_ALREADY_CONFIRMED";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string InternalError = "INTERNAL_ERROR";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
}
