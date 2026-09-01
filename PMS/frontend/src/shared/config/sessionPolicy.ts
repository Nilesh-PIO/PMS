/**
 * The client half of the session policy, mirroring `PMS.Application/Services/SessionPolicy.cs`.
 *
 * These are the plan's stated assumption for the open item **C-44 / REC-11**
 * (planning-pms-verification.md, F-2 point 1), not an answer from the physician. If C-44 is
 * decided differently, this file and its C# counterpart are the two places that change.
 */

/**
 * Idle time before the screen lock covers PHI: 5 minutes (E-62).
 *
 * Deliberately far shorter than the 12-hour session: locking the screen is a display event,
 * and must never be the same event as ending a session, or stepping out of the room would cost
 * a half-typed consultation (E-41).
 */
export const IDLE_LOCK_MS = 5 * 60 * 1000;

/** Absolute session lifetime: 12 hours from sign-in. Enforced by the server; shown here for the UI. */
export const ABSOLUTE_SESSION_MS = 12 * 60 * 60 * 1000;

/** Minimum password length (REC-11). Used to give the login form an honest hint. */
export const MIN_PASSWORD_LENGTH = 12;
