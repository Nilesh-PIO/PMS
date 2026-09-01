import { NETWORK_ERROR_STATUS, type ProblemDetails } from '../types/problemDetails';
import { ProblemDetailsError, parseProblemDetails } from './problemDetails';

/**
 * Every API call in the application goes through here. One place decides how a failure
 * becomes an error object, so no feature can accidentally swallow one (E-47).
 */

/** All routes are prefixed with /api and served same-origin by PMS.Api. */
export const API_BASE_PATH = '/api';

export interface RequestOptions extends Omit<RequestInit, 'body'> {
  /** Serialised as JSON unless it is already a FormData/Blob/string. */
  body?: unknown;
  /** Abort signal, forwarded to fetch. */
  signal?: AbortSignal;
}

/**
 * Issues a request against the API and returns the parsed JSON body.
 *
 * @param path Path relative to {@link API_BASE_PATH}, e.g. `/health` or `/patients/123`.
 * @throws ProblemDetailsError on any non-2xx response *and* on any transport failure.
 *         It never resolves with an error-shaped value, so a caller cannot mistake a
 *         failure for a success by forgetting to check a flag.
 */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { body, headers, ...rest } = options;

  const init: RequestInit = {
    ...rest,
    // Same-origin cookie auth (section 2). No token is ever read from or written to
    // localStorage/sessionStorage.
    credentials: 'same-origin',
    headers: buildHeaders(headers, body),
  };

  if (body !== undefined) {
    init.body = serialiseBody(body);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_PATH}${path}`, init);
  } catch (cause) {
    // fetch rejects only on a transport failure. Converting it here means every caller sees
    // one error type; a raw TypeError leaking into a component is how "nothing happened when
    // I clicked Save" happens.
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause;
    }

    const problem: ProblemDetails = {
      title: 'Could not reach the server.',
      detail:
        'The request did not reach the server, so nothing has been saved. Check the connection and try again.',
      status: NETWORK_ERROR_STATUS,
      instance: `${API_BASE_PATH}${path}`,
    };
    throw new ProblemDetailsError(problem, NETWORK_ERROR_STATUS);
  }

  if (!response.ok) {
    const problem = await parseProblemDetails(response);
    throw new ProblemDetailsError(problem, response.status);
  }

  return (await readSuccessBody<T>(response)) as T;
}

/** Convenience wrappers so feature API modules read as verbs. */
export const httpClient = {
  get: <T>(path: string, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'GET' }),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'PUT', body }),
  delete: <T>(path: string, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'DELETE' }),
};

function buildHeaders(headers: HeadersInit | undefined, body: unknown): Headers {
  const result = new Headers(headers);

  if (!result.has('Accept')) {
    result.set('Accept', 'application/json, application/problem+json');
  }

  const needsJsonContentType =
    body !== undefined &&
    !(body instanceof FormData) &&
    !(body instanceof Blob) &&
    !(body instanceof URLSearchParams) &&
    typeof body !== 'string';

  if (needsJsonContentType && !result.has('Content-Type')) {
    result.set('Content-Type', 'application/json');
  }

  return result;
}

function serialiseBody(body: unknown): BodyInit {
  if (
    body instanceof FormData ||
    body instanceof Blob ||
    body instanceof URLSearchParams ||
    typeof body === 'string'
  ) {
    return body;
  }
  return JSON.stringify(body);
}

async function readSuccessBody<T>(response: Response): Promise<T | undefined> {
  if (response.status === 204 || response.status === 205) {
    return undefined;
  }

  const text = await response.text();
  if (!text.trim()) {
    return undefined;
  }

  try {
    return JSON.parse(text) as T;
  } catch {
    // A 2xx that is not JSON is a server contract violation, not a user error. Surface it
    // rather than returning a half-parsed value.
    throw new ProblemDetailsError(
      {
        title: 'Unexpected response from the server.',
        detail: 'The server returned a successful status with a body that is not valid JSON.',
        status: response.status,
      },
      response.status,
    );
  }
}
