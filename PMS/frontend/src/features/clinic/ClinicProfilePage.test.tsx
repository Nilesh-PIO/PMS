import { QueryClient } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
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
import { ClinicProfilePage } from './ClinicProfilePage';
import { TemperatureUnit } from './types/clinicProfile';

/**
 * F-3 frontend unit tests (plan F-3 point 6): "required-field validation, unit selector".
 *
 * `fetch` is stubbed rather than the API module, so `httpClient`, `clinicApi` and the hooks all
 * run for real - a test that mocked `clinicApi` would pass even if the request shape were wrong.
 */

function renderPage(
  routes: Parameters<typeof stubFetch>[0],
  client: QueryClient = createTestQueryClient(),
) {
  const stub = stubFetch(routes);
  client.setQueryData(SESSION_QUERY_KEY, aSession());
  return { ...renderWithProviders(<ClinicProfilePage />, { client }), ...stub, client };
}

describe('ClinicProfilePage', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('loads the saved profile into the form', async () => {
    renderPage({ '/api/clinic-profile': () => jsonResponse(aClinicProfile()) });

    expect(await screen.findByLabelText('Clinic name')).toHaveValue('Sunrise Clinic');
    expect(screen.getByLabelText("Doctor's name")).toHaveValue('Dr A. Mehta');
    expect(screen.getByLabelText('Registration number')).toHaveValue('MMC-99215');
  });

  it('renders an empty form when no profile has been saved (404 is not an error)', async () => {
    renderPage({ '/api/clinic-profile': () => problemResponse(404) });

    expect(await screen.findByLabelText('Clinic name')).toHaveValue('');
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows an error rather than an empty form when the profile cannot be loaded', async () => {
    renderPage({ '/api/clinic-profile': () => problemResponse(500) });

    // "The server is down" must never render as "you have no clinic profile" - that would invite
    // the physician to retype one over a profile that still exists.
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByLabelText('Clinic name')).toBeNull();
  });

  // --- required-field validation (plan F-3 point 6) ----------------------

  it('renders the server field errors against the right inputs', async () => {
    const user = userEvent.setup();
    renderPage({
      '/api/clinic-profile': (init?: RequestInit) =>
        init?.method === 'PUT'
          ? problemResponse(400, {
              errors: {
                ClinicName: ['Enter the clinic name.'],
                DoctorName: ["Enter the doctor's name."],
                DoctorRegistrationNo: ["Enter the doctor's registration number."],
                TemperatureUnit: ['Choose the temperature unit this clinic uses.'],
              },
            })
          : jsonResponse(
              aClinicProfile({
                clinicName: '',
                doctorName: '',
                doctorRegistrationNo: '',
                temperatureUnit: TemperatureUnit.Unspecified,
                isSetupComplete: false,
              }),
            ),
    });

    await user.click(await screen.findByRole('button', { name: 'Save changes' }));

    expect(await screen.findByText('Enter the clinic name.')).toBeInTheDocument();
    expect(screen.getByText("Enter the doctor's name.")).toBeInTheDocument();
    expect(screen.getByText("Enter the doctor's registration number.")).toBeInTheDocument();
    expect(screen.getByText('Choose the temperature unit this clinic uses.')).toBeInTheDocument();

    expect(screen.getByLabelText('Clinic name')).toHaveAttribute('aria-invalid', 'true');
  });

  it('marks the four gate fields as required', async () => {
    renderPage({ '/api/clinic-profile': () => jsonResponse(aClinicProfile()) });

    expect(await screen.findByLabelText('Clinic name')).toBeRequired();
    expect(screen.getByLabelText("Doctor's name")).toBeRequired();
    expect(screen.getByLabelText('Registration number')).toBeRequired();
  });

  // --- unit selector (plan F-3 point 6, E-24) -----------------------------

  it('offers both temperature units and no third option', async () => {
    renderPage({ '/api/clinic-profile': () => jsonResponse(aClinicProfile()) });

    const radios = await screen.findAllByRole('radio');
    expect(radios).toHaveLength(2);
    expect(screen.getByRole('radio', { name: /Celsius/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: /Fahrenheit/ })).not.toBeChecked();
  });

  it('pre-selects nothing when the clinic has never chosen a unit', async () => {
    // E-24. A default would let the clinic pass this screen without anyone having answered, and
    // every temperature it ever records would carry a guess.
    renderPage({
      '/api/clinic-profile': () =>
        jsonResponse(
          aClinicProfile({ temperatureUnit: TemperatureUnit.Unspecified, isSetupComplete: false }),
        ),
    });

    for (const radio of await screen.findAllByRole('radio')) {
      expect(radio).not.toBeChecked();
    }
  });

  it('sends the chosen unit as its numeric enum value', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage({
      '/api/clinic-profile': (init?: RequestInit) =>
        init?.method === 'PUT'
          ? jsonResponse(aClinicProfile({ temperatureUnit: TemperatureUnit.Fahrenheit }))
          : jsonResponse(aClinicProfile()),
    });

    await user.click(await screen.findByRole('radio', { name: /Fahrenheit/ }));
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => {
      const put = calls.find((call) => call.init?.method === 'PUT');
      expect(put).toBeDefined();
      expect(JSON.parse(String(put!.init!.body))).toMatchObject({
        temperatureUnit: TemperatureUnit.Fahrenheit,
      });
    });
  });

  it('sends null rather than a guessed unit when none is chosen', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage({
      '/api/clinic-profile': (init?: RequestInit) =>
        init?.method === 'PUT'
          ? problemResponse(400, { errors: { TemperatureUnit: ['Choose the temperature unit.'] } })
          : jsonResponse(
              aClinicProfile({
                temperatureUnit: TemperatureUnit.Unspecified,
                isSetupComplete: false,
              }),
            ),
    });

    await user.click(await screen.findByRole('button', { name: 'Save changes' }));

    await waitFor(() => {
      const put = calls.find((call) => call.init?.method === 'PUT');
      expect(put).toBeDefined();
      expect(JSON.parse(String(put!.init!.body)).temperatureUnit).toBeNull();
    });
  });

  // --- signature (acceptance criterion 4) ---------------------------------

  it('shows a ruled signature area, not a broken image, when no signature is uploaded', async () => {
    renderPage({
      '/api/clinic-profile': () => jsonResponse(aClinicProfile({ signatureImageDataUrl: null })),
    });

    expect(await screen.findByTestId('signature-rule')).toBeInTheDocument();
    expect(screen.queryByRole('img', { name: 'Uploaded signature' })).toBeNull();
  });

  it('shows the uploaded signature when one exists', async () => {
    const dataUrl = 'data:image/png;base64,iVBORw0KGgo=';
    renderPage({
      '/api/clinic-profile': () =>
        jsonResponse(aClinicProfile({ signatureImageDataUrl: dataUrl })),
    });

    const image = await screen.findByRole('img', { name: 'Uploaded signature' });
    expect(image).toHaveAttribute('src', dataUrl);
    expect(screen.queryByTestId('signature-rule')).toBeNull();
  });

  it('rejects an oversize signature in the browser without uploading it', async () => {
    const user = userEvent.setup();
    const { calls } = renderPage({
      '/api/clinic-profile': () => jsonResponse(aClinicProfile()),
    });

    const tooBig = new File([new Uint8Array(200 * 1024 + 1)], 'signature.png', {
      type: 'image/png',
    });
    await user.upload(await screen.findByLabelText('Upload a PNG signature'), tooBig);

    expect(await screen.findByRole('alert')).toHaveTextContent(/200 KB or smaller/);
    expect(calls.some((call) => call.url.endsWith('/signature'))).toBe(false);
  });

  it('uploads an acceptable signature as multipart form data', async () => {
    const user = userEvent.setup();
    const dataUrl = 'data:image/png;base64,iVBORw0KGgo=';
    const { calls } = renderPage({
      '/api/clinic-profile/signature': () =>
        jsonResponse(aClinicProfile({ signatureImageDataUrl: dataUrl })),
      '/api/clinic-profile': () => jsonResponse(aClinicProfile()),
    });

    const png = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'signature.png', {
      type: 'image/png',
    });
    await user.upload(await screen.findByLabelText('Upload a PNG signature'), png);

    await waitFor(() => {
      const upload = calls.find((call) => call.url.endsWith('/clinic-profile/signature'));
      expect(upload).toBeDefined();
      expect(upload!.init?.body).toBeInstanceOf(FormData);
      // The browser must set the multipart boundary itself; setting Content-Type by hand
      // produces a request the server cannot parse.
      expect(new Headers(upload!.init?.headers).get('Content-Type')).toBeNull();
    });

    expect(await screen.findByRole('img', { name: 'Uploaded signature' })).toBeInTheDocument();
  });

  it('surfaces a 413 from the server rather than failing silently', async () => {
    const user = userEvent.setup();
    renderPage({
      '/api/clinic-profile/signature': () =>
        problemResponse(413, { title: 'The uploaded file is too large.' }),
      '/api/clinic-profile': () => jsonResponse(aClinicProfile()),
    });

    const png = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'signature.png', {
      type: 'image/png',
    });
    await user.upload(await screen.findByLabelText('Upload a PNG signature'), png);

    expect(await screen.findByRole('alert')).toHaveTextContent(/too large/i);
  });

  it('offers no signature upload until the clinic details have been saved', async () => {
    renderPage({ '/api/clinic-profile': () => problemResponse(404) });

    await screen.findByLabelText('Clinic name');
    expect(screen.queryByLabelText('Upload a PNG signature')).toBeNull();
    expect(screen.getByText(/Save the clinic details first/)).toBeInTheDocument();
  });

  // --- E-65: no browser autofill on a shared consulting-room PC -----------

  it('opts every field out of browser autofill', async () => {
    renderPage({ '/api/clinic-profile': () => jsonResponse(aClinicProfile()) });

    const clinicName = await screen.findByLabelText('Clinic name');
    expect(clinicName).toHaveAttribute('autocomplete', 'off');
    expect(screen.getByLabelText('Clinic address')).toHaveAttribute('autocomplete', 'off');
    expect(clinicName.closest('form')).toHaveAttribute('autocomplete', 'off');
  });
});
