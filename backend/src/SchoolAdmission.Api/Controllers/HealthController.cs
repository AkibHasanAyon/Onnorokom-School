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

        // serverTime is written by UtcDateTimeOffsetConverter (UTC, whole seconds, trailing Z).
        var now = clock.GetUtcNow();
        return healthy
            ? Ok(new HealthResponse("ok", now))
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse("unavailable", now));
    }
}
