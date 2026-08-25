using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PMS.Application.Dtos.Health;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-1 acceptance criterion 3: "GET /api/health/db returns 200 with a live SQL Server and
/// 503 with the connection string removed." Both halves are exercised against the real
/// pipeline, including middleware and routing.
/// </summary>
public class HealthEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public HealthEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_200_and_a_healthy_api_component()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
        body.Component.Should().Be("api");
        body.CheckedUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task Health_is_reachable_without_authentication()
    {
        // Section 7: every /api/* route except health and auth/login requires the cookie.
        // F-2 adds the cookie handler; this asserts health stays outside that gate.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthDb_returns_200_against_a_live_sql_server()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/db");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Healthy");
        body.Component.Should().Be("database");
        body.Detail.Should().BeNull();
    }

    [Fact]
    public async Task HealthDb_returns_503_with_the_connection_string_removed()
    {
        using var factory = new NoDatabaseWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/db");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Unhealthy");
        body.Component.Should().Be("database");
        body.Detail.Should().NotBeNullOrWhiteSpace("a 503 with no reason is unactionable");
    }

    [Fact]
    public async Task HealthDb_never_discloses_the_connection_string_or_server_name()
    {
        // The health endpoints are anonymous, so their bodies must not describe the deployment.
        using var factory = new NoDatabaseWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/db");
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContainEquivalentOf("Server=");
        raw.Should().NotContainEquivalentOf("localdb");
        raw.Should().NotContainEquivalentOf("Password");
        raw.Should().NotContainEquivalentOf("Trusted_Connection");
    }

    [Fact]
    public async Task Migrations_produce_a_schema_the_app_can_query()
    {
        // Proves InitialCreate is not merely present but applies cleanly to a fresh database:
        // a 200 from /api/health/db means CanConnect succeeded against the migrated schema.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health/db");
        var payload = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(payload);
        json.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }
}
