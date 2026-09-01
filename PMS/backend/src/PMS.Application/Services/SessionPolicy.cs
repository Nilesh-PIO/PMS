namespace PMS.Application.Services;

/// <summary>
/// The session and password policy for F-2, in one place so the API cookie handler, the
/// seeder and the React client cannot drift apart.
/// </summary>
/// <remarks>
/// <para>
/// These values implement the plan's stated assumption for the open item <b>C-44 / REC-11</b>
/// (planning-pms-verification.md, F-2 point 1), which brainstorm section 12 carries no
/// <c>Q-</c> for. They are an assumption, not an owner decision - if the physician answers
/// C-44 differently, this file is the only place that changes.
/// </para>
/// <para>
/// The two timeouts are deliberately independent. The <b>idle lock</b> hides PHI on a
/// consulting-room screen left unattended (E-62) and is a client-side overlay that never
/// unmounts the page beneath it, so a half-typed consultation survives it (E-41). The
/// <b>absolute session lifetime</b> is the server-side cap on how long one sign-in is good
/// for. Making the lock short and the session long is what stops "the screen locked" from
/// ever meaning "your typing was discarded".
/// </para>
/// </remarks>
public static class SessionPolicy
{
    /// <summary>
    /// Idle time before the screen-lock overlay covers PHI: <b>5 minutes</b> (REC-11, E-62).
    /// Exposed to the client by GET /api/auth/session's contract and consumed by
    /// <c>useIdleTimer</c>; the server does not enforce it, because a lock is a display state,
    /// not a session state.
    /// </summary>
    public static readonly TimeSpan IdleLock = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Absolute session lifetime: <b>12 hours</b> from sign-in (REC-11). Long enough to cover
    /// a full clinic day so the physician is not re-authenticating between patients, and hard
    /// enough that an abandoned browser cannot stay authenticated overnight.
    /// </summary>
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// Sliding renewal window. The cookie handler reissues the cookie once more than half of
    /// this has elapsed, but never past <see cref="AbsoluteLifetime"/> measured from the
    /// original sign-in - "sliding renewal within an absolute lifetime" (REC-11).
    /// </summary>
    public static readonly TimeSpan SlidingWindow = TimeSpan.FromHours(12);

    /// <summary>
    /// Minimum password length: <b>12 characters</b>, with <b>no forced rotation</b> (REC-11).
    /// Rotation is deliberately absent: with exactly one user and no recovery path until F-21
    /// resolves C-44, a forced change is a realistic route to locking the clinic out of its
    /// own records.
    /// </summary>
    public const int MinimumPasswordLength = 12;
}
