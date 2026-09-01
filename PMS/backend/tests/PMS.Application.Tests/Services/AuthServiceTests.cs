using FluentAssertions;
using PMS.Application.Dtos.Auth;
using PMS.Application.Exceptions;
using PMS.Application.Services;
using PMS.Application.Tests.TestDoubles;
using PMS.Domain.Entities;

namespace PMS.Application.Tests.Services;

/// <summary>
/// F-2 backend unit tests (plan F-2 point 6): "hash verification, wrong password, expiry
/// calculation".
/// </summary>
public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private const string UserName = "doctor";
    private const string Password = "SeedDoctor#2026!";

    private readonly FixedClock _clock = new(Now);
    private readonly FakePasswordHasher _hasher = new();

    /// <summary>
    /// F-3 added <c>IClinicProfileService</c> to AuthService so that <c>setupComplete</c> is a real
    /// read rather than F-2's constant. These tests are about credentials, so the clinic's setup
    /// state is stubbed; the two tests that care about it state their own value.
    /// </summary>
    private (AuthService Service, FakeAppUserRepository Repository, AppUser User) BuildWithUser(
        bool setupComplete = false)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = UserName,
            PasswordHash = _hasher.Hash(Password),
            SecurityStamp = "stamp-1",
        };

        var repository = new FakeAppUserRepository(user);
        return (
            new AuthService(repository, _hasher, _clock, new StubClinicProfileService(setupComplete)),
            repository,
            user);
    }

    // --- hash verification -------------------------------------------------

    [Fact]
    public async Task Correct_credentials_authenticate()
    {
        var (service, _, _) = BuildWithUser();

        var result = await service.AuthenticateAsync(new LoginRequest(UserName, Password), default);

        result.Succeeded.Should().BeTrue();
        result.Session!.UserName.Should().Be(UserName);
        result.SecurityStamp.Should().Be("stamp-1");
    }

    [Fact]
    public async Task A_successful_sign_in_never_returns_the_password_hash()
    {
        var (service, _, _) = BuildWithUser();

        var result = await service.AuthenticateAsync(new LoginRequest(UserName, Password), default);

        // The DTO has no hash property at all; this asserts the shape stays that way, because
        // a hash on the wire is a hash in the browser's memory and in any proxy log.
        typeof(SessionResponse).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(
                nameof(SessionResponse.UserName),
                nameof(SessionResponse.ExpiresUtc),
                nameof(SessionResponse.SetupComplete));
        result.Session!.ToString().Should().NotContain(_hasher.Hash(Password));
    }

    [Fact]
    public async Task A_successful_sign_in_records_the_login_time_and_clears_failed_attempts()
    {
        var (service, repository, user) = BuildWithUser();
        user.FailedAttempts = 3;

        await service.AuthenticateAsync(new LoginRequest(UserName, Password), default);

        user.LastLoginUtc.Should().Be(Now);
        user.FailedAttempts.Should().Be(0);
        repository.SaveCount.Should().Be(1, "the login time is only useful if it is persisted");
    }

    [Fact]
    public async Task A_user_name_is_trimmed_before_lookup()
    {
        var (service, _, _) = BuildWithUser();

        var result = await service.AuthenticateAsync(new LoginRequest("  doctor  ", Password), default);

        result.Succeeded.Should().BeTrue("a trailing space typed into a login box is not a wrong credential");
    }

    // --- wrong password ----------------------------------------------------

    [Fact]
    public async Task A_wrong_password_does_not_authenticate()
    {
        var (service, _, _) = BuildWithUser();

        var result = await service.AuthenticateAsync(new LoginRequest(UserName, "WrongPassword1!"), default);

        result.Succeeded.Should().BeFalse();
        result.Session.Should().BeNull();
        result.SecurityStamp.Should().BeNull();
    }

    [Fact]
    public async Task A_wrong_password_is_not_persisted_as_a_lockout()
    {
        // Lockout is F-21 and is Blocked on C-44. Shipping it early, with exactly one user and
        // no recovery path, risks locking the clinic out of its own records - so this test
        // pins the deliberate absence rather than leaving it to be "fixed" by accident.
        var (service, repository, user) = BuildWithUser();

        await service.AuthenticateAsync(new LoginRequest(UserName, "WrongPassword1!"), default);

        user.FailedAttempts.Should().Be(0);
        user.LockoutEndUtc.Should().BeNull();
        repository.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task An_unknown_user_name_fails_identically_to_a_wrong_password()
    {
        var (service, _, _) = BuildWithUser();

        var unknownUser = await service.AuthenticateAsync(new LoginRequest("nobody", Password), default);
        var wrongPassword = await service.AuthenticateAsync(new LoginRequest(UserName, "nope-nope-nope"), default);

        unknownUser.Should().BeEquivalentTo(wrongPassword,
            "distinguishing the two would confirm the user name to anyone who can reach the login page");
    }

    [Fact]
    public async Task An_unknown_user_name_still_pays_for_a_hash_verification()
    {
        // Timing equalisation: an early return would make an unknown user measurably faster
        // than a wrong password, which is a free user-name oracle.
        var (service, _, _) = BuildWithUser();
        var before = _hasher.VerifyCount;

        await service.AuthenticateAsync(new LoginRequest("nobody", Password), default);

        _hasher.VerifyCount.Should().Be(before + 1);
    }

    [Fact]
    public async Task Authenticating_against_an_empty_user_store_fails_rather_than_throwing()
    {
        var service = new AuthService(
            new FakeAppUserRepository(), _hasher, _clock, new StubClinicProfileService());

        var result = await service.AuthenticateAsync(new LoginRequest(UserName, Password), default);

        result.Succeeded.Should().BeFalse();
    }

    // --- request validation (400, not 401) ---------------------------------

    [Theory]
    [InlineData(null, "SeedDoctor#2026!")]
    [InlineData("", "SeedDoctor#2026!")]
    [InlineData("   ", "SeedDoctor#2026!")]
    [InlineData("doctor", null)]
    [InlineData("doctor", "")]
    public async Task A_missing_field_is_a_validation_failure_not_a_failed_sign_in(string? userName, string? password)
    {
        var (service, _, _) = BuildWithUser();

        var act = () => service.AuthenticateAsync(new LoginRequest(userName, password), default);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task Validation_errors_name_the_field_that_was_missing()
    {
        var (service, _, _) = BuildWithUser();

        var act = () => service.AuthenticateAsync(new LoginRequest(null, null), default);

        var thrown = await act.Should().ThrowAsync<ValidationFailedException>();
        thrown.Which.Errors.Keys.Should().BeEquivalentTo(
            nameof(LoginRequest.UserName), nameof(LoginRequest.Password));
    }

    // --- expiry calculation (REC-11) ---------------------------------------

    [Fact]
    public async Task The_session_expires_exactly_twelve_hours_after_sign_in()
    {
        var (service, _, _) = BuildWithUser();

        var result = await service.AuthenticateAsync(new LoginRequest(UserName, Password), default);

        result.Session!.ExpiresUtc.Should().Be(Now.AddHours(12));
        SessionPolicy.AbsoluteLifetime.Should().Be(TimeSpan.FromHours(12));
    }

    [Fact]
    public async Task The_expiry_moves_with_the_clock_not_with_a_hard_coded_instant()
    {
        var (service, _, _) = BuildWithUser();
        _clock.Advance(TimeSpan.FromHours(3));

        var result = await service.AuthenticateAsync(new LoginRequest(UserName, Password), default);

        result.Session!.ExpiresUtc.Should().Be(Now.AddHours(3).Add(SessionPolicy.AbsoluteLifetime));
    }

    [Fact]
    public async Task Describing_a_session_echoes_the_cookie_expiry_and_never_extends_it()
    {
        // Recomputing "now + 12h" here would let a client keep a session alive forever simply
        // by polling GET /api/auth/session, defeating the absolute cap REC-11 asks for.
        var (service, _, _) = BuildWithUser();
        var stampedExpiry = Now.AddHours(2);

        _clock.Advance(TimeSpan.FromHours(1));
        var session = await service.DescribeSessionAsync(UserName, stampedExpiry, default);

        session.ExpiresUtc.Should().Be(stampedExpiry);
        session.UserName.Should().Be(UserName);
    }

    // --- policy constants (C-44 / REC-11 assumption) ------------------------

    [Fact]
    public void The_session_policy_matches_the_plans_stated_assumption()
    {
        // These are the plan's assumed answers to C-44 / REC-11, not an owner decision. Pinning
        // them makes a later change to the policy a visible test change, not a silent drift.
        SessionPolicy.IdleLock.Should().Be(TimeSpan.FromMinutes(5));
        SessionPolicy.AbsoluteLifetime.Should().Be(TimeSpan.FromHours(12));
        SessionPolicy.MinimumPasswordLength.Should().Be(12);
    }

    [Fact]
    public void The_idle_lock_is_far_shorter_than_the_session()
    {
        // The whole E-41/E-62 mitigation rests on this inequality: locking the screen must
        // never be the same event as ending the session, or a lock would cost a draft.
        SessionPolicy.IdleLock.Should().BeLessThan(SessionPolicy.AbsoluteLifetime);
    }

    // --- F-3: setupComplete is a real read, not F-2's constant --------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sign_in_reports_the_clinics_actual_setup_state(bool setupComplete)
    {
        var (service, _, _) = BuildWithUser(setupComplete);

        var result = await service.AuthenticateAsync(new LoginRequest(UserName, Password), default);

        result.Session!.SetupComplete.Should().Be(setupComplete);
    }

    [Fact]
    public async Task Describing_a_session_re_reads_setup_state_rather_than_caching_it()
    {
        // E-1. The physician completes setup *during* a session. If setupComplete were stamped
        // into the cookie at sign-in, finishing the setup form would leave the client still being
        // redirected back to /setup until the next sign-out - so it must be read every time.
        var stub = new StubClinicProfileService(setupComplete: true);
        var service = new AuthService(
            new FakeAppUserRepository(), _hasher, _clock, stub);

        await service.DescribeSessionAsync(UserName, Now.AddHours(2), default);
        await service.DescribeSessionAsync(UserName, Now.AddHours(2), default);

        stub.IsSetupCompleteCallCount.Should().Be(2);
    }

    [Fact]
    public async Task A_described_session_reports_setup_complete_when_the_clinic_is_configured()
    {
        var service = new AuthService(
            new FakeAppUserRepository(), _hasher, _clock, new StubClinicProfileService(true));

        var session = await service.DescribeSessionAsync(UserName, Now.AddHours(2), default);

        session.SetupComplete.Should().BeTrue();
    }
}
