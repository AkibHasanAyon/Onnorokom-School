using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SchoolAdmission.Domain.Errors;

namespace SchoolAdmission.Api.ErrorHandling;

/// <summary>
/// Every error body is RFC 9457 problem+json with a stable <c>code</c> and a <c>traceId</c> (API.md §3).
/// </summary>
public static class ProblemDetailsSetup
{
    private const string MalformedJsonMessage = "The value is not valid JSON or has the wrong type.";

    /// <summary>Applied to every ProblemDetails: exception handler, status-code pages, MVC client errors (415...).</summary>
    public static void Customize(ProblemDetailsOptions options) => options.CustomizeProblemDetails = context =>
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;
        problem.Status = status;
        problem.Type = $"https://httpstatuses.io/{status}";

        if (!problem.Extensions.ContainsKey("code"))
            problem.Extensions["code"] = DefaultCode(status);
        problem.Detail ??= DefaultDetail(status);
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };

    /// <summary>
    /// [ApiController] model-binding failures. A JSON syntax/type error (key "$..." or an empty body) is
    /// 400 MALFORMED_JSON; anything else is 400 VALIDATION_FAILED.
    /// </summary>
    public static IActionResult FromInvalidModelState(ActionContext context)
    {
        var entries = context.ModelState.Where(e => e.Value is { Errors.Count: > 0 }).ToList();
        var malformed = entries.Any(e => e.Key.Length == 0 || e.Key.StartsWith('$'));

        var errors = malformed
            ? entries.Where(e => e.Key.Length == 0 || e.Key.StartsWith('$'))
                .GroupBy(e => JsonPathToField(e.Key))
                .ToDictionary(g => g.Key, _ => new[] { MalformedJsonMessage })
            : entries.ToDictionary(
                e => ToCamelCase(e.Key),
                e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(context.HttpContext, StatusCodes.Status400BadRequest,
            title: malformed ? "Malformed JSON" : "Validation failed",
            detail: malformed ? "The request body is not valid JSON or a field has the wrong type." : "One or more fields are invalid.");
        problem.Extensions["code"] = malformed ? ErrorCodes.MalformedJson : ErrorCodes.ValidationFailed;
        problem.Extensions["errors"] = errors;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    }

    internal static string DefaultCode(int status) => status switch
    {
        400 => ErrorCodes.ValidationFailed,
        404 => ErrorCodes.NotFound,
        405 => ErrorCodes.MethodNotAllowed,
        413 => ErrorCodes.PayloadTooLarge,
        415 => ErrorCodes.UnsupportedMediaType,
        503 => ErrorCodes.ServiceUnavailable,
        >= 500 => ErrorCodes.InternalError,
        _ => $"HTTP_{status}",
    };

    private static string? DefaultDetail(int status) => status switch
    {
        404 => "No resource exists at this URL.",
        405 => "This URL does not support this HTTP method.",
        413 => "The request body is larger than 64 KB.",
        415 => "Send the request body as application/json.",
        _ => null,
    };

    // "$.totalSeats" → "totalSeats"; "$" or "" → "body"
    private static string JsonPathToField(string key)
    {
        var field = key.TrimStart('$').TrimStart('.');
        return field.Length == 0 ? "body" : ToCamelCase(field);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
