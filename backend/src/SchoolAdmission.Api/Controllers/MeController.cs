using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Holds.GetMyHolds;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me")]
public sealed class MeController(ISender sender) : ControllerBase
{
    /// <summary>GET /me/holds → 200, this student's applications newest first ([] if none); bad header → 400.</summary>
    [HttpGet("holds")]
    public async Task<IActionResult> MyHolds(
        [FromHeader(Name = StudentIdHeader.Name)] StudentIdHeader? studentId, CancellationToken ct) =>
        Ok(await sender.Send(new GetMyHoldsQuery(studentId.Raw()), ct));
}
