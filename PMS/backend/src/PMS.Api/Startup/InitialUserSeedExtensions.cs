using PMS.Application.Abstractions;

namespace PMS.Api.Startup;

/// <summary>
/// The one-time startup task required by plan F-2 point 2: read an initial credential from
/// configuration, hash it, insert the single <c>AppUser</c> row, and refuse to run twice.
/// </summary>
public static class InitialUserSeedExtensions
{
    /// <summary>Configuration section holding the seed credential.</summary>
    public const string SectionName = "SeedUser";

    /// <summary>Key for the seed user name, relative to <see cref="SectionName"/>.</summary>
    public const string UserNameKey = SectionName + ":UserName";

    /// <summary>Key for the seed password, relative to <see cref="SectionName"/>.</summary>
    public const string PasswordKey = SectionName + ":Password";

    /// <summary>
    /// Creates the physician's login on first start and does nothing on every start after.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>DEVIATION from planning-pms-verification.md (F-2 point 2 and section 2
    /// "Environments") — explicit user instruction, recorded here so it is not mistaken for an
    /// oversight.</b> The plan requires the seed credential to come from user-secrets locally
    /// or an environment variable when deployed, with <c>appsettings.json</c> carrying no real
    /// value, and states that "no connection string, password pepper or signing key is ever
    /// committed". At the user's explicit direction — given after being warned that this
    /// departs from both the plan and normal secret handling — the real seed user name and
    /// password are written in plain text into the tracked file
    /// <c>PMS/backend/src/PMS.Api/appsettings.json</c> instead. <b>Committing that file puts a
    /// working login credential for this application into git history permanently, where
    /// rotating the password does not remove it.</b> The configuration keys below are read the
    /// same way either way, so supplying <c>SeedUser__Password</c> as an environment variable
    /// still overrides the committed value and is the way back to the plan's design.
    /// </para>
    /// <para>
    /// Failures here are logged, never thrown. A database that is missing, unmigrated or
    /// unreachable at startup must leave the API able to answer <c>GET /api/health/db</c> with
    /// a diagnosable 503, which is F-1's established behaviour; crashing the process instead
    /// would leave nothing running that could explain why.
    /// </para>
    /// </remarks>
    public static async Task SeedInitialUserAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        var logger = services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(InitialUserSeedExtensions).FullName!);

        var configuration = services.GetRequiredService<IConfiguration>();

        // DEVIATION read site (see the remarks above): in the plan's design these two values
        // arrive from user-secrets or the environment. Per explicit user instruction they are
        // committed in appsettings.json instead.
        var userName = configuration[UserNameKey];
        var password = configuration[PasswordKey];

        try
        {
            // Resolution itself is inside the try: with no connection string, AddInfrastructure
            // never registers IAppUserRepository, so building the seeder throws rather than
            // returning null. That state is F-1's documented "database not configured" case and
            // must stay a 503 on /api/health/db, not a refusal to start.
            var seeder = services.GetRequiredService<IInitialUserSeeder>();

            var result = await seeder.SeedAsync(userName, password, cancellationToken);

            // The user name is logged; the password never is, under any outcome.
            if (result.Created)
            {
                logger.LogInformation("Initial login seeding: {Detail}", result.Detail);
            }
            else
            {
                logger.LogInformation(
                    "Initial login seeding skipped ({Outcome}): {Detail}", result.Outcome, result.Detail);
            }
        }
        catch (Exception ex)
        {
            // Never rethrow. A database that is missing, unmigrated, unreachable or simply not
            // configured must leave the API running and able to answer GET /api/health/db;
            // crashing here would remove the only thing that could explain the problem.
            logger.LogError(
                ex,
                "Initial login seeding could not run. The database may be unconfigured, "
                + "unreachable or not migrated. Check GET /api/health/db, run "
                + "`dotnet ef database update`, and restart.");
        }
    }
}
