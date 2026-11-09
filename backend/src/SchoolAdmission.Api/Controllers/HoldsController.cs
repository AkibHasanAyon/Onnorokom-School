using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Application.Holds.CreateHold;
using SchoolAdmission.Application.Holds.GetHoldById;

namespace SchoolAdmission.Api.Controllers;

/// <summary>Hold endpoints. Route ids and the X-Student-Id header are passed through raw; handlers decide 400/404.</summary>
/// <remarks>Route ids are <c>string?</c>: a whitespace segment binds as null and must give 404, not 400.</remarks>
[ApiController]
[Route("holds")]
public sealed class HoldsController(ISender sender) : ControllerBase
{
    private const string StudentIdHeader = "X-Student-Id";

    /// <summary>POST /classes/{classId}/holds → 201 + Location: /holds/{holdId}; 409 when no seat is left.</summary>
    /// <remarks>No body is read: any body (any content type) is ignored.</remarks>
    [HttpPost("/classes/{classId}/holds")]
    public async Task<IActionResult> Create(string? classId,
        [FromHeader(Name = StudentIdHeader)] string? studentId, CancellationToken ct)
    {
        var dto = await sender.Send(new CreateHoldCommand(classId ?? string.Empty, studentId), ct);
        return Created($"/holds/{dto.HoldId}", dto);
    }

    /// <summary>GET /holds/{holdId} → 200 with the effective status; no header needed (ignored if sent).</summary>
    [HttpGet("{holdId}")]
    public async Task<IActionResult> Get(string? holdId, CancellationToken ct) =>
        Ok(await sender.Send(new GetHoldByIdQuery(holdId ?? string.Empty), ct));
}
