import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { RouterProvider, createBrowserRouter, createMemoryRouter } from 'react-router-dom';
import { ErrorBoundary } from './shared/components/ErrorBoundary';
import { queryClient as defaultQueryClient } from './shared/api/queryClient';
import { routes } from './routes';

export interface AppProps {
  /** Overridden in tests so each test gets an isolated cache. */
  client?: QueryClient;
  /**
   * Overridden in tests to use a memory router at a given path. jsdom has no real history,
   * and a browser router in a test asserts the environment rather than the app.
   */
  initialEntries?: string[];
}

/**
 * Opt in to the v7 behaviours now, while the route table is nine placeholders, rather than
 * after fifteen features depend on the v6 defaults.
 */
const ROUTER_FUTURE = {
  v7_relativeSplatPath: true,
  v7_fetcherPersist: true,
  v7_normalizeFormMethod: true,
  v7_partialHydration: true,
  v7_skipActionErrorRevalidation: true,
} as const;

/** `v7_startTransition` is a RouterProvider option, not a router option. */
const PROVIDER_FUTURE = { v7_startTransition: true } as const;

export function App({ client = defaultQueryClient, initialEntries }: AppProps) {
  const router = initialEntries
    ? createMemoryRouter(routes, { initialEntries, future: ROUTER_FUTURE })
    : createBrowserRouter(routes, { future: ROUTER_FUTURE });

  return (
    <ErrorBoundary>
      <QueryClientProvider client={client}>
        <RouterProvider router={router} future={PROVIDER_FUTURE} />
      </QueryClientProvider>
    </ErrorBoundary>
  );
}
