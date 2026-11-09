using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.IntegrationTests.Api;

public sealed record ProbeBody(string? Name, int? TotalSeats);

/// <summary>
/// Test-only endpoints (added to the host by <see cref="ApiConventionsTests"/>) that exercise the shared error
/// handling and JSON conventions before any real endpoint with a body exists.
/// </summary>
[ApiController]
[Route("__probe")]
public sealed class ProbeController : ControllerBase
{
    public static readonly Guid ExistingHoldId = Guid.Parse("0193a6f3-0a11-7c22-8e33-445566778899");

    [HttpGet("throw/{kind}")]
    public IActionResult Throw(string kind) => throw (kind switch
    {
        "not-found" => new NotFoundException(ErrorCodes.ClassNotFound, "This class does not exist."),
        "forbidden" => new ForbiddenException(ErrorCodes.HoldNotOwned, "This application belongs to a different student."),
        "conflict" => new ConflictException(ErrorCodes.ActiveHoldExists, "Already applied.",
            new Dictionary<string, object?> { ["existingHoldId"] = ExistingHoldId }),
        "domain" => new DomainException(ErrorCodes.HoldExpired, "This application has expired."),
        "student" => new RequestValidationException(
            [new FluentValidation.Results.ValidationFailure("X-Student-Id", "X-Student-Id must be the 17-digit birth registration number.") { ErrorCode = ErrorCodes.InvalidStudentId }]),
        "db-down" => new NpgsqlException("Failed to connect to 10.0.0.1:5432"),
        "unavailable" => new ServiceUnavailableException("pool exhausted"),
        "too-large" => new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge),
        _ => new InvalidOperationException("SECRET internal detail: connection string Password=hunter2"),
    });

    [HttpPost("body")]
    public IActionResult Body(ProbeBody body) => Ok(body);

    [HttpGet("time")]
    public IActionResult Time() => Ok(new
    {
        at = new DateTimeOffset(2026, 11, 1, 10, 10, 0, 456, TimeSpan.FromHours(6)),
        none = (DateTimeOffset?)null,
    });
}
