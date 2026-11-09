using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;

namespace SchoolAdmission.Api.Controllers;

/// <summary>Liveness probe plus server time (extra endpoint, not part of the brief's contract).</summary>
[ApiController]
[Route("health")]
public sealed class HealthController(TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        // UTC, whole seconds, trailing Z — same timestamp format as the rest of the API.
        var serverTime = clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return Ok(new HealthResponse("ok", serverTime));
    }
}
