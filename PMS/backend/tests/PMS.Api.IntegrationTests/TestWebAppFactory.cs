using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMS.Application.Abstractions;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests;

/// <summary>
/// Boots the real PMS.Api pipeline against a throwaway SQL Server LocalDB database created
/// from the committed migrations and dropped afterwards, per the plan's test strategy
/// (section 8): "database created per test class from migrations and torn down after
/// (never against a developer's dev database)".
/// </summary>
/// <remarks>
/// <para>
/// The connection string here targets LocalDB with integrated security - no password, no
/// secret, and a database name unique to the run. It is a test fixture, not the application's
/// configured connection string, which stays out of every committed file (F-1 acceptance
/// criterion 4). Override the server with the PMS_TEST_SQLSERVER environment variable.
/// </para>
/// <para>
/// The DbContext is re-registered through <c>ConfigureServices</c> rather than injected as
/// configuration, because under the minimal hosting model Program.cs reads
/// <c>builder.Configuration</c> while registering services, before WebApplicationFactory's
/// configuration hooks are replayed. Service replacement is the only ordering that is
/// deterministic here. The real <c>AddInfrastructure</c> config branches are covered
/// directly by <see cref="Registration.InfrastructureRegistrationTests"/>.
/// </para>
/// </remarks>
public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultServer = @"(localdb)\MSSQLLocalDB";

    private readonly string _databaseName = $"PMSDb_Test_{Guid.NewGuid():N}";

    /// <summary>The connection string this factory injects into the app under test.</summary>
    public string ConnectionString =>
        $"Server={Environment.GetEnvironmentVariable("PMS_TEST_SQLSERVER") ?? DefaultServer};" +
        $"Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDatabaseProbe>();
            services.RemoveAll<PmsDbContext>();
            services.RemoveAll<DbContextOptions<PmsDbContext>>();
            services.RemoveAll<DbContextOptions>();

            services.AddDbContext<PmsDbContext>(options =>
                options.UseSqlServer(
                    ConnectionString,
                    sql => sql.MigrationsAssembly(typeof(PmsDbContext).Assembly.GetName().Name)));

            services.AddScoped<IDatabaseProbe>(sp =>
                new EfCoreDatabaseProbe(sp.GetRequiredService<PmsDbContext>()));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        // Migrate, never EnsureCreated: the point is to prove the committed migrations
        // produce a usable schema, which EnsureCreated would bypass entirely.
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}

/// <summary>
/// Same pipeline, but in the state <c>AddInfrastructure</c> produces when
/// <c>ConnectionStrings:Pms</c> is absent - no DbContext, and a probe that reports
/// "not configured". Exercises the "503 with the connection string removed" half of
/// F-1 acceptance criterion 3 end-to-end, through the real controller and middleware.
/// </summary>
public class NoDatabaseWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDatabaseProbe>();
            services.RemoveAll<PmsDbContext>();
            services.RemoveAll<DbContextOptions<PmsDbContext>>();
            services.RemoveAll<DbContextOptions>();

            services.AddScoped<IDatabaseProbe>(_ => new EfCoreDatabaseProbe(null));
        });
    }
}
