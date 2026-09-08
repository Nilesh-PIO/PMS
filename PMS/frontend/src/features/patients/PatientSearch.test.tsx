import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PatientSearch } from './PatientSearch';
import { SEARCH_DEBOUNCE_MS } from './usePatientSearch';
import { jsonResponse, problemResponse, renderWithProviders, stubFetch } from '../../test/testUtils';
import type { PatientSummary } from './types/patient';

/**
 * F-7 frontend unit tests (plan F-7 point 6): "`PatientSearch.test.tsx` (debounce, `/` focus)".
 *
 * Both named cases are here, plus the ones the plan's acceptance criteria depend on the client for:
 * E-7's inline register action carrying the typed text, E-28's no-auto-selection rule, and the
 * fuzzy result being labelled as a guess.
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

/** Advances past the debounce inside `act`, so React flushes the state update it schedules. */
async function runDebounce() {
  await act(async () => {
    vi.advanceTimersByTime(SEARCH_DEBOUNCE_MS + 10);
  });
}

describe('PatientSearch', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  const user = () => userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

  // --- debounce (plan F-7 point 6) --------------------------------------

  it('does not issue a request for every keystroke', async () => {
    const { calls } = stubFetch({
      '/patients/search': () => jsonResponse([aSummary()]),
    });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'ravi');

    // Four characters typed, nothing sent yet - the debounce timer has not elapsed.
    expect(calls.filter((c) => c.url.includes('/patients/search'))).toHaveLength(0);

    await runDebounce();

    await waitFor(() => {
      const searches = calls.filter((c) => c.url.includes('/patients/search'));
      expect(searches).toHaveLength(1);
      expect(searches[0].url).toContain('query=ravi');
    });
  });

  it('debounces at the plan-specified 300 ms', () => {
    // Pinned so the number is a decision rather than a habit (C-35).
    expect(SEARCH_DEBOUNCE_MS).toBe(300);
  });

  it('never sends a query shorter than two characters', async () => {
    const { calls } = stubFetch({
      '/patients/search': () => jsonResponse([]),
    });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'r');
    await runDebounce();

    expect(calls.filter((c) => c.url.includes('/patients/search'))).toHaveLength(0);
    expect(screen.getByText(/at least two characters/i)).toBeInTheDocument();
  });

  // --- "/" focus (REC-16, plan F-7 point 6) -----------------------------

  it('focuses the search box when "/" is pressed anywhere in the application', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(
      <>
        <button type="button">Somewhere else</button>
        <PatientSearch />
      </>,
    );

    const input = screen.getByRole('combobox');
    expect(input).not.toHaveFocus();

    screen.getByRole('button', { name: 'Somewhere else' }).focus();
    await act(async () => {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: '/', bubbles: true }));
    });

    expect(input).toHaveFocus();
  });

  it('does not steal "/" from someone typing into a field', async () => {
    // A consultation note is full of "/" characters. Teleporting the caret out of it mid-sentence
    // would be worse than having no shortcut at all.
    stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(
      <>
        <textarea aria-label="Consultation note" defaultValue="" />
        <PatientSearch />
      </>,
    );

    const note = screen.getByLabelText('Consultation note');
    note.focus();

    await act(async () => {
      note.dispatchEvent(new KeyboardEvent('keydown', { key: '/', bubbles: true }));
    });

    expect(note).toHaveFocus();
    expect(screen.getByRole('combobox')).not.toHaveFocus();
  });

  // --- results, and what must never happen to them ----------------------

  it('renders a result row with every disambiguating field (E-28)', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([aSummary()]) });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'ravi');
    await runDebounce();

    const row = await screen.findByTestId('patient-picker-row');
    expect(within(row).getByText('Ravi Kumar')).toBeInTheDocument();
    expect(within(row).getByText(/3210/)).toBeInTheDocument();
    expect(within(row).getByText('41')).toBeInTheDocument();
  });

  it('does not auto-select even when exactly one patient matches (RSK-12)', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([aSummary()]) });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'ravi');
    await runDebounce();

    // The single match is rendered as a choice to make, and the input still holds what was typed -
    // nothing navigated on its own.
    await screen.findByTestId('patient-picker-row');
    expect(screen.getByRole('combobox')).toHaveValue('ravi');
  });

  it('labels a fuzzy result as a guess and warns before it is chosen (E-30)', async () => {
    stubFetch({
      '/patients/search': () =>
        jsonResponse([aSummary({ fullName: 'Ravi Kumr', matchKind: 'SimilarName' })]),
    });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'ravi kumar');
    await runDebounce();

    // Two separate warnings, deliberately: a banner over the whole list, and a badge on each row.
    // The banner is missed by someone who scans straight to the names, so the row carries it too.
    expect(await screen.findByText(/No exact match/i)).toBeInTheDocument();

    const row = screen.getByTestId('patient-picker-row');
    expect(within(row).getByText(/Similar name/i)).toBeInTheDocument();
  });

  // --- the empty result (E-7, acceptance criterion 3) -------------------

  it('offers to register the typed name when nothing is found', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'Zenobia');
    await runDebounce();

    expect(await screen.findByText(/No patient found/i)).toBeInTheDocument();

    const register = screen.getByRole('link', { name: /Register "Zenobia" as a new patient/i });
    // The typed text is carried into the form, so nobody retypes it - retyping is how the second
    // spelling of one person's name gets into the database.
    expect(register).toHaveAttribute('href', '/patients/new?name=Zenobia');
  });

  it('prefills the phone field, not the name, when the query was digits', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), '9876543210');
    await runDebounce();

    const register = await screen.findByRole('link', { name: /Register "9876543210"/i });
    expect(register).toHaveAttribute('href', '/patients/new?phone=9876543210');
  });

  it('still offers registration when there are matches, because none may be the right person', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([aSummary()]) });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'Ravi');
    await runDebounce();

    await screen.findByTestId('patient-picker-row');
    expect(screen.getByText(/Not one of these\?/i)).toBeInTheDocument();
  });

  // --- failure is never reported as "not found" -------------------------

  it('reports a failed search as a failure and never as "no patient found"', async () => {
    // The dangerous confusion in this feature: "the server broke" rendered as "this person does not
    // exist" is what makes someone register a duplicate (E-47 meeting E-30).
    stubFetch({
      '/patients/search': () => problemResponse(500, { detail: 'Something went wrong.' }),
    });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'ravi');
    await runDebounce();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByText(/The patient may well exist/i)).toBeInTheDocument();
    expect(screen.queryByText(/No patient found/i)).not.toBeInTheDocument();
  });

  // --- the include-inactive toggle --------------------------------------

  it('asks the server for retired records only when the toggle is on', async () => {
    const { calls } = stubFetch({ '/patients/search': () => jsonResponse([]) });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'ravi');
    await runDebounce();

    await waitFor(() => {
      expect(calls.some((c) => c.url.includes('/patients/search'))).toBe(true);
    });
    expect(calls.at(-1)!.url).not.toContain('includeInactive');

    await user().click(screen.getByLabelText(/Include retired and merged/i));
    await runDebounce();

    await waitFor(() => {
      expect(calls.at(-1)!.url).toContain('includeInactive=true');
    });
  });

  // --- no PHI in web storage (F-2's standing rule) ----------------------

  it('puts nothing in web storage', async () => {
    stubFetch({ '/patients/search': () => jsonResponse([aSummary()]) });

    renderWithProviders(<PatientSearch />);
    await user().type(screen.getByRole('combobox'), 'ravi');
    await runDebounce();
    await screen.findByTestId('patient-picker-row');

    // Patient names are the most sensitive thing this screen holds; none of it is persisted.
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});
