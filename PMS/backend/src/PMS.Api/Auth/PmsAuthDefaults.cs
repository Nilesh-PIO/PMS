namespace PMS.Api.Auth;

/// <summary>
/// Names shared by the cookie handler, the controller and the tests, so none of them can
/// drift from the others by a typo.
/// </summary>
public static class PmsAuthDefaults
{
    /// <summary>
    /// The session cookie's name. Deliberately not the framework default
    /// (<c>.AspNetCore.Cookies</c>) - a generic name tells a reader nothing, and the tests
    /// assert this exact cookie's attributes.
    /// </summary>
    public const string CookieName = "pms.session";

    /// <summary>
    /// Claim holding the session's absolute expiry as Unix seconds. Written once at sign-in
    /// and never rewritten, which is what makes sliding renewal unable to extend a session
    /// past 12 hours (REC-11).
    /// </summary>
    public const string AbsoluteExpiryClaim = "pms:absexp";

    /// <summary>
    /// Claim holding the user's <c>SecurityStamp</c>, so a future credential change (F-21) has
    /// a way to invalidate cookies that are still in the wild.
    /// </summary>
    public const string SecurityStampClaim = "pms:stamp";
}
