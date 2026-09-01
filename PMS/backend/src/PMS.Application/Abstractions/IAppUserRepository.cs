using PMS.Domain.Entities;

namespace PMS.Application.Abstractions;

/// <summary>
/// The only way the application layer reaches the AppUsers table. Services take this
/// interface, never PmsDbContext (section 2, Data access).
/// </summary>
public interface IAppUserRepository
{
    /// <summary>The single physician's row, or null if the credential has never been seeded.</summary>
    Task<AppUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);

    /// <summary>True when at least one AppUser row exists. Used by the one-time seeder.</summary>
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <summary>Stages a new user. Not persisted until <see cref="SaveChangesAsync"/>.</summary>
    Task AddAsync(AppUser user, CancellationToken cancellationToken);

    /// <summary>Persists staged changes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
