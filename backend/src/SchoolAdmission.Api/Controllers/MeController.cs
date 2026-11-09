using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.Holds.GetMyHolds;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me")]
public sealed class MeController(ISender sender) : ControllerBase
{
    /// <summary>GET /me/holds → 200, this student's applications newest first ([] if none); bad header → 400.</summary>
    [HttpGet("holds")]
    public async Task<IActionResult> MyHolds([FromHeader(Name = "X-Student-Id")] string? studentId, CancellationToken ct) =>
        Ok(await sender.Send(new GetMyHoldsQuery(studentId), ct));
}
