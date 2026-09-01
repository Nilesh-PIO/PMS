import { screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { Route, Routes } from 'react-router-dom';
import { RequireAuth } from './RequireAuth';
import { SESSION_QUERY_KEY } from '../../features/auth/useSession';
import {
  aSession,
  createTestQueryClient,
  jsonResponse,
  problemResponse,
  renderWithProviders,
  stubFetch,
} from '../../test/testUtils';

function Guarded() {
  return <h1>Patient record</h1>;
}

function LoginStand() {
  return <h1>Sign in</h1>;
}

function renderGuard(routes: Parameters<typeof stubFetch>[0], seed?: 'session' | 'signedOut') {
  const stub = stubFetch(routes);
  const client = createTestQueryClient();

  if (seed === 'session') {
    client.setQueryData(SESSION_QUERY_KEY, aSession());
  } else if (seed === 'signedOut') {
    client.setQueryData(SESSION_QUERY_KEY, null);
  }

  const rendered = renderWithProviders(
    <Routes>
      <Route
        path="/patients/1"
        element={
          <RequireAuth>
            <Guarded />
          </RequireAuth>
        }
      />
      <Route path="/login" element={<LoginStand />} />
    </Routes>,
    { client, route: '/patients/1' },
  );

  return { ...rendered, ...stub };
}

describe('RequireAuth', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders the guarded screen when there is a session', () => {
    renderGuard({ '/api/auth/session': () => jsonResponse(aSession()) }, 'session');

    expect(screen.getByRole('heading', { name: 'Patient record' })).toBeInTheDocument();
  });

  it('sends a signed-out visitor to the login screen', () => {
    renderGuard({ '/api/auth/session': () => problemResponse(401) }, 'signedOut');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Patient record' })).toBeNull();
  });

  it('redirects on a 401 from the server, not only on a seeded cache', async () => {
    renderGuard({ '/api/auth/session': () => problemResponse(401) });

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('shows a checking state rather than flashing the guarded screen', () => {
    renderGuard({ '/api/auth/session': () => jsonResponse(aSession()) });

    expect(screen.getByRole('status')).toHaveTextContent(/checking your session/i);
    expect(screen.queryByRole('heading', { name: 'Patient record' })).toBeNull();
  });

  it('renders the guarded screen once the session request resolves', async () => {
    renderGuard({ '/api/auth/session': () => jsonResponse(aSession()) });

    expect(await screen.findByRole('heading', { name: 'Patient record' })).toBeInTheDocument();
  });

  it('does not claim the physician is signed out when the server is unreachable', async () => {
    // "Cannot reach the server" and "you are signed out" are different problems with different
    // fixes. Redirecting to a login screen that cannot work would hide the real one.
    renderGuard({
      '/api/auth/session': () => {
        throw new TypeError('Failed to fetch');
      },
    });

    // A longer wait than the other cases on purpose: a transport failure is the one error the
    // shared query client retries once (a 4xx/5xx is not retried), so the error state only
    // appears after that second attempt and its backoff.
    const alert = await screen.findByRole('alert', {}, { timeout: 5000 });
    expect(alert.textContent).toMatch(/could not reach the server/i);
    expect(screen.queryByRole('heading', { name: 'Sign in' })).toBeNull();
  }, 10_000);

  it('does not render the guarded screen on a server error', async () => {
    renderGuard({ '/api/auth/session': () => problemResponse(500) });

    await screen.findByRole('alert');
    expect(screen.queryByRole('heading', { name: 'Patient record' })).toBeNull();
  });
});
