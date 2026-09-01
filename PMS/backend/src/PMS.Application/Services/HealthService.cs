using PMS.Application.Abstractions;
using PMS.Application.Dtos.Health;

namespace PMS.Application.Services;

/// <inheritdoc cref="IHealthService" />
public sealed class HealthService : IHealthService
{
    public const string ApiComponent = "api";
    public const string DatabaseComponent = "database";

    private readonly IDatabaseProbe _databaseProbe;
    private readonly IClock _clock;

    public HealthService(IDatabaseProbe databaseProbe, IClock clock)
    {
        _databaseProbe = databaseProbe;
        _clock = clock;
    }

    public HealthResponse CheckApi() =>
        new(HealthResponse.Healthy, ApiComponent, _clock.UtcNow, null);

    public async Task<HealthResponse> CheckDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var probe = await _databaseProbe.CheckAsync(cancellationToken).ConfigureAwait(false);

        return probe.IsReachable
            ? new HealthResponse(HealthResponse.Healthy, DatabaseComponent, _clock.UtcNow, null)
            : new HealthResponse(HealthResponse.Unhealthy, DatabaseComponent, _clock.UtcNow, probe.Reason);
    }
}
