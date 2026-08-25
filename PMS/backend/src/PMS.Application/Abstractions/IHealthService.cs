using PMS.Application.Dtos.Health;

namespace PMS.Application.Abstractions;

/// <summary>
/// Service contract behind the F-1 health endpoints. Controllers depend on this,
/// never on PmsDbContext (section 2, API shape).
/// </summary>
public interface IHealthService
{
    /// <summary>Liveness of the API process itself. Always healthy if it can answer.</summary>
    HealthResponse CheckApi();

    /// <summary>Readiness of the database behind the API.</summary>
    Task<HealthResponse> CheckDatabaseAsync(CancellationToken cancellationToken = default);
}
