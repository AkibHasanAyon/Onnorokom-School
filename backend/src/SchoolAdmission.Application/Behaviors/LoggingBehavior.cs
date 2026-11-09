using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using SchoolAdmission.Application.Common.Exceptions;

namespace SchoolAdmission.Application.Behaviors;

/// <summary>
/// Logs each use case's name, outcome and duration. Never logs request bodies or raw student ids.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await next(cancellationToken);
            logger.LogDebug("{Request} succeeded in {ElapsedMs:0} ms", name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return response;
        }
        catch (AppException ex)
        {
            // Expected business outcome (4xx), not a server error.
            logger.LogInformation("{Request} rejected with {Code} in {ElapsedMs:0} ms",
                name, ex.Code, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("{Request} failed with {Exception} in {ElapsedMs:0} ms",
                name, ex.GetType().Name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
