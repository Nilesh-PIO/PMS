/**
 * TypeScript mirror of the API's `SessionResponse` DTO
 * (planning-pms-verification.md, F-2 point 3).
 *
 * There is deliberately no token field: authentication travels in an `HttpOnly` cookie the
 * page's own JavaScript cannot read, so there is nothing here that could be written to
 * `localStorage` even by mistake (section 2 Auth, E-62, E-65).
 */
export interface Session {
  /** The signed-in physician's user name. */
  userName: string;
  /** Absolute expiry, ISO-8601. Sliding renewal never moves this. */
  expiresUtc: string;
  /** Whether first-run clinic setup is done. F-3 acts on this. */
  setupComplete: boolean;
}

/** Request body for login and reauth. */
export interface LoginRequest {
  userName: string;
  password: string;
}
