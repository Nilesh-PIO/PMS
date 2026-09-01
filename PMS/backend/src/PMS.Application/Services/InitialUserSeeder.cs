using PMS.Application.Abstractions;
using PMS.Domain.Entities;

namespace PMS.Application.Services;

/// <summary>
/// Creates the clinic's single physician login once, from a configured credential.
/// </summary>
/// <remarks>
/// The "refuses to run twice" guarantee has two independent layers, on purpose. This class
/// checks <see cref="IAppUserRepository.AnyAsync"/> before inserting, and the unique index on
/// <c>AppUsers.UserName</c> (F-1's AppUserConfiguration) makes a second row impossible even if
/// this check were wrong. A seeder that reseeds is a data-integrity fault, not an
/// inconvenience: it would overwrite a password the physician had changed and hand the clinic
/// back a credential that is written down in a config file.
/// </remarks>
public sealed class InitialUserSeeder : IInitialUserSeeder
{
    private readonly IAppUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;

    public InitialUserSeeder(IAppUserRepository users, IPasswordHasher passwordHasher)
    {
        _users = users;
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public async Task<InitialUserSeedResult> SeedAsync(
        string? userName,
        string? password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return new InitialUserSeedResult(
                InitialUserSeedOutcome.SkippedNotConfigured,
                "No seed credential is configured, so no login was created.");
        }

        if (password.Length < SessionPolicy.MinimumPasswordLength)
        {
            return new InitialUserSeedResult(
                InitialUserSeedOutcome.RejectedWeakPassword,
                $"The configured seed password is shorter than the {SessionPolicy.MinimumPasswordLength}-character " +
                "minimum (REC-11). No login was created.");
        }

        if (await _users.AnyAsync(cancellationToken))
        {
            return new InitialUserSeedResult(
                InitialUserSeedOutcome.SkippedAlreadySeeded,
                "A login already exists; the seed credential was ignored and nothing was changed.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName.Trim(),
            // Only ever the hash. The plaintext is never persisted, logged or echoed.
            PasswordHash = _passwordHasher.Hash(password),
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FailedAttempts = 0,
            LockoutEndUtc = null,
            LastLoginUtc = null,
        };

        await _users.AddAsync(user, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new InitialUserSeedResult(
            InitialUserSeedOutcome.Seeded,
            $"Created the initial login '{user.UserName}'.");
    }
}
