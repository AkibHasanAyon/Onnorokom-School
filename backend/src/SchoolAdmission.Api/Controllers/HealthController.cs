using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Abstractions.Persistence;

namespace SchoolAdmission.Api.Controllers;

/// <summary>Liveness + database probe plus server time (extra endpoint, not part of the brief's contract).</summary>
[ApiController]
[Route("health")]
public sealed class HealthController(IDatabaseHealthCheck database, TimeProvider clock) : ControllerBase
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        bool healthy;
        using (var probe = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            probe.CancelAfter(ProbeTimeout);
            try
            {
                healthy = await database.CanQueryAsync(probe.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                healthy = false; // probe timed out
            }
        }

        // UTC, whole seconds, trailing Z — same timestamp format as the rest of the API.
        var serverTime = clock.GetUtcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        return healthy
            ? Ok(new HealthResponse("ok", serverTime))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("unavailable", serverTime));
    }
}
