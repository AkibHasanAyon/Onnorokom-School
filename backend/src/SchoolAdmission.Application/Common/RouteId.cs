namespace SchoolAdmission.Application.Common;

/// <summary>
/// Route ids travel as raw strings so validation (400) always runs before not-found (404).
/// Handlers parse them here; anything that isn't a non-empty UUID is treated as unknown (404).
/// </summary>
public static class RouteId
{
    public static bool TryParse(string? raw, out Guid id) => Guid.TryParse(raw, out id) && id != Guid.Empty;
}
