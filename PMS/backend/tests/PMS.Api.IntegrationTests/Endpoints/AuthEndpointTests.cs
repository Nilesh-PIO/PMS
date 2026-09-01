using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PMS.Api.Auth;
using PMS.Application.Abstractions;
using PMS.Infrastructure.Persistence;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// F-2 backend integration tests (plan F-2 point 6): "login sets an HttpOnly/Secure/
/// SameSite=Strict cookie; protected endpoint returns 401 without it; reauth returns a fresh
/// cookie". Runs the real pipeline against a throwaway LocalDB database created from the
/// committed migrations, with the credential inserted by the real seeder and hashed by the
/// real PBKDF2 hasher.
/// </summary>
public class AuthEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public AuthEndpointTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    private static object ValidCredentials => new
    {
        userName = TestWebAppFactory.TestUserName,
        password = TestWebAppFactory.TestPassword,
    };

    // --- acceptance criterion 1: the cookie's attributes --------------------

    [Fact]
    public async Task Login_with_correct_credentials_returns_200_and_the_session()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("userName").GetString().Should().Be(TestWebAppFactory.TestUserName);
        json.RootElement.GetProperty("expiresUtc").GetDateTimeOffset()
            .Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(12), TimeSpan.FromMinutes(2));
        json.RootElement.GetProperty("setupComplete").GetBoolean().Should().BeFalse(
            "F-3 owns the clinic profile, and it has not been captured yet");
    }

    [Fact]
    public async Task Login_sets_an_HttpOnly_Secure_SameSiteStrict_cookie()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        var setCookie = SessionSetCookie(response);
        setCookie.Should().NotBeNull("login must issue the session cookie");
        setCookie!.Should().Contain("httponly", "script-readable auth is the E-62/E-65 exposure");
        setCookie.Should().Contain("secure");
        setCookie.Should().Contain("samesite=strict");
        setCookie.Should().Contain($"{PmsAuthDefaults.CookieName}=");
    }

    [Fact]
    public async Task The_session_cookie_is_not_persistent()
    {
        // No Expires/Max-Age: closing the browser on a consulting-room PC ends the session
        // rather than leaving it usable by whoever opens the browser next (E-62).
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        var setCookie = SessionSetCookie(response)!;
        setCookie.Should().NotContain("expires=");
        setCookie.Should().NotContain("max-age=");
    }

    [Fact]
    public async Task No_response_body_ever_carries_a_token_or_a_hash()
    {
        // The client has nothing it *could* put in localStorage - which is what makes
        // "no token in web storage" a property of the API, not a frontend habit.
        var client = _factory.CreateHttpsClient();

        var body = await (await client.PostAsJsonAsync("/api/auth/login", ValidCredentials))
            .Content.ReadAsStringAsync();

        body.Should().NotContain(TestWebAppFactory.TestPassword);
        body.Should().NotContain("PBKDF2");
        body.ToLowerInvariant().Should().NotContain("token");
        body.ToLowerInvariant().Should().NotContain("passwordhash");
    }

    // --- wrong and malformed credentials -----------------------------------

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401_and_no_cookie()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = "DefinitelyNotThePassword!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        SessionSetCookie(response).Should().BeNull();
    }

    [Fact]
    public async Task Login_with_an_unknown_user_returns_the_same_401_as_a_wrong_password()
    {
        var client = _factory.CreateHttpsClient();

        var unknownUser = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "no-such-doctor",
            password = TestWebAppFactory.TestPassword,
        });
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = "DefinitelyNotThePassword!",
        });

        unknownUser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Compared without the per-request traceId, which is diagnostic noise rather than
        // anything that distinguishes the two cases.
        var a = WithoutTraceId(await unknownUser.Content.ReadAsStringAsync());
        var b = WithoutTraceId(await wrongPassword.Content.ReadAsStringAsync());
        a.Should().BeEquivalentTo(b, "a different body for the two would confirm the user name");
        a.Should().NotContainKey("detail",
            "a 401 must not explain which half of the credential was wrong");
    }

    [Fact]
    public async Task Login_with_a_missing_field_returns_400_as_ProblemDetails()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { userName = "", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        using var json = JsonDocument.Parse(body);
        json.RootElement.TryGetProperty("errors", out _).Should().BeTrue(
            "a malformed request is a 400 with field errors; a wrong credential is a bare 401");
    }

    // --- acceptance criterion 2: 401 without a cookie -----------------------

    [Fact]
    public async Task Session_without_a_cookie_returns_401()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/auth/session");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unauthenticated_401_is_ProblemDetails_and_never_a_redirect()
    {
        // A 302 to an HTML login page would make httpClient.ts JSON-parse a web page and
        // report a nonsense error instead of "your session ended" (E-47, F-1's error contract).
        var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/auth/session");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        response.Headers.Location.Should().BeNull();

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetInt32().Should().Be(401);
        json.RootElement.GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Logout_without_a_cookie_returns_401()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsync("/api/auth/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_endpoints_stay_anonymous()
    {
        // The plan's exception list is exactly health and auth/login; health must not have
        // been swept up by the default-deny policy.
        var client = _factory.CreateHttpsClient();

        (await client.GetAsync("/api/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/health/db")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unmatched_api_route_is_still_a_404_not_a_401()
    {
        // Regression guard on F-1's committed error contract: default-deny must not turn a
        // path that is not an endpoint into an authentication problem.
        var client = _factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/still-not-a-real-endpoint");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    // --- the authenticated round trip --------------------------------------

    [Fact]
    public async Task Session_with_the_login_cookie_returns_the_same_session()
    {
        var client = _factory.CreateHttpsClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);
        var loginExpiry = (await login.Content.ReadAsStringAsync()).Let(ReadExpiry);

        var session = await client.GetAsync("/api/auth/session");

        session.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await session.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("userName").GetString().Should().Be(TestWebAppFactory.TestUserName);
        ReadExpiry(body).Should().BeCloseTo(loginExpiry, TimeSpan.FromSeconds(1),
            "polling the session must not extend the absolute 12-hour cap");
    }

    [Fact]
    public async Task Logout_clears_the_cookie_and_the_session_is_gone()
    {
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- reauth: the E-41 in-place re-authentication ------------------------

    [Fact]
    public async Task Reauth_without_a_cookie_returns_a_fresh_cookie()
    {
        // This is the endpoint the screen-lock overlay calls. It has to work with no valid
        // cookie at all, because that is exactly the state a locked screen may be in - and it
        // must not require a navigation, because navigating away is what would discard the
        // half-typed consultation underneath the overlay (E-41).
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/reauth", ValidCredentials);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var setCookie = SessionSetCookie(response);
        setCookie.Should().NotBeNull();
        setCookie!.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict");
    }

    [Fact]
    public async Task Reauth_after_logout_restores_a_working_session()
    {
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);
        await client.PostAsync("/api/auth/logout", content: null);

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var reauth = await client.PostAsJsonAsync("/api/auth/reauth", ValidCredentials);

        reauth.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reauth_with_a_wrong_password_returns_401_and_issues_nothing()
    {
        var client = _factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/reauth", new
        {
            userName = TestWebAppFactory.TestUserName,
            password = "StillNotThePassword!",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        SessionSetCookie(response).Should().BeNull();
    }

    [Fact]
    public async Task Reauth_does_not_disturb_an_already_valid_session()
    {
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        var reauth = await client.PostAsJsonAsync("/api/auth/reauth", ValidCredentials);

        reauth.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- the seeded credential ---------------------------------------------

    [Fact]
    public async Task The_seeder_created_exactly_one_user_and_a_second_run_creates_none()
    {
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IInitialUserSeeder>();

        var second = await seeder.SeedAsync("another-doctor", "AnotherPassword#1", CancellationToken.None);

        second.Outcome.Should().Be(InitialUserSeedOutcome.SkippedAlreadySeeded);

        var db = scope.ServiceProvider.GetRequiredService<PmsDbContext>();
        db.AppUsers.Count().Should().Be(1, "the clinic has exactly one physician login");

        // And the original credential still works after the attempted reseed.
        var client = _factory.CreateHttpsClient();
        (await client.PostAsJsonAsync("/api/auth/login", ValidCredentials))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string? SessionSetCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(PmsAuthDefaults.CookieName + "=", StringComparison.Ordinal)
                                         && !v.Contains($"{PmsAuthDefaults.CookieName}=;", StringComparison.Ordinal))
                ?.ToLowerInvariant()
            : null;

    /// <summary>Parses a ProblemDetails body into a dictionary with the per-request traceId dropped.</summary>
    private static Dictionary<string, string> WithoutTraceId(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.EnumerateObject()
            .Where(p => !string.Equals(p.Name, "traceId", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(p => p.Name, p => p.Value.ToString());
    }

    private static DateTimeOffset ReadExpiry(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("expiresUtc").GetDateTimeOffset();
    }
}

internal static class LetExtensions
{
    /// <summary>Small readability helper so a response body can be parsed inline.</summary>
    public static TOut Let<TIn, TOut>(this TIn value, Func<TIn, TOut> map) => map(value);
}
