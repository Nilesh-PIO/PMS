import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SESSION_QUERY_KEY } from '../../features/auth/useSession';
import {
  aSession,
  createTestQueryClient,
  problemResponse,
  renderWithProviders,
  stubFetch,
} from '../../test/testUtils';
import { RequireSetup } from './RequireSetup';

/**
 * F-3, acceptance criterion 1: with an empty ClinicProfile table every authenticated route
 * redirects to /setup (E-1).
 */

function renderGuard(session: ReturnType<typeof aSession> | null, route = '/') {
  stubFetch({ '/api/auth/session': () => problemResponse(401) });

  const client = createTestQueryClient();
  client.setQueryData(SESSION_QUERY_KEY, session);

  return renderWithProviders(
    <Routes>
      <Route
        path="/"
        element={
          <RequireSetup>
            <h1>Today</h1>
          </RequireSetup>
        }
      />
      <Route
        path="/patients"
        element={
          <RequireSetup>
            <h1>Patients</h1>
          </RequireSetup>
        }
      />
      <Route path="/setup" element={<h1>First-run setup</h1>} />
    </Routes>,
    { client, route },
  );
}

describe('RequireSetup', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('redirects to /setup when the clinic profile has never been saved', () => {
    renderGuard(aSession({ setupComplete: false }));

    expect(screen.getByRole('heading', { level: 1, name: 'First-run setup' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Today' })).toBeNull();
  });

  it.each(['/', '/patients'])('redirects from %s, not just the home route', (route) => {
    renderGuard(aSession({ setupComplete: false }), route);

    expect(screen.getByRole('heading', { level: 1, name: 'First-run setup' })).toBeInTheDocument();
  });

  it('renders the route once setup is complete', () => {
    renderGuard(aSession({ setupComplete: true }));

    expect(screen.getByRole('heading', { level: 1, name: 'Today' })).toBeInTheDocument();
  });

  it('does not redirect a signed-out visitor to /setup', () => {
    // setupComplete is a property of the session, so there is no answer to give someone who has
    // none. RequireAuth owns that case; sending them to /setup would hide the login screen behind
    // a form they cannot submit.
    renderGuard(null);

    expect(screen.queryByRole('heading', { name: 'First-run setup' })).toBeNull();
    expect(screen.getByRole('heading', { level: 1, name: 'Today' })).toBeInTheDocument();
  });
});
