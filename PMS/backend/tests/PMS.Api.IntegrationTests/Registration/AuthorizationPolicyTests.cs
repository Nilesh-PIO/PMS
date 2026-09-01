using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PMS.Api.Auth;
using PMS.Application.Services;

namespace PMS.Api.IntegrationTests.Registration;

/// <summary>
/// F-2 acceptance criterion 2, asserted structurally as well as by request: every /api route
/// other than health and auth/login requires the cookie.
/// </summary>
/// <remarks>
/// The per-request tests in <see cref="Endpoints.AuthEndpointTests"/> prove the endpoints that
/// exist today behave correctly. This class guards the endpoints that do not exist yet: the
/// policy is default-deny, so a controller added by F-5 or F-10 is protected unless someone
/// explicitly opts it out - and the allow-list below turns any such opt-out into a failing
/// test rather than a silently public PHI route.
/// </remarks>
public class AuthorizationPolicyTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public AuthorizationPolicyTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The complete set of API routes allowed to be anonymous. The plan names health and
    /// auth/login; auth/reauth is here because it is credential-bearing by design and is
    /// reached precisely when the cookie has expired (E-41), and the /api catch-all is here so
    /// a path that is not an endpoint stays a 404 rather than becoming a 401 (F-1's contract).
    /// </summary>
    private static readonly string[] ExpectedAnonymousApiRoutes =
    [
        "api/health",
        "api/health/db",
        "api/auth/login",
        "api/auth/reauth",
        "api/{**slug}",
    ];

    [Fact]
    public void The_fallback_policy_requires_an_authenticated_user()
    {
        var options = _factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        options.FallbackPolicy.Should().NotBeNull(
            "without a fallback policy every new controller would ship unprotected by default");
        options.FallbackPolicy!.Requirements
            .Should().ContainSingle(r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void Exactly_the_expected_api_routes_are_anonymous()
    {
        // MapControllers records patterns without a leading slash; MapFallback keeps the one it
        // was given, so both forms are normalised before comparing.
        var anonymous = _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new { Endpoint = e, Route = e.RoutePattern.RawText?.TrimStart('/') })
            .Where(x => x.Route is not null && x.Route.StartsWith("api", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(x => x.Route!)
            .Distinct()
            .OrderBy(r => r, StringComparer.Ordinal);

        anonymous.Should().BeEquivalentTo(ExpectedAnonymousApiRoutes,
            "any new anonymous /api route must be a deliberate, reviewed decision");
    }

    [Fact]
    public void The_spa_fallback_is_anonymous_so_the_login_screen_is_reachable()
    {
        var spaFallback = _factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == "{*path:nonfile}");

        spaFallback.Metadata.GetMetadata<IAllowAnonymous>().Should().NotBeNull(
            "gating the bundle behind auth would leave a signed-out visitor with no login page to use");
    }

    [Fact]
    public void The_cookie_is_configured_HttpOnly_Secure_SameSiteStrict_and_sliding()
    {
        var options = _factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        options.Cookie.Name.Should().Be(PmsAuthDefaults.CookieName);
        options.Cookie.HttpOnly.Should().BeTrue();
        options.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.Always);
        options.Cookie.SameSite.Should().Be(SameSiteMode.Strict);
        options.Cookie.IsEssential.Should().BeTrue();

        options.SlidingExpiration.Should().BeTrue("REC-11 asks for sliding renewal on activity");
        options.ExpireTimeSpan.Should().Be(SessionPolicy.SlidingWindow);
    }
}
