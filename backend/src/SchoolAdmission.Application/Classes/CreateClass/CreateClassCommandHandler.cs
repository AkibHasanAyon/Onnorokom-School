using MediatR;
using SchoolAdmission.Application.Abstractions.Persistence;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Classes.CreateClass;

/// <summary>
/// Creates a class. Name uniqueness (case-insensitive, trimmed) is enforced by the unique index on
/// <c>normalized_name</c>; the unit of work turns a violation into 409 CLASS_NAME_EXISTS, which is also
/// correct when two admins create the same name at the same moment.
/// </summary>
public sealed class CreateClassCommandHandler(IClassRepository classes, TimeProvider clock)
    : IRequestHandler<CreateClassCommand, CreatedClassDto>
{
    public Task<CreatedClassDto> Handle(CreateClassCommand command, CancellationToken cancellationToken)
    {
        // Full precision (not exposed in the API): GET /classes orders by creation time, then id.
        var now = clock.GetUtcNow();
        var schoolClass = SchoolClass.Create(Guid.CreateVersion7(now), command.Name!, command.TotalSeats!.Value, now);
        classes.Add(schoolClass); // TransactionBehavior saves and commits

        return Task.FromResult(new CreatedClassDto(schoolClass.Id, schoolClass.Name, schoolClass.TotalSeats));
    }
}
