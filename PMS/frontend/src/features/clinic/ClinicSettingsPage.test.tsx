import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  aSession,
  createTestQueryClient,
  jsonResponse,
  problemResponse,
  renderWithProviders,
  stubFetch,
} from '../../test/testUtils';
import { SESSION_QUERY_KEY } from '../auth/useSession';
import { ClinicSettingsPage } from './ClinicSettingsPage';
import type { SettingOption } from './types/clinicSettings';

/**
 * F-4 frontend unit tests for the list editor (plan F-4 point 4 names the page; point 5 states the
 * rule it has to make visible - an option is retired, never deleted).
 */

const GENDER_GET = '/api/clinic-settings/options?category=Gender&includeInactive=true';
const REASON_GET = '/api/clinic-settings/options?category=VitalsNotRecordedReason&includeInactive=true';
const GENDER_PUT = '/api/clinic-settings/options/Gender';

function anOption(
  id: number,
  value: string,
  displayOrder: number,
  overrides: Partial<SettingOption> = {},
): SettingOption {
  return {
    id,
    category: 'Gender',
    value,
    displayOrder,
    isActive: true,
    isProtected: false,
    ...overrides,
  };
}

function theSeededGenderList(): SettingOption[] {
  return [
    anOption(1, 'Female', 1),
    anOption(2, 'Male', 2),
    anOption(3, 'Other', 3),
    anOption(4, 'Not stated', 4, { isProtected: true }),
  ];
}

function theSeededReasonList(): SettingOption[] {
  return [
    anOption(5, 'Equipment unavailable', 1, { category: 'VitalsNotRecordedReason' }),
    anOption(6, 'Patient declined', 2, { category: 'VitalsNotRecordedReason' }),
    anOption(7, 'Not clinically indicated', 3, { category: 'VitalsNotRecordedReason' }),
    anOption(8, 'Other', 4, { category: 'VitalsNotRecordedReason' }),
  ];
}

function renderPage(
  routes: Parameters<typeof stubFetch>[0] = {
    [GENDER_GET]: () => jsonResponse(theSeededGenderList()),
    [REASON_GET]: () => jsonResponse(theSeededReasonList()),
    [GENDER_PUT]: () => jsonResponse(theSeededGenderList()),
  },
) {
  const stub = stubFetch(routes);
  const client = createTestQueryClient();
  client.setQueryData(SESSION_QUERY_KEY, aSession());
  return { ...renderWithProviders(<ClinicSettingsPage />, { client }), ...stub };
}

/**
 * The gender editor once its data has actually arrived.
 *
 * Deliberately keyed off the labelled `region`, not the `<h2>`: the loading and error branches
 * render that heading too, so `findByRole('heading')` resolves while the list is still empty and
 * every assertion after it races. (The first draft of this file did exactly that and failed.)
 */
function genderSection() {
  return screen.findByRole('region', { name: 'Gender options' });
}

function lastPutBody(calls: { url: string; init?: RequestInit }[]) {
  const put = [...calls].reverse().find((call) => call.init?.method === 'PUT');
  expect(put, 'expected the page to have sent a PUT').toBeDefined();
  return JSON.parse(String(put!.init!.body));
}

describe('ClinicSettingsPage', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders both configured lists in display order', async () => {
    renderPage();

    const gender = await genderSection();
    expect(screen.getByRole('heading', { name: 'Reasons a vital was not recorded' })).toBeInTheDocument();

    expect(within(gender).getByLabelText('Option 1')).toHaveValue('Female');
    expect(within(gender).getByLabelText('Option 4')).toHaveValue('Not stated');
  });

  it('asks for inactive options too, so a retired one can be brought back', async () => {
    const { calls } = renderPage();

    await genderSection();
    expect(calls.some((call) => call.url.includes('includeInactive=true'))).toBe(true);
  });

  // --- the protected option (E-23) ---------------------------------------

  it('will not let "Not stated" be retired, and says why', async () => {
    renderPage();

    const gender = await genderSection();

    expect(within(gender).getByLabelText('Offer "Not stated"')).toBeDisabled();
    expect(within(gender).getByLabelText('Option 4')).toHaveAttribute('readonly');
    expect(within(gender).getByText(/Always offered/)).toBeInTheDocument();
    expect(within(gender).getByLabelText('Offer "Male"')).toBeEnabled();
  });

  // --- retire, never delete (plan F-4 point 5) ---------------------------

  it('offers no delete control anywhere, and explains the alternative', async () => {
    renderPage();

    await genderSection();

    expect(screen.queryByRole('button', { name: /delete/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /remove/i })).toBeNull();
    expect(screen.getAllByText(/Nothing here is ever deleted/).length).toBeGreaterThan(0);
  });

  it('sends a cleared "Offered" box as isActive false rather than dropping the row', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    const gender = await genderSection();

    await user.click(within(gender).getByLabelText('Offer "Other"'));
    await user.click(within(gender).getByRole('button', { name: 'Save gender options' }));

    await waitFor(() => expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true));

    const body = lastPutBody(calls);
    expect(body.items).toHaveLength(4);
    expect(body.items[2]).toEqual({ value: 'Other', isActive: false });
    expect(calls.some((c) => c.init?.method === 'DELETE')).toBe(false);
  });

  // --- reordering ---------------------------------------------------------

  it('sends the reordered list in the order shown on screen', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    const gender = await genderSection();

    await user.click(within(gender).getByRole('button', { name: 'Move "Male" up' }));
    await user.click(within(gender).getByRole('button', { name: 'Save gender options' }));

    await waitFor(() => expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true));

    const body = lastPutBody(calls);
    expect(body.items.map((i: { value: string }) => i.value)).toEqual([
      'Male',
      'Female',
      'Other',
      'Not stated',
    ]);
  });

  it('disables the reorder controls at the ends of the list', async () => {
    renderPage();

    const gender = await genderSection();

    expect(within(gender).getByRole('button', { name: 'Move "Female" up' })).toBeDisabled();
    expect(within(gender).getByRole('button', { name: 'Move "Not stated" down' })).toBeDisabled();
  });

  // --- adding ------------------------------------------------------------

  it('adds a typed option to the end of the list and submits it', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    const gender = await genderSection();

    await user.type(within(gender).getByLabelText(/Add an option/), 'Non-binary');
    await user.click(within(gender).getByRole('button', { name: 'Add' }));
    await user.click(within(gender).getByRole('button', { name: 'Save gender options' }));

    await waitFor(() => expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true));

    const body = lastPutBody(calls);
    expect(body.items.map((i: { value: string }) => i.value)).toContain('Non-binary');
  });

  it('catches a case-insensitive duplicate before it is ever sent (C-20)', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    const gender = await genderSection();

    await user.type(within(gender).getByLabelText(/Add an option/), 'male');
    await user.click(within(gender).getByRole('button', { name: 'Add' }));

    expect(await within(gender).findByText('"male" is already in this list.')).toBeInTheDocument();
    expect(calls.some((c) => c.init?.method === 'PUT')).toBe(false);
  });

  it('refuses to add an empty option', async () => {
    const user = userEvent.setup();
    renderPage();

    const gender = await genderSection();

    await user.click(within(gender).getByRole('button', { name: 'Add' }));

    expect(await within(gender).findByText('Type the option first.')).toBeInTheDocument();
  });

  // --- errors -------------------------------------------------------------

  it('renders a server field error against the row it belongs to', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage({
      [GENDER_GET]: () => jsonResponse(theSeededGenderList()),
      [REASON_GET]: () => jsonResponse(theSeededReasonList()),
      [GENDER_PUT]: () =>
        problemResponse(400, {
          errors: { 'Items[1].Value': ['"male" is already in the list at position 1.'] },
        }),
    });

    const gender = await genderSection();
    await user.click(within(gender).getByRole('button', { name: 'Save gender options' }));

    expect(await within(gender).findByText('"male" is already in the list at position 1.'))
      .toBeInTheDocument();
    expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true);
  });

  it('renders the list-level error when the server refuses the whole edit', async () => {
    const user = userEvent.setup();
    renderPage({
      [GENDER_GET]: () => jsonResponse(theSeededGenderList()),
      [REASON_GET]: () => jsonResponse(theSeededReasonList()),
      [GENDER_PUT]: () =>
        problemResponse(400, {
          errors: { Items: ['"Not stated" must stay in the list and stay active.'] },
        }),
    });

    const gender = await genderSection();
    await user.click(within(gender).getByRole('button', { name: 'Save gender options' }));

    expect(await within(gender).findByRole('alert')).toHaveTextContent(
      '"Not stated" must stay in the list and stay active.',
    );
  });

  it('shows an error rather than an empty editor when a list cannot be loaded', async () => {
    renderPage({
      [GENDER_GET]: () => problemResponse(500),
      [REASON_GET]: () => jsonResponse(theSeededReasonList()),
    });

    // An empty editor would read as "you have no gender options", and the physician would start
    // retyping a list that still exists on the server.
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByLabelText('Offer "Male"')).toBeNull();
  });
});
