import { httpClient } from '../../shared/api/httpClient';
import type { LoginRequest, Session } from './types/session';

/**
 * The four F-2 endpoints, one function each
 * (planning-pms-verification.md, F-2 point 4).
 *
 * Every call goes through the shared `httpClient`, so a failure is always a typed
 * `ProblemDetailsError` and never a resolved promise carrying an error-shaped value (E-47).
 * Nothing here reads or writes web storage.
 */
export const authApi = {
  /** POST /api/auth/login - 200 with the session, 400 malformed, 401 wrong credential. */
  login: (request: LoginRequest): Promise<Session> =>
    httpClient.post<Session>('/auth/login', request),

  /** POST /api/auth/logout - 204. */
  logout: (): Promise<void> => httpClient.post<void>('/auth/logout'),

  /** GET /api/auth/session - 200 with the cookie, 401 without it. */
  getSession: (signal?: AbortSignal): Promise<Session> =>
    httpClient.get<Session>('/auth/session', { signal }),

  /**
   * POST /api/auth/reauth - the screen-lock overlay's endpoint.
   *
   * Called *without* navigating anywhere. That is the whole point: the consultation page
   * underneath the overlay stays mounted, so a half-typed draft survives the lock and the
   * re-authentication (E-41).
   */
  reauth: (request: LoginRequest): Promise<Session> =>
    httpClient.post<Session>('/auth/reauth', request),
};
