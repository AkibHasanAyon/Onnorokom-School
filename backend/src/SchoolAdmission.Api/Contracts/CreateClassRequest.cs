namespace SchoolAdmission.Api.Contracts;

/// <summary>
/// POST /classes body (contract): <c>{ "name": "Class 6 - Morning Shift", "totalSeats": 120 }</c>.
/// Nullable so a missing field is a validation error (400), not a silent default.
/// </summary>
public sealed record CreateClassRequest(string? Name, int? TotalSeats);
