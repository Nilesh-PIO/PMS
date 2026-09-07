import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PatientList } from './PatientList';
import { SEARCH_DEBOUNCE_MS } from './usePatientSearch';
import { jsonResponse, renderWithProviders, stubFetch } from '../../test/testUtils';
import type { PatientSummary } from './types/patient';

/**
 * The `/patients?query=` page (F-7 point 4).
 *
 * The behaviour worth testing here is the one the header box does not have: the query lives in the
 * URL, so a refresh does not discard it and the search can be bookmarked or shared.
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
    matchKind: 'Name',
    ...overrides,
  };
}

async function runDebounce() {
  await act(async () => {
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS + 10);
  });
}

describe('PatientList', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it('runs the search in the URL on arrival, so a refresh keeps it', async () => {
    const { calls } = stubFetch({ '/patients/search': () => jsonResponse([aSummary()]) });

    renderWithProviders(<PatientList />, { route: '/patients?query=ravi' });
    await runDebounce();

    await waitFor(() => {
      const searches = calls.filter((c) => c.url.includes('/patients/search'));
      expect(searches).toHaveLength(1);
      expect(searches[0].url).toContain('query=ravi');
    });

    expect(await screen.findByTestId('patient-picker-row')).toBeInTheDocument();
  });

  it('carries the include-inactive toggle in the URL too', async () => {
    const { calls } = stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientList />, {
      route: '/patients?query=ravi&includeInactive=true',
    });
    await runDebounce();

    await waitFor(() => {
      expect(calls.at(-1)!.url).toContain('includeInactive=true');
    });
    expect(screen.getByLabelText(/Include retired and merged/i)).toBeChecked();
  });

  it('asks for more rows than the header box, because this page has room for them', async () => {
    const { calls } = stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientList />, { route: '/patients?query=kumar' });
    await runDebounce();

    await waitFor(() => {
      expect(calls.at(-1)!.url).toContain('take=50');
    });
  });

  it('shows the register action when the search finds nobody (E-7)', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientList />, { route: '/patients?query=Zenobia' });
    await runDebounce();

    expect(await screen.findByText(/No patient found/i)).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: /Register "Zenobia" as a new patient/i }),
    ).toHaveAttribute('href', '/patients/new?name=Zenobia');
  });

  it('prompts rather than searching when the box is empty', async () => {
    const { calls } = stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientList />, { route: '/patients' });
    await runDebounce();

    expect(calls.filter((c) => c.url.includes('/patients/search'))).toHaveLength(0);
    expect(screen.getByText(/Type at least two characters/i)).toBeInTheDocument();
  });

  it('updates the query as the physician types', async () => {
    const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
    const { calls } = stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientList />, { route: '/patients' });

    await user.type(screen.getByRole('searchbox'), 'sunita');
    await runDebounce();

    await waitFor(() => {
      expect(calls.at(-1)!.url).toContain('query=sunita');
    });
  });
});
