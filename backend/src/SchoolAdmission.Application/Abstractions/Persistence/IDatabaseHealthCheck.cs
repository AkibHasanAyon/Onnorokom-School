namespace SchoolAdmission.Application.Abstractions.Persistence;

/// <summary>Answers "can we run a query right now?" for <c>GET /health</c>.</summary>
public interface IDatabaseHealthCheck
{
    /// <returns>true if <c>SELECT 1</c> succeeds; false if the database is unreachable or failing.</returns>
    Task<bool> CanQueryAsync(CancellationToken ct);
}
