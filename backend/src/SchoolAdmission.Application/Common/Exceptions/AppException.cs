namespace SchoolAdmission.Application.Common.Exceptions;

/// <summary>
/// Base for expected application errors. The API maps each subtype to an HTTP status and writes
/// <c>code</c>, <c>detail</c> and any <see cref="Extensions"/> into the problem+json body.
/// </summary>
public abstract class AppException(string code, string message, IReadOnlyDictionary<string, object?>? extensions = null)
    : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>Extra problem+json members, e.g. <c>existingHoldId</c>.</summary>
    public IReadOnlyDictionary<string, object?> Extensions { get; } = extensions ?? new Dictionary<string, object?>();
}

/// <summary>404 — unknown or malformed id.</summary>
public sealed class NotFoundException(string code, string message) : AppException(code, message);

/// <summary>403 — e.g. the hold belongs to another student.</summary>
public sealed class ForbiddenException(string code, string message) : AppException(code, message);

/// <summary>409 — business conflict (no seats, duplicate application, duplicate class name...).</summary>
public sealed class ConflictException(string code, string message, IReadOnlyDictionary<string, object?>? extensions = null)
    : AppException(code, message, extensions);

/// <summary>503 — the database is unreachable, the pool is exhausted, or a command timed out.</summary>
public sealed class ServiceUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
