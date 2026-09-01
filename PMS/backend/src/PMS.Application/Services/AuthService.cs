using PMS.Application.Abstractions;
using PMS.Application.Dtos.Auth;
using PMS.Application.Exceptions;

namespace PMS.Application.Services;

/// <summary>
/// F-2 credential verification. Deliberately says nothing about cookies: the API layer turns
/// a successful <see cref="AuthenticationResult"/> into a <c>HttpOnly</c>/<c>Secure</c>/
/// <c>SameSite=Strict</c> cookie (section 2, Auth), and this service stays testable without
/// an HTTP context.
/// </summary>
public sealed class AuthService : IAuthService
{
    /// <summary>
    /// Whether first-run clinic setup has been completed.
    /// </summary>
    /// <remarks>
    /// ASSUMPTION (plan F-2 point 3 vs. F-3): <c>SessionResponse.setupComplete</c> is part of
    /// F-2's response contract, but the <c>ClinicProfile</c> entity that answers it is F-3's
    /// and does not exist yet. Reporting <c>false</c> is not a placeholder - it is literally
    /// true right now, because no clinic profile has been captured. F-3 replaces this constant
    /// with a read of <c>ClinicProfile.IsSetupComplete</c> and adds the client-side redirect to
    /// /setup; nothing about the wire shape changes when it does.
    /// </remarks>
    private const bool SetupCompleteUntilF3 = false;

    private readonly IAppUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;

    public AuthService(IAppUserRepository users, IPasswordHasher passwordHasher, IClock clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var userName = request.UserName!.Trim();
        var password = request.Password!;

        var user = await _users.FindByUserNameAsync(userName, cancellationToken);

        if (user is null)
        {
            // Verify against a throwaway hash anyway. Returning early here would make an
            // unknown user measurably faster than a wrong password, which is a free user-name
            // oracle on a login endpoint that is reachable anonymously.
            _passwordHasher.Verify(password, DummyHash());
            return AuthenticationResult.Failed();
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            // FailedAttempts is intentionally NOT incremented into a lockout here. Lockout
            // policy is F-21 and is Blocked on C-44: with exactly one user and no recovery
            // path, shipping a lockout now could lock the clinic out of its own patient
            // records permanently. The column exists (plan section 4); the behaviour waits for
            // the owner's decision.
            return AuthenticationResult.Failed();
        }

        var now = _clock.UtcNow;
        user.LastLoginUtc = now;
        user.FailedAttempts = 0;
        await _users.SaveChangesAsync(cancellationToken);

        var session = new SessionResponse(
            user.UserName,
            now.Add(SessionPolicy.AbsoluteLifetime),
            SetupCompleteUntilF3);

        return AuthenticationResult.Success(session, user.SecurityStamp);
    }

    /// <inheritdoc />
    public Task<SessionResponse> DescribeSessionAsync(
        string userName,
        DateTimeOffset absoluteExpiryUtc,
        CancellationToken cancellationToken)
    {
        // The absolute expiry is echoed from the cookie rather than recomputed: recomputing it
        // from "now" would silently extend the session every time the client polled it, which
        // is exactly the sliding-forever behaviour REC-11's absolute cap exists to prevent.
        return Task.FromResult(new SessionResponse(userName, absoluteExpiryUtc, SetupCompleteUntilF3));
    }

    private static void Validate(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            errors[nameof(LoginRequest.UserName)] = ["Enter your user name."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors[nameof(LoginRequest.Password)] = ["Enter your password."];
        }

        if (errors.Count > 0)
        {
            // A missing field is a 400 (the request is malformed). A wrong field is a 401.
            // Keeping those apart means the 401 body never has to say which half was wrong.
            throw new ValidationFailedException(errors);
        }
    }

    /// <summary>
    /// A hash of a value nobody knows, produced by the real hasher so it always parses and
    /// always costs the same work a genuine verification does. Computed at most once per
    /// process; the benign race on first use costs one extra hash, never a wrong answer.
    /// A hard-coded literal was rejected here because it would silently stop equalising the
    /// timing the moment the hash format or iteration count changed.
    /// </summary>
    private string DummyHash() =>
        _dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString("N"));

    private static string? _dummyHash;
}
