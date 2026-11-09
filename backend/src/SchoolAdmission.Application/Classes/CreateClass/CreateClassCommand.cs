using SchoolAdmission.Application.Abstractions.Messaging;

namespace SchoolAdmission.Application.Classes.CreateClass;

/// <summary>POST /classes. Fields are nullable so missing values reach the validator (→ 400, not a default).</summary>
public sealed record CreateClassCommand(string? Name, int? TotalSeats) : ICommand<CreatedClassDto>;

/// <summary>201 body (contract): <c>{ "id", "name", "totalSeats" }</c>; <c>name</c> is returned trimmed.</summary>
public sealed record CreatedClassDto(Guid Id, string Name, int TotalSeats);
