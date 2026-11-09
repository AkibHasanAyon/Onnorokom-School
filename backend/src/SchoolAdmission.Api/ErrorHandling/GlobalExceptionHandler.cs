using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Api.ErrorHandling;

/// <summary>
/// The single place exceptions become HTTP responses. Expected errors → 4xx with their code;
/// database outages → 503; anything else is a bug → 500 INTERNAL_ERROR with no internal details.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private static readonly Dictionary<string, string> Titles = new()
    {
        [ErrorCodes.InvalidStudentId] = "Invalid student ID",
        [ErrorCodes.ValidationFailed] = "Validation failed",
        [ErrorCodes.HoldNotOwned] = "Not your application",
        [ErrorCodes.ClassNotFound] = "Class not found",
        [ErrorCodes.HoldNotFound] = "Application not found",
        [ErrorCodes.ClassNameExists] = "Class name already exists",
        [ErrorCodes.NoSeatsAvailable] = "No seats available",
        [ErrorCodes.ActiveHoldExists] = "Application already exists",
        [ErrorCodes.AlreadyAdmitted] = "Already admitted",
        [ErrorCodes.HoldExpired] = "Application expired",
        [ErrorCodes.HoldCancelled] = "Application cancelled",
        [ErrorCodes.HoldAlreadyConfirmed] = "Admission already confirmed",
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499; // client went away; nobody reads the body
            return true;
        }

        var problem = Map(exception);
        LogException(exception, problem);

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Map(Exception exception)
    {
        switch (exception)
        {
            case RequestValidationException e:
            {
                var p = Problem(StatusCodes.Status400BadRequest, e.Code, e.Message);
                p.Extensions["errors"] = e.Errors;
                return p;
            }
            case NotFoundException e: return Problem(StatusCodes.Status404NotFound, e.Code, e.Message, e.Extensions);
            case ForbiddenException e: return Problem(StatusCodes.Status403Forbidden, e.Code, e.Message, e.Extensions);
            case ConflictException e: return Problem(StatusCodes.Status409Conflict, e.Code, e.Message, e.Extensions);
            case Domain.Errors.DomainException e: return Problem(StatusCodes.Status409Conflict, e.Code, e.Message);
            case BadHttpRequestException e: // e.g. Kestrel's 64 KB body limit → 413
                return Problem(e.StatusCode, ProblemDetailsSetup.DefaultCode(e.StatusCode), null, title: null);
            case var e when IsDatabaseUnavailable(e):
                return Problem(StatusCodes.Status503ServiceUnavailable, ErrorCodes.ServiceUnavailable,
                    "The service is temporarily unavailable. Please try again in a moment.", title: "Service unavailable");
            default:
                return Problem(StatusCodes.Status500InternalServerError, ErrorCodes.InternalError,
                    "An unexpected error occurred. Please try again.", title: "Internal server error");
        }
    }

    /// <summary>Connection refused, pool exhausted, timeouts, server shutting down, too many clients.</summary>
    private static bool IsDatabaseUnavailable(Exception e) => e switch
    {
        ServiceUnavailableException or TimeoutException => true,
        PostgresException pg => pg.IsTransient,           // e.g. 53300 too_many_connections, 57P01 admin_shutdown
        NpgsqlException => true,                          // connection-level failure (no server error code)
        DbUpdateException { InnerException: { } inner } => IsDatabaseUnavailable(inner),
        InvalidOperationException { InnerException: { } inner } => IsDatabaseUnavailable(inner),
        _ => false,
    };

    private static ProblemDetails Problem(int status, string code, string? detail,
        IReadOnlyDictionary<string, object?>? extensions = null, string? title = "")
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title == "" ? Titles.GetValueOrDefault(code) : title,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        if (extensions is not null)
            foreach (var (key, value) in extensions) problem.Extensions[key] = value;
        return problem;
    }

    private void LogException(Exception exception, ProblemDetails problem)
    {
        var code = problem.Extensions["code"];
        if (problem.Status >= 500)
            logger.LogError(exception, "Request failed with {Status} {Code}", problem.Status, code);
        else
            logger.LogDebug("Request rejected with {Status} {Code}", problem.Status, code);
    }
}
