/**
 * TypeScript mirror of the RFC-7807 body every API failure returns
 * (planning-pms-verification.md, section 7 "Error handling").
 *
 * There is exactly one error shape in this application. Anything the UI renders as an error
 * comes through here, which is what makes "the doctor believes it saved" (E-47) preventable
 * rather than merely unlikely.
 */
export interface ProblemDetails {
  /** URI identifying the problem type. */
  type?: string;
  /** Short human-readable summary, safe to show. */
  title?: string;
  /** HTTP status code, mirrored into the body by the server. */
  status?: number;
  /** Human-readable explanation of this specific occurrence. */
  detail?: string;
  /** The request path that failed. */
  instance?: string;
  /** Field-keyed validation messages on a 400. */
  errors?: Record<string, string[]>;
  /** Machine-readable domain-rule slug on a 409, e.g. "setup-incomplete". */
  ruleType?: string;
  /** Correlation id on a 500, quotable when reporting the failure. */
  correlationId?: string;
  /** Any other extension the server adds. */
  [key: string]: unknown;
}

/**
 * Sentinel status used when the request never reached the server at all (offline, DNS
 * failure, connection reset). A real HTTP status is never 0, so callers can distinguish
 * "the server said no" from "we never got an answer" - the second is retryable, the first
 * usually is not.
 */
export const NETWORK_ERROR_STATUS = 0;
