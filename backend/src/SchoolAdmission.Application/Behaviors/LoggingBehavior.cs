using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using SchoolAdmission.Application.Abstractions.Messaging;
using SchoolAdmission.Application.Common.Exceptions;
using SchoolAdmission.Application.Common.Logging;

namespace SchoolAdmission.Application.Behaviors;

/// <summary>
/// Structured log line per use case: name, student (masked to the last 4 digits), outcome and duration.
/// Never logs request bodies or raw student ids.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var student = request is IStudentRequest s ? StudentIdMask.Mask(s.StudentId) : "-";
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await next(cancellationToken);
            logger.LogDebug("{Request} for student {Student} succeeded in {ElapsedMs:0} ms",
                name, student, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (AppException ex)
        {
            // Expected business outcome (4xx), not a server error.
            logger.LogInformation("{Request} for student {Student} rejected with {Code} in {ElapsedMs:0} ms",
                name, student, ex.Code, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("{Request} for student {Student} failed with {Exception} in {ElapsedMs:0} ms",
                name, student, ex.GetType().Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
