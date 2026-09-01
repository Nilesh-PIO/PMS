using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Abstractions;
using PMS.Infrastructure.Persistence;
using PMS.Infrastructure.Persistence.Repositories;
using PMS.Infrastructure.Security;

namespace PMS.Infrastructure;

/// <summary>
/// Registers the infrastructure layer (EF Core and everything that talks to the outside world).
/// Called from the PMS.Api composition root.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Configuration key holding the SQL Server connection string. Supplied by user-secrets
    /// locally and environment variables in a deployed environment - never committed
    /// (planning-pms-verification.md, section 2 Environments).
    /// </summary>
    public const string ConnectionStringName = "Pms";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // F-2. Password hashing has no database dependency, so it is registered before the
        // connection-string branch below and is available in both states.
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Deliberately not an exception. A missing connection string must be observable
            // through GET /api/health/db as a 503 - that endpoint exists to answer exactly
            // this question, and a startup crash would leave nothing able to answer it.
            services.AddScoped<IDatabaseProbe>(_ => new EfCoreDatabaseProbe(null));
            return services;
        }

        services.AddDbContext<PmsDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(PmsDbContext).Assembly.GetName().Name)));

        services.AddScoped<IDatabaseProbe>(sp =>
            new EfCoreDatabaseProbe(sp.GetRequiredService<PmsDbContext>()));

        // F-2. Only registered alongside the DbContext it needs: with no connection string
        // there is no user store to read, and GET /api/health/db is the endpoint that says so.
        services.AddScoped<IAppUserRepository, AppUserRepository>();

        return services;
    }
}
