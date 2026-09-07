import { screen, waitFor } from '@testing-library/react';
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
import { VitalRangesPage } from './VitalRangesPage';
import type { VitalRange } from './types/clinicSettings';

/**
 * F-4 frontend unit tests (plan F-4 point 6): "blank threshold submits as null, not zero".
 *
 * `fetch` is stubbed rather than the API module, so `httpClient`, `clinicApi` and the hooks all run
 * for real - a test that mocked `clinicApi` would pass even if the request body were wrong, which
 * is exactly the defect this file exists to catch.
 */

const RANGES_PATH = '/api/clinic-settings/vital-ranges';

function anUnconfiguredRangeList(): VitalRange[] {
  return [
    { metric: 'Temperature', label: 'Temperature', unit: '°C', warnLow: null, warnHigh: null, updatedUtc: null },
    {
      metric: 'BloodPressureSystolic',
      label: 'Blood pressure (systolic)',
      unit: 'mmHg',
      warnLow: null,
      warnHigh: null,
      updatedUtc: null,
    },
    {
      metric: 'BloodPressureDiastolic',
      label: 'Blood pressure (diastolic)',
      unit: 'mmHg',
      warnLow: null,
      warnHigh: null,
      updatedUtc: null,
    },
    { metric: 'PulseBpm', label: 'Pulse', unit: 'bpm', warnLow: null, warnHigh: null, updatedUtc: null },
  ];
}

function renderPage(ranges: VitalRange[] = anUnconfiguredRangeList()) {
  const stub = stubFetch({
    [RANGES_PATH]: (init) =>
      init?.method === 'PUT' ? jsonResponse(ranges) : jsonResponse(ranges),
  });

  const client = createTestQueryClient();
  client.setQueryData(SESSION_QUERY_KEY, aSession());

  return { ...renderWithProviders(<VitalRangesPage />, { client }), ...stub };
}

/** The body of the last PUT the page sent. */
function lastPutBody(calls: { url: string; init?: RequestInit }[]) {
  const put = [...calls].reverse().find((call) => call.init?.method === 'PUT');
  expect(put, 'expected the page to have sent a PUT').toBeDefined();
  return JSON.parse(String(put!.init!.body));
}

describe('VitalRangesPage', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders every metric with its unit, unconfigured', async () => {
    renderPage();

    expect(await screen.findByRole('heading', { name: 'Temperature (°C)' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Blood pressure (systolic) (mmHg)' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Blood pressure (diastolic) (mmHg)' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Pulse (bpm)' })).toBeInTheDocument();

    expect(screen.getByLabelText('Temperature lower limit')).toHaveValue('');
    expect(screen.getAllByText(/No warning set/)).toHaveLength(4);
  });

  it('says out loud that the ranges are the physician’s own, not the software’s', async () => {
    // Not decoration. A doctor who assumes the application already knows what a dangerous pulse is
    // would be relying on something that does not exist (plan section 7, C-31).
    renderPage();

    expect(await screen.findByText(/These are your numbers/)).toBeInTheDocument();
    expect(screen.getByText(/ships with no ranges at all/)).toBeInTheDocument();
  });

  // --- the plan's named test ---------------------------------------------

  it('submits a blank threshold as null, not zero', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    await screen.findByLabelText('Pulse upper limit');
    await user.type(screen.getByLabelText('Pulse upper limit'), '120');
    await user.click(screen.getByRole('button', { name: 'Save vital ranges' }));

    await waitFor(() => expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true));

    const body = lastPutBody(calls);
    const pulse = body.items.find((i: { metric: string }) => i.metric === 'PulseBpm');

    expect(pulse.warnHigh).toBe(120);
    // The assertion this whole test file exists for. `Number('')` is 0 in JavaScript, and a
    // threshold of 0 would warn on every reading the clinic ever enters - the exact opposite of
    // "leave me alone about this one".
    expect(pulse.warnLow).toBeNull();
    expect(pulse.warnLow).not.toBe(0);
  });

  it('submits every untouched metric as null too, rather than omitting or zeroing it', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    await screen.findByLabelText('Pulse upper limit');
    await user.type(screen.getByLabelText('Pulse upper limit'), '120');
    await user.click(screen.getByRole('button', { name: 'Save vital ranges' }));

    await waitFor(() => expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true));

    const body = lastPutBody(calls);
    expect(body.items).toHaveLength(4);
    for (const item of body.items.filter((i: { metric: string }) => i.metric !== 'PulseBpm')) {
      expect(item.warnLow).toBeNull();
      expect(item.warnHigh).toBeNull();
    }
  });

  it('sends a decimal threshold unchanged', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    await screen.findByLabelText('Temperature upper limit');
    await user.type(screen.getByLabelText('Temperature upper limit'), '37.5');
    await user.click(screen.getByRole('button', { name: 'Save vital ranges' }));

    await waitFor(() => expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true));

    const body = lastPutBody(calls);
    expect(body.items.find((i: { metric: string }) => i.metric === 'Temperature').warnHigh).toBe(37.5);
  });

  it('accepts a threshold of zero as a real threshold', async () => {
    // The mirror image of the blank case: if the page treated 0 as "unset", a physician who
    // deliberately set a floor of zero would silently get no warnings at all.
    const user = userEvent.setup();
    const { calls } = renderPage();

    await screen.findByLabelText('Blood pressure (diastolic) lower limit');
    await user.type(screen.getByLabelText('Blood pressure (diastolic) lower limit'), '0');
    await user.click(screen.getByRole('button', { name: 'Save vital ranges' }));

    await waitFor(() => expect(calls.some((c) => c.init?.method === 'PUT')).toBe(true));

    const body = lastPutBody(calls);
    const diastolic = body.items.find(
      (i: { metric: string }) => i.metric === 'BloodPressureDiastolic',
    );
    expect(diastolic.warnLow).toBe(0);
    expect(diastolic.warnLow).not.toBeNull();
  });

  // --- client-side guards -------------------------------------------------

  it('refuses an inverted range without sending it', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    await screen.findByLabelText('Pulse lower limit');
    await user.type(screen.getByLabelText('Pulse lower limit'), '120');
    await user.type(screen.getByLabelText('Pulse upper limit'), '40');
    await user.click(screen.getByRole('button', { name: 'Save vital ranges' }));

    expect(await screen.findByText('The lower limit must not be above the upper limit.'))
      .toBeInTheDocument();
    expect(calls.some((c) => c.init?.method === 'PUT')).toBe(false);
  });

  it('refuses a non-numeric threshold without sending it', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage();

    await screen.findByLabelText('Temperature upper limit');
    await user.type(screen.getByLabelText('Temperature upper limit'), 'hot');
    await user.click(screen.getByRole('button', { name: 'Save vital ranges' }));

    expect(await screen.findByText('Enter a number, or leave it blank for no warning.'))
      .toBeInTheDocument();
    expect(calls.some((c) => c.init?.method === 'PUT')).toBe(false);
  });

  // --- reading back -------------------------------------------------------

  it('loads configured thresholds into the inputs and leaves blank ones empty', async () => {
    const configured = anUnconfiguredRangeList();
    configured[0] = { ...configured[0], warnLow: null, warnHigh: 42, updatedUtc: '2026-09-04T09:00:00Z' };

    renderPage(configured);

    expect(await screen.findByLabelText('Temperature upper limit')).toHaveValue('42');
    expect(screen.getByLabelText('Temperature lower limit')).toHaveValue('');
    expect(screen.getAllByText(/No warning set/)).toHaveLength(3);
    expect(screen.getByText(/asks for confirmation/)).toBeInTheDocument();
  });

  it('shows an error rather than an empty form when the ranges cannot be loaded', async () => {
    stubFetch({ [RANGES_PATH]: () => problemResponse(500) });
    const client = createTestQueryClient();
    client.setQueryData(SESSION_QUERY_KEY, aSession());
    renderWithProviders(<VitalRangesPage />, { client });

    // "The server is down" must never render as "you have no ranges configured" - that would
    // invite the physician to retype thresholds over ones that still exist.
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByLabelText('Temperature upper limit')).toBeNull();
  });

  it('renders a server field error against the row it belongs to', async () => {
    const user = userEvent.setup();
    stubFetch({
      [RANGES_PATH]: (init) =>
        init?.method === 'PUT'
          ? problemResponse(400, {
              errors: { 'Items[3].WarnHigh': ['Enter a number below 10000.'] },
            })
          : jsonResponse(anUnconfiguredRangeList()),
    });
    const client = createTestQueryClient();
    client.setQueryData(SESSION_QUERY_KEY, aSession());
    renderWithProviders(<VitalRangesPage />, { client });

    await screen.findByLabelText('Pulse upper limit');
    await user.type(screen.getByLabelText('Pulse upper limit'), '99');
    await user.click(screen.getByRole('button', { name: 'Save vital ranges' }));

    expect(await screen.findByText('Enter a number below 10000.')).toBeInTheDocument();
  });
});
