namespace PMS.Application.Abstractions;

/// <summary>
/// Lets the application layer ask "is the database reachable?" without referencing EF Core.
/// Implemented in PMS.Infrastructure. Keeps the F-1 health service inside the
/// Controller -> Service -> Repository layering (planning-pms-verification.md, section 2).
/// </summary>
public interface IDatabaseProbe
{
    /// <summary>
    /// True when a connection to the configured database can be opened. Never throws for an
    /// expected failure (missing/blank connection string, server down) - those return false
    /// with a non-PHI reason, because the health endpoint must answer, not blow up.
    /// </summary>
    Task<DatabaseProbeResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <param name="IsReachable">Whether the database answered.</param>
/// <param name="Reason">
/// A short, non-sensitive explanation when unreachable. Never contains the connection string,
/// credentials or any PHI - operational logs must not carry those (section 7, Logging &amp; audit).
/// </param>
public readonly record struct DatabaseProbeResult(bool IsReachable, string? Reason)
{
    public static DatabaseProbeResult Reachable() => new(true, null);

    public static DatabaseProbeResult Unreachable(string reason) => new(false, reason);
}
