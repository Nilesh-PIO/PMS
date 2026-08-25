namespace PMS.Application.Dtos.Health;

/// <summary>
/// Response DTO for GET /api/health and GET /api/health/db.
/// A DTO, not an entity - no EF type ever crosses the wire (section 2, API shape).
/// </summary>
/// <param name="Status">"Healthy" or "Unhealthy".</param>
/// <param name="Component">Which thing was checked: "api" or "database".</param>
/// <param name="CheckedUtc">When the check ran, from <see cref="Abstractions.IClock"/>.</param>
/// <param name="Detail">
/// Short non-sensitive reason when unhealthy; null when healthy. Never contains a connection
/// string, credential or PHI.
/// </param>
// ASSUMPTION (plan section 6, F-1 point 3): the plan names `HealthResponse` but does not
// specify its fields. These four are the smallest set that satisfies "200 with a live SQL
// Server and 503 with the connection string removed" while staying non-sensitive.
public sealed record HealthResponse(
    string Status,
    string Component,
    DateTimeOffset CheckedUtc,
    string? Detail)
{
    public const string Healthy = "Healthy";
    public const string Unhealthy = "Unhealthy";
}
