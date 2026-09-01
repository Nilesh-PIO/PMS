using PMS.Application.Dtos.Auth;

namespace PMS.Application.Abstractions;

/// <summary>
/// Credential verification and session description for F-2. Knows nothing about cookies -
/// issuing and clearing the cookie is the API layer's job; deciding whether a credential is
/// good, and when the resulting session must die, is this service's.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Verifies a user name and password.
    /// </summary>
    /// <exception cref="Exceptions.ValidationFailedException">
    /// The request omitted a user name or a password (HTTP 400). A <em>wrong</em> credential is
    /// not a validation failure - it returns an unsuccessful result so the caller answers 401,
    /// because telling a caller which field was wrong is a credential oracle.
    /// </exception>
    Task<AuthenticationResult> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Describes the session of an already-authenticated caller, for GET /api/auth/session.
    /// </summary>
    /// <param name="userName">From the cookie's claims, never from the request body.</param>
    /// <param name="absoluteExpiryUtc">The absolute expiry stamped into the cookie at sign-in.</param>
    Task<SessionResponse> DescribeSessionAsync(
        string userName,
        DateTimeOffset absoluteExpiryUtc,
        CancellationToken cancellationToken);
}

/// <summary>Outcome of a credential check. Carries no reason on failure, by design.</summary>
/// <param name="Succeeded">Whether the credential matched a stored user.</param>
/// <param name="Session">The session to report to the client; null when <paramref name="Succeeded"/> is false.</param>
/// <param name="SecurityStamp">
/// The user's security stamp, written into the cookie so a future credential change can
/// invalidate live sessions. Null when unsuccessful.
/// </param>
public sealed record AuthenticationResult(
    bool Succeeded,
    SessionResponse? Session,
    string? SecurityStamp)
{
    /// <summary>A failed check. Deliberately indistinguishable between "no such user" and "wrong password".</summary>
    public static AuthenticationResult Failed() => new(false, null, null);

    /// <summary>A successful check.</summary>
    public static AuthenticationResult Success(SessionResponse session, string securityStamp) =>
        new(true, session, securityStamp);
}
