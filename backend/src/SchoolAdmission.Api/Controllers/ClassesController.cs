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
    /// <remarks>
    /// Route ids are nullable: MVC binds a whitespace segment (e.g. <c>%20</c>) as null, and a non-nullable
    /// parameter would be implicitly required → 400. The handler decides instead → 404.
    /// </remarks>
    [HttpGet("{classId}")]
    public async Task<IActionResult> Get(string? classId, CancellationToken ct) =>
        Ok(await sender.Send(new GetClassByIdQuery(classId ?? string.Empty), ct));
}
