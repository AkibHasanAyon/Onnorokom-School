using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Classes.CreateClass;
using SchoolAdmission.Application.Classes.GetClassById;
using SchoolAdmission.Application.Classes.GetClasses;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public sealed class ClassesController(ISender sender) : ControllerBase
{
    /// <summary>POST /classes → 201 + Location: /classes/{id}.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateClassRequest request, CancellationToken ct)
    {
        var dto = await sender.Send(new CreateClassCommand(request.Name, request.TotalSeats), ct);
        return Created($"/classes/{dto.Id}", dto);
    }

    /// <summary>GET /classes → 200, all classes with live seat counts (oldest first; [] if none).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await sender.Send(new GetClassesQuery(), ct));

    /// <summary>GET /classes/{classId} → 200, or 404 CLASS_NOT_FOUND (also for malformed ids).</summary>
    [HttpGet("{classId}")]
    public async Task<IActionResult> Get(string classId, CancellationToken ct) =>
        Ok(await sender.Send(new GetClassByIdQuery(classId), ct));
}
