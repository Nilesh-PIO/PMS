import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import { RecentPatients } from './RecentPatients';
import { jsonResponse, problemResponse, renderWithProviders, stubFetch } from '../../test/testUtils';
import type { PatientSummary } from './types/patient';

/**
 * F-7 frontend unit tests (plan F-7 point 6): "`RecentPatients.test.tsx` (empty state)".
 *
 * The empty state is acceptance criterion 5 and E-2's mitigation, so it gets more than one
 * assertion: it must be empty-stated, it must offer registration as the action, and — the part that
 * actually matters — a *failed load* must not be rendered as an empty clinic.
 */

function aSummary(overrides: Partial<PatientSummary> = {}): PatientSummary {
  return {
    id: 'patient-1',
    fullName: 'Ravi Kumar',
    phoneTail: '3210',
    ageDisplay: '41',
    gender: 'Male',
    lastVisitDate: null,
    registeredOn: '2026-09-07',
    status: 'Active',
    isMerged: false,
    isProfileIncomplete: false,
    matchKind: 'Recent',
    ...overrides,
  };
}

describe('RecentPatients', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders an empty state with a register action on a fresh install (E-2, AC-5)', async () => {
    stubFetch({ '/patients/recent': () => jsonResponse([]) });

    renderWithProviders(<RecentPatients />);

    // Not a blank panel: a named state, and exactly one obvious next action.
    expect(await screen.findByText(/No patients registered yet/i)).toBeInTheDocument();

    const action = screen.getByRole('link', { name: /Register the first patient/i });
    expect(action).toHaveAttribute('href', '/patients/new');
  });

  it('renders recent patients as full picker rows, not as a list of names (E-28)', async () => {
    stubFetch({
      '/patients/recent': () =>
        jsonResponse([
          aSummary({ id: 'a', fullName: 'Ravi Kumar', phoneTail: '3210' }),
          aSummary({ id: 'b', fullName: 'Sunita Devi', phoneTail: '6789' }),
        ]),
    });

    renderWithProviders(<RecentPatients />);

    const rows = await screen.findAllByTestId('patient-picker-row');
    expect(rows).toHaveLength(2);

    // The same guarantee the search results give - phone tail and age on every row.
    expect(within(rows[0]).getByText(/3210/)).toBeInTheDocument();
    expect(within(rows[0]).getByText('41')).toBeInTheDocument();
    expect(within(rows[1]).getByText(/6789/)).toBeInTheDocument();
  });

  it('asks the server for the recent list exactly once', async () => {
    const { calls } = stubFetch({ '/patients/recent': () => jsonResponse([]) });

    renderWithProviders(<RecentPatients />);
    await screen.findByText(/No patients registered yet/i);

    await waitFor(() => {
      expect(calls.filter((c) => c.url.includes('/patients/recent'))).toHaveLength(1);
    });
  });

  it('shows a failed load as an error, never as an empty clinic', async () => {
    // The whole point of E-2's empty state is that it means "you have no patients". If a 500
    // rendered the same way, the physician would be told the clinic is empty because the network
    // hiccuped - and the obvious next action offered would be to register someone who exists.
    stubFetch({
      '/patients/recent': () => problemResponse(500, { detail: 'Database is unavailable.' }),
    });

    renderWithProviders(<RecentPatients />);

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByText(/No patients registered yet/i)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Try again/i })).toBeInTheDocument();
  });

  it('mentions the keyboard shortcut once there is something to search past', async () => {
    stubFetch({ '/patients/recent': () => jsonResponse([aSummary()]) });

    renderWithProviders(<RecentPatients />);

    await screen.findByTestId('patient-picker-row');
    expect(screen.getByText(/to search by name or phone number/i)).toBeInTheDocument();
  });

  it('puts nothing in web storage', async () => {
    stubFetch({ '/patients/recent': () => jsonResponse([aSummary()]) });

    renderWithProviders(<RecentPatients />);
    await screen.findByTestId('patient-picker-row');

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});
