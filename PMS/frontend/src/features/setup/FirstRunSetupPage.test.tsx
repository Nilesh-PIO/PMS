import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SESSION_QUERY_KEY } from '../auth/useSession';
import {
  aClinicProfile,
  aSession,
  createTestQueryClient,
  jsonResponse,
  problemResponse,
  renderWithProviders,
  stubFetch,
} from '../../test/testUtils';
import { TemperatureUnit } from '../clinic/types/clinicProfile';
import { FirstRunSetupPage } from './FirstRunSetupPage';

/**
 * F-3, E-1: the screen that stands between a brand-new install and a prescription printed with
 * no clinic identity on it.
 */

function renderSetup(
  routes: Parameters<typeof stubFetch>[0],
  { setupComplete = false }: { setupComplete?: boolean } = {},
) {
  const stub = stubFetch(routes);
  const client = createTestQueryClient();
  client.setQueryData(SESSION_QUERY_KEY, aSession({ setupComplete }));
  return {
    ...renderWithProviders(<FirstRunSetupPage />, { client, route: '/setup' }),
    ...stub,
    client,
  };
}

describe('FirstRunSetupPage', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('offers a blank clinic form on a fresh database', async () => {
    renderSetup({ '/api/clinic-profile': () => problemResponse(404) });

    expect(await screen.findByRole('heading', { level: 1, name: 'First-run setup' }))
      .toBeInTheDocument();
    expect(screen.getByLabelText('Clinic name')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Save and continue' })).toBeInTheDocument();
  });

  it('renders without the app navigation chrome', async () => {
    // There is nothing useful to navigate to until the clinic has a name, and offering the
    // navigation would invite the physician to skip the one screen they cannot skip.
    renderSetup({ '/api/clinic-profile': () => problemResponse(404) });

    await screen.findByLabelText('Clinic name');
    expect(screen.queryByRole('navigation', { name: 'Main' })).toBeNull();
  });

  it('saves the four gate fields and reports the clinic as set up', async () => {
    const user = userEvent.setup();
    const { calls } = renderSetup({
      '/api/clinic-profile': (init?: RequestInit) =>
        init?.method === 'PUT'
          ? jsonResponse(aClinicProfile({ isSetupComplete: true }))
          : problemResponse(404),
    });

    await user.type(await screen.findByLabelText('Clinic name'), 'Sunrise Clinic');
    await user.type(screen.getByLabelText("Doctor's name"), 'Dr A. Mehta');
    await user.type(screen.getByLabelText('Registration number'), 'MMC-99215');
    await user.click(screen.getByRole('radio', { name: /Celsius/ }));
    await user.click(screen.getByRole('button', { name: 'Save and continue' }));

    await waitFor(() => {
      const put = calls.find((call) => call.init?.method === 'PUT');
      expect(put).toBeDefined();
      expect(JSON.parse(String(put!.init!.body))).toMatchObject({
        clinicName: 'Sunrise Clinic',
        doctorName: 'Dr A. Mehta',
        doctorRegistrationNo: 'MMC-99215',
        temperatureUnit: TemperatureUnit.Celsius,
      });
    });
  });

  it('re-reads the session after saving so the setup gate lifts', async () => {
    // The router guard reads session.setupComplete. Without this invalidation the physician would
    // save a complete profile and still be bounced back to /setup until the cache went stale.
    const user = userEvent.setup();
    const { calls } = renderSetup({
      '/api/clinic-profile': (init?: RequestInit) =>
        init?.method === 'PUT'
          ? jsonResponse(aClinicProfile({ isSetupComplete: true }))
          : problemResponse(404),
      '/api/auth/session': () => jsonResponse(aSession({ setupComplete: true })),
    });

    await user.type(await screen.findByLabelText('Clinic name'), 'Sunrise Clinic');
    await user.type(screen.getByLabelText("Doctor's name"), 'Dr A. Mehta');
    await user.type(screen.getByLabelText('Registration number'), 'MMC-99215');
    await user.click(screen.getByRole('radio', { name: /Celsius/ }));
    await user.click(screen.getByRole('button', { name: 'Save and continue' }));

    await waitFor(() => {
      expect(calls.some((call) => call.url.endsWith('/api/auth/session'))).toBe(true);
    });
  });

  it('stays put when the server reports the profile still incomplete', async () => {
    const user = userEvent.setup();
    renderSetup({
      '/api/clinic-profile': (init?: RequestInit) =>
        init?.method === 'PUT'
          ? jsonResponse(aClinicProfile({ isSetupComplete: false }))
          : problemResponse(404),
      '/api/auth/session': () => jsonResponse(aSession({ setupComplete: false })),
    });

    await user.type(await screen.findByLabelText('Clinic name'), 'Sunrise Clinic');
    await user.click(screen.getByRole('button', { name: 'Save and continue' }));

    // The server, not the form, decides when the gate opens.
    expect(
      await screen.findByRole('heading', { level: 1, name: 'First-run setup' }),
    ).toBeInTheDocument();
  });

  it('sends an already-configured clinic to the settings screen instead', async () => {
    renderSetup({ '/api/clinic-profile': () => jsonResponse(aClinicProfile()) }, {
      setupComplete: true,
    });

    // Typing /setup by hand on a running clinic should not hand back a first-run wizard.
    await waitFor(() => {
      expect(screen.queryByRole('heading', { level: 1, name: 'First-run setup' })).toBeNull();
    });
  });

  it('shows an error rather than a blank form when the profile cannot be loaded', async () => {
    renderSetup({ '/api/clinic-profile': () => problemResponse(500) });

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByLabelText('Clinic name')).toBeNull();
  });
});
