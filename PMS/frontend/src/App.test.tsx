import { QueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import { SESSION_QUERY_KEY } from './features/auth/useSession';
import { REGISTERED_PATHS, routes } from './routes';
import { aClinicProfile, aSession, jsonResponse, problemResponse, stubFetch } from './test/testUtils';

/**
 * F-1's route-table tests, updated by F-2 and F-3.
 *
 * From F-2 onward everything under `/` sits behind `RequireAuth`, so these tests seed a
 * session into the query cache. That is not a workaround: it is the state the physician is in
 * for every one of these screens, and the signed-out case is asserted separately below.
 *
 * F-3 adds a second guard, `RequireSetup`. `aSession()` now defaults to `setupComplete: true`
 * because that is the ordinary state of a working clinic; the first-run case is asserted
 * explicitly in its own describe block.
 */
function renderAt(
  path: string,
  { signedIn = true, setupComplete = true }: { signedIn?: boolean; setupComplete?: boolean } = {},
) {
  stubFetch({
    '/api/auth/session': () => problemResponse(401),
    '/api/clinic-profile': () => jsonResponse(aClinicProfile()),
  });

  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 60_000 } },
  });
  client.setQueryData(SESSION_QUERY_KEY, signedIn ? aSession({ setupComplete }) : null);

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
    ['/export', 'Export'],
    ['/audit', 'Audit log'],
  ])('registers %s and renders its placeholder', (path, heading) => {
    renderAt(path);

    expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument();
  });

  it('registers /settings/clinic and renders F-3s real clinic profile page', async () => {
    renderAt('/settings/clinic');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Clinic profile' }),
    ).toBeInTheDocument();
    expect(await screen.findByLabelText('Clinic name')).toBeInTheDocument();
  });

  it('renders /login without the app navigation chrome', () => {
    renderAt('/login', { signedIn: false });

    expect(screen.getByRole('heading', { level: 1, name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
  });

  it('renders /setup without the app navigation chrome', async () => {
    renderAt('/setup', { setupComplete: false });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'First-run setup' }),
    ).toBeInTheDocument();
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

describe('app shell first-run setup gate (F-3, E-1)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it.each(['/', '/patients', '/patients/abc-123', '/visits/abc-123', '/settings/clinic', '/export', '/audit'])(
    'sends a signed-in physician from %s to /setup while the clinic is unconfigured',
    async (path) => {
      renderAt(path, { setupComplete: false });

      // Acceptance criterion 1: with an empty ClinicProfile table every authenticated route
      // redirects to /setup, so a consultation can never be started that could not be printed.
      expect(
        await screen.findByRole('heading', { level: 1, name: 'First-run setup' }),
      ).toBeInTheDocument();
      expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
    },
  );

  it('lifts the redirect once the clinic profile is saved', () => {
    renderAt('/', { setupComplete: true });

    expect(screen.getByRole('heading', { level: 1, name: 'Today' })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument();
  });

  it('sends a signed-out visitor to /login rather than /setup', async () => {
    // Auth is asked first: setupComplete is a property of a session, and there is no session.
    renderAt('/', { signedIn: false, setupComplete: false });

    expect(screen.getByRole('heading', { level: 1, name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'First-run setup' })).toBeNull();
  });
});
