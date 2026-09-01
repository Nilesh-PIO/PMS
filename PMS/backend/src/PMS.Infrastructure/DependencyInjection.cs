using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Abstractions;
using PMS.Infrastructure.Persistence;

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

        return services;
    }
}
