using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMS.Application.Abstractions;
using PMS.Application.Services;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// The 12-hour absolute cap from REC-11, proven rather than asserted from configuration.
/// </summary>
/// <remarks>
/// The cookie handler's own <c>SlidingExpiration</c> renews indefinitely; the cap is enforced
/// by the <c>OnValidatePrincipal</c> hook reading the absolute-expiry claim. That is the piece
/// that could silently regress, so it gets a test that moves the application's clock rather
/// than one that reads back the options object.
/// </remarks>
public class SessionExpiryTests : IClassFixture<ClockControlledWebAppFactory>
{
    private readonly ClockControlledWebAppFactory _factory;

    public SessionExpiryTests(ClockControlledWebAppFactory factory)
    {
        _factory = factory;
    }

    private static object ValidCredentials => new
    {
        userName = TestWebAppFactory.TestUserName,
        password = TestWebAppFactory.TestPassword,
    };

    [Fact]
    public async Task A_session_survives_activity_well_inside_the_absolute_lifetime()
    {
        _factory.Clock.Reset();
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        _factory.Clock.Advance(TimeSpan.FromHours(11));

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK,
            "a full clinic day must not require signing in again");
    }

    [Fact]
    public async Task A_session_is_dead_once_it_passes_twelve_hours_however_active_it_has_been()
    {
        _factory.Clock.Reset();
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        // Keep using it, so sliding renewal has every chance to extend the cookie.
        for (var hour = 0; hour < 12; hour++)
        {
            _factory.Clock.Advance(TimeSpan.FromHours(1));
            await client.GetAsync("/api/auth/session");
        }

        _factory.Clock.Advance(TimeSpan.FromMinutes(1));

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "sliding renewal must not be able to push a session past its absolute cap");
    }

    [Fact]
    public async Task An_expired_session_can_be_re_authenticated_in_place()
    {
        // The E-41 path end to end on the server side: the session dies mid-consultation, and
        // the physician recovers it with one credential-bearing POST - no navigation, no page
        // reload, so nothing the client is holding needs to be torn down.
        _factory.Clock.Reset();
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        _factory.Clock.Advance(SessionPolicy.AbsoluteLifetime + TimeSpan.FromMinutes(1));
        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var reauth = await client.PostAsJsonAsync("/api/auth/reauth", ValidCredentials);

        reauth.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Re_authentication_starts_a_fresh_twelve_hours_not_a_resumed_one()
    {
        _factory.Clock.Reset();
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        _factory.Clock.Advance(SessionPolicy.AbsoluteLifetime + TimeSpan.FromMinutes(1));
        await client.PostAsJsonAsync("/api/auth/reauth", ValidCredentials);

        _factory.Clock.Advance(TimeSpan.FromHours(11));

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

/// <summary>
/// <see cref="TestWebAppFactory"/> with the application's <see cref="IClock"/> replaced by one
/// a test can move. Time is the only way to observe an expiry policy without waiting for it.
/// </summary>
public class ClockControlledWebAppFactory : TestWebAppFactory
{
    public MutableClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);
        });
    }
}

/// <summary>A clock the test drives. Starts at a fixed instant so runs are reproducible.</summary>
public sealed class MutableClock : IClock
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; private set; } = Start;

    public DateOnly UtcToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);

    /// <summary>Back to the start, so tests sharing this fixture do not inherit each other's time.</summary>
    public void Reset() => UtcNow = Start;
}
