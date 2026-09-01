namespace PMS.Application.Dtos.Auth;

/// <summary>
/// Response DTO for login, reauth and GET /api/auth/session
/// (planning-pms-verification.md, F-2 point 3). Serialised camelCase:
/// <c>{ "userName": ..., "expiresUtc": ..., "setupComplete": ... }</c>.
/// </summary>
/// <param name="UserName">The signed-in physician's user name. Never the password or hash.</param>
/// <param name="ExpiresUtc">
/// The session's <em>absolute</em> expiry - sign-in time plus
/// <see cref="Services.SessionPolicy.AbsoluteLifetime"/>. Sliding renewal on activity moves
/// the cookie's own expiry but never this instant (REC-11).
/// </param>
/// <param name="SetupComplete">
/// Whether first-run clinic setup has been completed, so the client knows whether to send the
/// physician to /setup. F-3 owns the real value.
/// </param>
public sealed record SessionResponse(
    string UserName,
    DateTimeOffset ExpiresUtc,
    bool SetupComplete);
