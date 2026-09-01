namespace PMS.Application.Abstractions;

/// <summary>
/// The one-time credential seeder required by plan F-2 point 2: "seeded by a one-time PMS.Api
/// startup task that reads the initial credential from configuration and refuses to run
/// twice".
/// </summary>
public interface IInitialUserSeeder
{
    /// <summary>
    /// Creates the single physician's AppUser row if, and only if, the AppUsers table is empty.
    /// Idempotent: on every restart after the first it reports
    /// <see cref="InitialUserSeedOutcome.SkippedAlreadySeeded"/> and changes nothing - it never
    /// resets, rehashes or overwrites an existing credential, because that would silently undo
    /// a password the physician had changed.
    /// </summary>
    Task<InitialUserSeedResult> SeedAsync(
        string? userName,
        string? password,
        CancellationToken cancellationToken);
}

/// <summary>What the seeder did. Reported by the caller to the log; never to an HTTP response.</summary>
public enum InitialUserSeedOutcome
{
    /// <summary>The row did not exist and was created.</summary>
    Seeded,

    /// <summary>An AppUser row already existed. Nothing was touched. This is the normal restart path.</summary>
    SkippedAlreadySeeded,

    /// <summary>No seed credential is configured, so there was nothing to create.</summary>
    SkippedNotConfigured,

    /// <summary>
    /// A seed credential was configured but its password is shorter than
    /// <see cref="Services.SessionPolicy.MinimumPasswordLength"/>. Refused rather than
    /// weakened.
    /// </summary>
    RejectedWeakPassword,
}

/// <param name="Outcome">What happened.</param>
/// <param name="Detail">A log-safe explanation. Never contains the password.</param>
public sealed record InitialUserSeedResult(InitialUserSeedOutcome Outcome, string Detail)
{
    public bool Created => Outcome == InitialUserSeedOutcome.Seeded;
}
