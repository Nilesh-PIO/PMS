using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-1 point 5: "every failed write surfaces as a typed error the UI must render, never a
/// swallowed promise". These tests pin the server half of that contract - an API failure is
/// always an RFC-7807 body, never HTML and never an empty response - so that
/// <c>httpClient.ts</c> can always throw a typed ProblemDetailsError (E-47).
/// </summary>
public class ErrorContractTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ErrorContractTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unmatched_api_route_returns_problem_json_not_the_spa_index()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json",
                "an HTML fallback here would make the React client JSON-parse a web page");

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        json.RootElement.TryGetProperty("title", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Unmatched_api_route_body_is_never_empty()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/patients/not-built-yet");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotBeNullOrWhiteSpace(
            "an empty error body is indistinguishable from a successful no-content write");
    }

    [Fact]
    public async Task Unknown_client_route_falls_through_to_the_spa_host()
    {
        // A non-/api path is a React Router route. wwwroot holds no built bundle in the test
        // run, so the correct observable behaviour is "not an API error" - specifically, it
        // must not be handled by the /api ProblemDetails fallback.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/patients/123");

        response.Content.Headers.ContentType?.MediaType
            .Should().NotBe("application/problem+json");
    }
}
