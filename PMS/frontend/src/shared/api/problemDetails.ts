import {
  NETWORK_ERROR_STATUS,
  type ProblemDetails,
} from '../types/problemDetails';

/**
 * The single error type thrown by {@link request}. Every mutation hook surfaces it;
 * no promise rejection is ever swallowed (planning-pms-verification.md, section 7).
 */
export class ProblemDetailsError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(problem: ProblemDetails, status: number) {
    super(problem.title || problem.detail || `Request failed with status ${status}.`);
    this.name = 'ProblemDetailsError';
    this.status = status;
    this.problem = problem;

    // Required for `instanceof` to work when targeting ES5-era output.
    Object.setPrototypeOf(this, ProblemDetailsError.prototype);
  }

  /** Field-keyed validation messages, or an empty object when this is not a 400. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem.errors ?? {};
  }

  /** Machine-readable domain-rule slug on a 409, or undefined. */
  get ruleType(): string | undefined {
    return typeof this.problem.ruleType === 'string' ? this.problem.ruleType : undefined;
  }

  /** True when the request never reached the server, so a retry is meaningful. */
  get isNetworkError(): boolean {
    return this.status === NETWORK_ERROR_STATUS;
  }

  /** Best single sentence to put in front of the user. */
  get userMessage(): string {
    if (this.isNetworkError) {
      return 'Could not reach the server. Your work has not been saved yet.';
    }
    return this.problem.detail || this.problem.title || 'Something went wrong.';
  }
}

export function isProblemDetailsError(error: unknown): error is ProblemDetailsError {
  return error instanceof ProblemDetailsError;
}

/**
 * Turns any failed Response into a ProblemDetails, whatever the server actually sent.
 *
 * A proxy, a load balancer or a crashed host can return HTML, plain text or nothing at all.
 * Those must not become `undefined` in a catch block, because a caller that reads
 * `error.problem.title` on undefined throws a second, meaningless error and the real failure
 * disappears - which is exactly the silent-loss shape of E-47.
 */
export async function parseProblemDetails(response: Response): Promise<ProblemDetails> {
  const fallback: ProblemDetails = {
    title: response.statusText || 'Request failed.',
    status: response.status,
  };

  let raw: string;
  try {
    raw = await response.text();
  } catch {
    return fallback;
  }

  if (!raw.trim()) {
    return fallback;
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    // Non-JSON body (an HTML error page, a proxy's plain-text message). Keep it, truncated,
    // so the failure is still diagnosable, but never render it as a title.
    return { ...fallback, detail: truncate(raw, 500) };
  }

  if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
    return { ...fallback, detail: truncate(raw, 500) };
  }

  const problem = parsed as ProblemDetails;

  return {
    ...problem,
    // The server always mirrors status into the body, but a gateway might not.
    status: typeof problem.status === 'number' ? problem.status : response.status,
    title: problem.title ?? fallback.title,
  };
}

function truncate(value: string, max: number): string {
  return value.length <= max ? value : `${value.slice(0, max)}...`;
}
