namespace SchoolAdmission.Api.Contracts;

/// <summary>Body of <c>GET /health</c>: <c>{ "status": "ok", "serverTime": "2026-11-01T04:03:12Z" }</c>.</summary>
public sealed record HealthResponse(string Status, string ServerTime);
