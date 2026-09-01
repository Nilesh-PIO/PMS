using Microsoft.EntityFrameworkCore;
using PMS.Application.Abstractions;

namespace PMS.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IDatabaseProbe"/>. Keeps EF entirely inside
/// PMS.Infrastructure - the application layer only sees the abstraction.
/// </summary>
public sealed class EfCoreDatabaseProbe : IDatabaseProbe
{
    /// <summary>
    /// Reason reported when no connection string was configured at all. Deliberately generic:
    /// the health endpoint is anonymous, so it must not describe the deployment.
    /// </summary>
    public const string NotConfiguredReason = "Database connection is not configured.";

    /// <summary>Reason reported when a connection string exists but the server did not answer.</summary>
    public const string UnreachableReason = "Database is not reachable.";

    private readonly PmsDbContext? _dbContext;

    /// <param name="dbContext">
    /// Null when no connection string was supplied - see PmsDbContextRegistration. A missing
    /// connection string is a configuration state to report as 503, not an exception to throw
    /// on startup, because the health endpoint exists precisely to surface it.
    /// </param>
    public EfCoreDatabaseProbe(PmsDbContext? dbContext = null)
    {
        _dbContext = dbContext;
    }

    public async Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext is null)
        {
            return DatabaseProbeResult.Unreachable(NotConfiguredReason);
        }

        try
        {
            var canConnect = await _dbContext.Database
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return canConnect
                ? DatabaseProbeResult.Reachable()
                : DatabaseProbeResult.Unreachable(UnreachableReason);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The exception text can carry the server name and instance; it is logged by the
            // caller's logging pipeline but never returned on an anonymous endpoint.
            return DatabaseProbeResult.Unreachable(UnreachableReason);
        }
    }
}
