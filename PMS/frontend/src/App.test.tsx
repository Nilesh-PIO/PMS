import { QueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { SESSION_QUERY_KEY } from './features/auth/useSession';
import { REGISTERED_PATHS, routes } from './routes';
import { aSession, problemResponse, stubFetch } from './test/testUtils';

/**
 * F-1's route-table tests, updated by F-2.
 *
 * From F-2 onward everything under `/` sits behind `RequireAuth`, so these tests seed a
 * session into the query cache. That is not a workaround: it is the state the physician is in
 * for every one of these screens, and the signed-out case is asserted separately below.
 */
function renderAt(path: string, { signedIn = true }: { signedIn?: boolean } = {}) {
  stubFetch({ '/api/auth/session': () => problemResponse(401) });

  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 60_000 } },
  });
  client.setQueryData(SESSION_QUERY_KEY, signedIn ? aSession() : null);

  return render(<App client={client} initialEntries={[path]} />);
}

describe('app shell routing', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders the layout chrome on an authenticated route', () => {
    renderAt('/');

    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Patients' })).toBeInTheDocument();
  });

  it.each([
    ['/', 'Today'],
    ['/patients', 'Patients'],
    ['/patients/abc-123', 'Patient profile'],
    ['/visits/abc-123', 'Consultation'],
    ['/settings/clinic', 'Clinic profile'],
    ['/export', 'Export'],
    ['/audit', 'Audit log'],
  ])('registers %s and renders its placeholder', (path, heading) => {
    renderAt(path);

    expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
  });

  it('renders /login without the app navigation chrome', () => {
    renderAt('/login', { signedIn: false });

    expect(screen.getByRole('heading', { level: 1, name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
  });

  it('renders /setup without the app navigation chrome', () => {
    renderAt('/setup');

    expect(screen.getByRole('heading', { level: 1, name: 'First-run setup' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
  });

  it('shows a not-found page instead of a blank screen for an unknown route', () => {
    renderAt('/nothing-here');

    expect(screen.getByRole('heading', { level: 1, name: 'Page not found' })).toBeInTheDocument();
  });

  it('declares every path the F-1 plan names', () => {
    const declared = new Set<string>();
    const walk = (list: typeof routes, prefix = '') => {
      for (const route of list) {
        const path = route.index
          ? prefix || '/'
          : `${prefix === '/' ? '' : prefix}/${route.path ?? ''}`.replace(/\/+/g, '/');
        if (route.path?.startsWith('/')) {
          declared.add(route.path);
        } else if (route.path || route.index) {
          declared.add(path.replace(/\/$/, '') || '/');
        }
        if (route.children) {
          walk(route.children, route.path?.startsWith('/') ? route.path : path);
        }
      }
    };
    walk(routes);

    for (const path of REGISTERED_PATHS) {
      expect(declared).toContain(path);
    }
  });
});

describe('app shell route guard (F-2)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it.each(['/', '/patients', '/patients/abc-123', '/visits/abc-123', '/settings/clinic', '/export', '/audit'])(
    'sends a signed-out visitor from %s to the login screen',
    (path) => {
      renderAt(path, { signedIn: false });

      expect(screen.getByRole('heading', { level: 1, name: /sign in/i })).toBeInTheDocument();
      expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
    },
  );

  it('shows the signed-in user name and a sign-out control in the layout', () => {
    renderAt('/');

    expect(screen.getByText('doctor')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Sign out' })).toBeInTheDocument();
  });
});
