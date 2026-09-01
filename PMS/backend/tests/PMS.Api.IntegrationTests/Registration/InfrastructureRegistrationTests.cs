using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PMS.Application.Abstractions;
using PMS.Infrastructure;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests.Registration;

/// <summary>
/// Covers the real <c>AddInfrastructure</c> configuration branches directly, since
/// <see cref="TestWebAppFactory"/> replaces the DbContext registration and therefore cannot
/// observe them. Together with the endpoint tests this closes F-1 acceptance criteria 3 and 4:
/// a missing connection string is a reportable 503 state, not a startup crash, and nothing
/// hardcodes a connection string.
/// </summary>
public class InfrastructureRegistrationTests
{
    private static IConfiguration ConfigurationWith(params (string Key, string? Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => e.Value))
            .Build();

    [Fact]
    public async Task With_no_connection_string_the_probe_reports_not_configured_rather_than_throwing()
    {
        var services = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith())
            .BuildServiceProvider();

        var probe = services.GetRequiredService<IDatabaseProbe>();
        var result = await probe.CheckAsync();

        result.IsReachable.Should().BeFalse();
        result.Reason.Should().Be(EfCoreDatabaseProbe.NotConfiguredReason);
    }

    [Fact]
    public void With_no_connection_string_no_DbContext_is_registered()
    {
        var services = new ServiceCollection().AddInfrastructure(ConfigurationWith());

        services.Should().NotContain(d => d.ServiceType == typeof(PmsDbContext));
    }

    [Fact]
    public async Task A_blank_connection_string_is_treated_as_absent()
    {
        var services = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(("ConnectionStrings:Pms", "   ")))
            .BuildServiceProvider();

        var result = await services.GetRequiredService<IDatabaseProbe>().CheckAsync();

        result.IsReachable.Should().BeFalse();
        result.Reason.Should().Be(EfCoreDatabaseProbe.NotConfiguredReason);
    }

    [Fact]
    public void With_a_connection_string_the_DbContext_is_registered_against_sql_server()
    {
        var services = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(
                ("ConnectionStrings:Pms", @"Server=(localdb)\MSSQLLocalDB;Database=PMSDb_Registration_Probe;Trusted_Connection=True;TrustServerCertificate=True")))
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();

        db.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
    }

    [Fact]
    public async Task An_unreachable_server_reports_unreachable_rather_than_propagating_the_exception()
    {
        // A hostname that cannot resolve: the probe must answer, because /api/health/db
        // exists precisely to report this state.
        var services = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(
                ("ConnectionStrings:Pms", "Server=pms-no-such-host,14330;Database=PMSDb;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=2")))
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var probe = scope.ServiceProvider.GetRequiredService<IDatabaseProbe>();

        var result = await probe.CheckAsync();

        result.IsReachable.Should().BeFalse();
        result.Reason.Should().Be(EfCoreDatabaseProbe.UnreachableReason);
    }

    [Fact]
    public async Task The_probe_reason_never_echoes_the_connection_string()
    {
        var services = new ServiceCollection()
            .AddInfrastructure(ConfigurationWith(
                ("ConnectionStrings:Pms", "Server=pms-no-such-host,14330;Database=SecretDbName;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=2")))
            .BuildServiceProvider();

        using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IDatabaseProbe>().CheckAsync();

        result.Reason.Should().NotContainEquivalentOf("SecretDbName");
        result.Reason.Should().NotContainEquivalentOf("pms-no-such-host");
    }
}
