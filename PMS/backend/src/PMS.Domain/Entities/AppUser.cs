namespace PMS.Domain.Entities;

/// <summary>
/// The single clinic user (one general physician). Exactly one row exists in Phase 1.
/// Shape follows the plan's data model overview (planning-pms-verification.md, section 4).
/// Credential seeding, login and lockout behaviour belong to F-2 / F-21 and are not
/// implemented here — F-1 only establishes the table so <c>InitialCreate</c> has a schema.
/// </summary>
public class AppUser
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string SecurityStamp { get; set; } = string.Empty;

    public int FailedAttempts { get; set; }

    public DateTimeOffset? LockoutEndUtc { get; set; }

    public DateTimeOffset? LastLoginUtc { get; set; }
}
