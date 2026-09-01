using System.Net;
using FluentAssertions;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-1 acceptance criterion 5: the built React bundle is served by PMS.Api, so the SPA and
/// the API are same-origin - the precondition for the SameSite=Strict cookie chosen in
/// section 2.
/// </summary>
/// <remarks>
/// The bundle is build output and is git-ignored, so it may or may not be present when this
/// runs. Both states are asserted rather than one being skipped: with a bundle the SPA must
/// be served, without one the request must still not be handled by the API's ProblemDetails
/// fallback (which would mean a client route was being treated as a missing endpoint).
/// </remarks>
public class SpaHostingTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public SpaHostingTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    private static bool BundleIsBuilt =>
        File.Exists(Path.Combine(WebRootPath, "index.html"));

    private static string WebRootPath
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && dir.Name != "backend")
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, "src", "PMS.Api", "wwwroot");
        }
    }

    [Fact]
    public async Task Root_serves_the_spa_when_the_bundle_is_built()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        if (BundleIsBuilt)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain("<div id=\"root\">",
                "the SPA mount point is what proves this is the React bundle and not a stray file");
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "with no bundle built there is nothing to serve, but it must still not be an API error");
        }
    }

    [Fact]
    public async Task A_deep_client_route_serves_the_spa_shell_not_an_api_error()
    {
        // React Router owns /patients/123 on a hard refresh; the server must hand back the
        // SPA shell, not a 404 body the router can never recover from.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/patients/123");

        response.Content.Headers.ContentType?.MediaType.Should().NotBe("application/problem+json");

        if (BundleIsBuilt)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        }
    }

    [Fact]
    public async Task The_spa_fallback_never_captures_an_api_route()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/not-a-real-endpoint");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
