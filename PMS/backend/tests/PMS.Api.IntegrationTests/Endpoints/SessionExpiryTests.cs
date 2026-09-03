using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PMS.Application.Abstractions;
using PMS.Application.Services;

namespace PMS.Api.IntegrationTests.Endpoints;

/// <summary>
/// The 12-hour absolute cap from REC-11, proven rather than asserted from configuration.
/// </summary>
/// <remarks>
/// <para>
/// The cookie handler's own <c>SlidingExpiration</c> renews indefinitely; the cap is enforced
/// by the <c>OnValidatePrincipal</c> hook reading the absolute-expiry claim. That is the piece
/// that could silently regress, so it gets a test that moves the application's clock rather
/// than one that reads back the options object.
/// </para>
/// <para>
/// <b>Two clocks have to move together for that to work.</b> The application's business logic
/// reads <see cref="IClock"/>, but the cookie handler validates the ticket's own
/// <c>ExpiresUtc</c> against its <c>TimeProvider</c>. Substituting only <see cref="IClock"/>
/// left the ticket judged by real wall-clock time, which made every test in this class rot as
/// soon as real time passed the fixture's start instant plus <c>ExpireTimeSpan</c>.
/// <see cref="ClockControlledWebAppFactory"/> therefore drives both from one
/// <see cref="MutableClock"/>, and
/// <see cref="The_cookie_handler_judges_expiry_by_the_fixture_clock_not_the_wall_clock"/>
/// exists specifically to stop that wiring from being lost again.
/// </para>
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

    /// <summary>
    /// The regression guard for the defect this class was written around: the cookie handler
    /// must judge ticket expiry by the fixture's clock, not the machine's.
    /// </summary>
    /// <remarks>
    /// Asserted against the resolved options rather than inferred from a status code, because a
    /// status code cannot tell "expired by the simulated advance" apart from "expired because
    /// today's date drifted past the fixture's start instant" - which is exactly how the
    /// absolute-cap test below went on reporting green while proving nothing.
    /// </remarks>
    [Fact]
    public void The_cookie_handler_judges_expiry_by_the_fixture_clock_not_the_wall_clock()
    {
        var options = _factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        options.TimeProvider.Should().BeSameAs(_factory.Clock.TimeProvider,
            "the handler's ExpiresUtc check must move with the same clock the tests advance");

        _factory.Clock.Reset();
        var before = options.TimeProvider!.GetUtcNow();
        _factory.Clock.Advance(TimeSpan.FromHours(3));

        options.TimeProvider.GetUtcNow().Should().Be(before.AddHours(3),
            "advancing the fixture clock must advance the clock the cookie handler reads");

        _factory.Clock.UtcNow.Should().Be(options.TimeProvider.GetUtcNow(),
            "the application's IClock and the cookie handler's TimeProvider are one clock, "
            + "so a session can never be alive to one and expired to the other");
    }

    [Fact]
    public async Task A_session_survives_activity_well_inside_the_absolute_lifetime()
    {
        _factory.Clock.Reset();
        var client = _factory.CreateHttpsClient();
        await client.PostAsJsonAsync("/api/auth/login", ValidCredentials);

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK,
            "the session has to be alive before the simulated advance, or the assertion below "
            + "proves nothing about the passage of time");

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

        // Keep using it, so sliding renewal has every chance to extend the cookie. Every hour is
        // asserted alive: without that, a fixture whose clock control had broken would arrive at
        // the final assertion already unauthenticated and still report green.
        var slidingRenewalHappened = false;

        for (var hour = 1; hour <= 11; hour++)
        {
            _factory.Clock.Advance(TimeSpan.FromHours(1));
            var response = await client.GetAsync("/api/auth/session");

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"hour {hour} of a clinic day is still inside the 12-hour cap");

            // Past the halfway point the handler reissues the cookie, pushing its own
            // ExpireTimeSpan window out to roughly hour 19. Observing that renewal is what makes
            // the 401 below attributable to the absolute cap rather than to the sliding window
            // simply running out.
            slidingRenewalHappened |= response.Headers.TryGetValues("Set-Cookie", out var setCookie)
                && setCookie.Any(value => value.StartsWith("pms.session=", StringComparison.Ordinal));
        }

        slidingRenewalHappened.Should().BeTrue(
            "sliding renewal must have reissued the cookie during those 11 hours, so the expiry "
            + "proven below is the absolute cap and not the sliding window running out");

        _factory.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));

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

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK,
            "the session has to start alive for its death below to mean anything");

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

        // 11 hours past a re-auth that itself happened 12 hours past sign-in: alive only if the
        // cap restarted at re-auth. A resumed cap would have expired 12 hours ago.
        _factory.Clock.Advance(TimeSpan.FromHours(11));

        (await client.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

/// <summary>
/// <see cref="TestWebAppFactory"/> with <em>every</em> clock the session path reads replaced by
/// one a test can move. Time is the only way to observe an expiry policy without waiting for it.
/// </summary>
/// <remarks>
/// Two substitutions, not one, because the session path has two clocks:
/// <list type="bullet">
/// <item><description>
/// <see cref="IClock"/> - the application's own clock. <c>AuthService</c> stamps the absolute
/// expiry with it, <c>AuthController</c> stamps <c>IssuedUtc</c> with it, and
/// <c>AuthenticationSetup.EnforceAbsoluteExpiryAsync</c> compares against it.
/// </description></item>
/// <item><description>
/// <see cref="Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions.TimeProvider"/> -
/// the clock the cookie handler itself uses to decide whether the ticket's <c>ExpiresUtc</c>
/// has passed and whether to slide. It defaults to <see cref="System.TimeProvider.System"/> and
/// is <em>not</em> covered by replacing <see cref="IClock"/>; left on the system clock, the
/// handler rejected tickets whose simulated issue time was more than <c>ExpireTimeSpan</c> in
/// the real past, whatever the test's clock said.
/// </description></item>
/// </list>
/// </remarks>
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

            // Runs after Program.cs's AddCookie configuration, so it wins: named options
            // configuration is applied in registration order and WebApplicationFactory replays
            // these delegates last. Deliberately scoped to the cookie scheme rather than
            // registered as a host-wide TimeProvider - nothing else in the host should be told
            // it is a different year.
            services.Configure<CookieAuthenticationOptions>(
                CookieAuthenticationDefaults.AuthenticationScheme,
                options => options.TimeProvider = Clock.TimeProvider);
        });
    }
}

/// <summary>
/// A clock the test drives, exposed both as the application's <see cref="IClock"/> and as a
/// <see cref="System.TimeProvider"/> for the cookie authentication handler. One instant behind
/// two interfaces, so a session can never be alive to one and expired to the other.
/// </summary>
public sealed class MutableClock : IClock
{
    /// <summary>
    /// A fixed instant, so runs are reproducible - and a deliberately <em>long past</em> one, so
    /// this fixture can never again quietly depend on the machine's calendar. If the cookie
    /// handler's <c>TimeProvider</c> is ever left on the system clock again, every ticket issued
    /// here is years stale and these tests fail on the very next run rather than on some future
    /// date nobody is watching for.
    /// </summary>
    private static readonly DateTimeOffset Start = new(2020, 1, 1, 8, 0, 0, TimeSpan.Zero);

    public MutableClock()
    {
        TimeProvider = new ClockBackedTimeProvider(this);
    }

    public DateTimeOffset UtcNow { get; private set; } = Start;

    public DateOnly UtcToday => DateOnly.FromDateTime(UtcNow.UtcDateTime);

    /// <summary>The same instant, in the shape the cookie authentication handler consumes.</summary>
    public TimeProvider TimeProvider { get; }

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);

    /// <summary>Back to the start, so tests sharing this fixture do not inherit each other's time.</summary>
    public void Reset() => UtcNow = Start;

    /// <summary>
    /// Only <see cref="System.TimeProvider.GetUtcNow"/> is overridden. Timers and timestamps stay
    /// on the system implementation: wall-clock time is all the handler reads to judge expiry,
    /// and nothing here should make a real timer wait years.
    /// </summary>
    private sealed class ClockBackedTimeProvider(MutableClock clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => clock.UtcNow;
    }
}
