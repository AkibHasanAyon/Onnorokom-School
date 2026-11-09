using MediatR;
using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Application.Classes.CreateClass;

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
}
