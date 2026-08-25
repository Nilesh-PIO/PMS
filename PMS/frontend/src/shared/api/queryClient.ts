import { QueryClient } from '@tanstack/react-query';
import { isProblemDetailsError } from './problemDetails';

/**
 * Server state is managed with TanStack Query v5 throughout - no mixed fetching strategies
 * (planning-pms-verification.md, section 2 "Frontend structure"). Plain component state is
 * used only for uncommitted form input.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Per the F-1 plan: retry: 1, refetchOnWindowFocus: false.
        retry: (failureCount, error) => {
          // Never retry a request the server actively rejected - a 400/401/409 will fail
          // identically every time and retrying just delays the error the user must see.
          if (isProblemDetailsError(error) && !error.isNetworkError) {
            return false;
          }
          return failureCount < 1;
        },
        refetchOnWindowFocus: false,
        staleTime: 30_000,
      },
      mutations: {
        // A write is never retried automatically: a retried POST can create a second patient
        // or a second visit. Retrying is the user's explicit decision (E-46).
        retry: false,
      },
    },
  });
}

/** The client instance used by the running app. Tests build their own. */
export const queryClient = createQueryClient();
