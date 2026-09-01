import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { authApi } from './authApi';
import type { LoginRequest, Session } from './types/session';

/** Query key for the current session. Exported so tests and other features can invalidate it. */
export const SESSION_QUERY_KEY = ['auth', 'session'] as const;

/**
 * The current session, as the server sees it
 * (planning-pms-verification.md, F-2 point 4: TanStack Query, `staleTime: 60_000`).
 *
 * A 401 is a legitimate answer - "you are signed out" - not a failure to report as an error
 * page, so it resolves to `null` rather than throwing. Any other failure still throws, because
 * "the server is unreachable" must not be silently rendered as "you are signed out".
 */
export function useSession(): UseQueryResult<Session | null> {
  return useQuery<Session | null>({
    queryKey: SESSION_QUERY_KEY,
    queryFn: async ({ signal }) => {
      try {
        return await authApi.getSession(signal);
      } catch (error) {
        if (isProblemDetailsError(error) && error.status === 401) {
          return null;
        }
        throw error;
      }
    },
    staleTime: 60_000,
    // Retrying a 401 is pointless and delays the login screen. A network failure is still
    // retried once by the shared client default.
    retry: (failureCount, error) =>
      isProblemDetailsError(error) && !error.isNetworkError ? false : failureCount < 1,
  });
}

/** Signs in from the login page and primes the session cache with the response. */
export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: LoginRequest) => authApi.login(request),
    onSuccess: (session) => {
      queryClient.setQueryData(SESSION_QUERY_KEY, session);
    },
  });
}

/**
 * Re-authenticates from the screen-lock overlay.
 *
 * Deliberately identical in shape to {@link useLogin} except that it never clears any other
 * cache entry: clearing would discard the consultation the physician is in the middle of,
 * which is precisely the loss E-41 describes.
 */
export function useReauth() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: LoginRequest) => authApi.reauth(request),
    onSuccess: (session) => {
      queryClient.setQueryData(SESSION_QUERY_KEY, session);
    },
  });
}

/** Signs out and drops the cached session. */
export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => authApi.logout(),
    onSuccess: () => {
      queryClient.setQueryData(SESSION_QUERY_KEY, null);
      // Everything else in the cache is PHI for a patient this browser is no longer entitled
      // to show. Dropping it on sign-out is the cache half of E-62.
      queryClient.clear();
    },
  });
}
