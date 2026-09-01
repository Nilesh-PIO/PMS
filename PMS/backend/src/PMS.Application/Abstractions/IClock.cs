namespace PMS.Application.Abstractions;

/// <summary>
/// The only source of "now" in the application layer. Every service takes this rather than
/// calling <see cref="DateTimeOffset.UtcNow"/> directly, so later features (visit draft
/// timestamps, session expiry, VisitDate fixed at draft creation) are deterministically
/// testable. Introduced by F-1 and used by every later service test.
/// </summary>
public interface IClock
{
    /// <summary>Current instant, always in UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Current date in UTC, for entities that store a date rather than an instant.</summary>
    DateOnly UtcToday { get; }
}
