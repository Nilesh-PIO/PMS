import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PatientForm } from './PatientForm';
import { jsonResponse, problemResponse, renderWithProviders, stubFetch } from '../../test/testUtils';

/**
 * F-5 frontend unit tests (plan F-5 point 6): "mononym accepted (E-13), no-phone accepted with
 * prompt (E-20), approx-age path".
 */

const navigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigate };
});

const GENDER_OPTIONS = [
  { id: 1, category: 'Gender', value: 'Female', displayOrder: 1, isActive: true, isProtected: false },
  { id: 2, category: 'Gender', value: 'Male', displayOrder: 2, isActive: true, isProtected: false },
  { id: 4, category: 'Gender', value: 'Not stated', displayOrder: 4, isActive: true, isProtected: true },
];

/** Stubs the two calls this screen makes and captures what was POSTed. */
function renderForm(
  createResponse: (init?: RequestInit) => Response | Promise<Response> = () =>
    jsonResponse({ id: 'new-1', fullName: 'Ravi Kumar' }, 201),
) {
  const stub = stubFetch({
    '/api/clinic-settings/options?category=Gender': () => jsonResponse(GENDER_OPTIONS),
    '/api/patients': createResponse,
  });

  const rendered = renderWithProviders(<PatientForm />);

  const bodies = () =>
    stub.calls
      .filter((c) => c.url.endsWith('/api/patients') && c.init?.method === 'POST')
      .map((c) => JSON.parse(String(c.init?.body)));

  return { ...rendered, ...stub, bodies };
}

describe('PatientForm (F-5)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    navigate.mockReset();
  });

  // --- the name field (C-18, E-13) ---------------------------------------

  it('accepts a single-word name', async () => {
    // E-13. A required-surname design rejects real patients.
    const user = userEvent.setup();
    const { bodies } = renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Meera');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(1));
    expect(bodies()[0].fullName).toBe('Meera');
  });

  it('does not submit a blank name', async () => {
    const user = userEvent.setup();
    const { bodies } = renderForm();

    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    expect(await screen.findByText("Enter the patient's name.")).toBeInTheDocument();
    expect(bodies()).toHaveLength(0);
  });

  it('offers no surname field at all', () => {
    // The strongest form of this assertion: not "surname is optional" but "there is no such box".
    renderForm();

    expect(screen.queryByLabelText(/surname|last name|family name/i)).toBeNull();
  });

  // --- no phone (Q-7, E-8, E-20) ------------------------------------------

  it('accepts a patient with no phone and warns that the profile will be incomplete', async () => {
    // E-20. Allowed, and E-8: said out loud rather than discovered later.
    const user = userEvent.setup();
    const { bodies } = renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Meera');

    expect(screen.getByText(/saved and marked/i)).toBeInTheDocument();
    expect(screen.getByText('incomplete')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(1));
    expect(bodies()[0].primaryPhone).toBeNull();
  });

  it('stops warning about an incomplete profile once phone, gender and age are given', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.type(screen.getByLabelText('Phone number'), '98765 43210');
    await user.selectOptions(screen.getByLabelText('Gender'), 'Male');
    await user.click(screen.getByLabelText('Date of birth is known'));
    await user.type(screen.getByLabelText('Date of birth'), '1985-03-02');

    expect(screen.queryByText(/saved and marked/i)).toBeNull();
  });

  it('sends the phone exactly as typed rather than reformatting it', async () => {
    // E-59. Normalising is the server's job and it adds a column; it never rewrites the entry.
    const user = userEvent.setup();
    const { bodies } = renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.type(screen.getByLabelText('Phone number'), '+91 98765-43210');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(1));
    expect(bodies()[0].primaryPhone).toBe('+91 98765-43210');
  });

  // --- the age path (E-9, E-21) -------------------------------------------

  it('sends an approximate age with no date of birth, and says it is stored as an estimate', async () => {
    const user = userEvent.setup();
    const { bodies } = renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.click(screen.getByLabelText('Only an approximate age is known'));

    // E-21, made visible before the physician commits to it.
    expect(screen.getByText(/~40 \(recorded 2026\)/)).toBeInTheDocument();

    await user.type(screen.getByLabelText('Approximate age in years'), '40');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(1));
    expect(bodies()[0].approxAgeYears).toBe(40);
    expect(bodies()[0].dateOfBirth).toBeNull();
  });

  it('never lets a date of birth and an approximate age be sent together', async () => {
    // E-9. The mutual exclusion is structural - choosing one hides the other's input entirely,
    // so the invalid combination cannot be expressed rather than being validated after the fact.
    const user = userEvent.setup();
    const { bodies } = renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.click(screen.getByLabelText('Only an approximate age is known'));
    await user.type(screen.getByLabelText('Approximate age in years'), '40');

    expect(screen.queryByLabelText('Date of birth')).toBeNull();

    await user.click(screen.getByLabelText('Date of birth is known'));
    expect(screen.queryByLabelText('Approximate age in years')).toBeNull();

    await user.type(screen.getByLabelText('Date of birth'), '1985-03-02');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(1));
    expect(bodies()[0].dateOfBirth).toBe('1985-03-02');
    expect(bodies()[0].approxAgeYears).toBeNull();
  });

  it('sends no age at all when the age is not known', async () => {
    const user = userEvent.setup();
    const { bodies } = renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Meera');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(1));
    expect(bodies()[0].dateOfBirth).toBeNull();
    expect(bodies()[0].approxAgeYears).toBeNull();
  });

  // --- gender comes from F-4's list (C-20) --------------------------------

  it('renders the clinics active gender options in display order, plus a not-answered choice', async () => {
    renderForm();

    const select = await screen.findByLabelText('Gender');
    await waitFor(() =>
      expect([...select.querySelectorAll('option')].map((o) => o.textContent)).toEqual([
        'Not answered',
        'Female',
        'Male',
        'Not stated',
      ]),
    );
  });

  it('offers no way to type a gender that is not on the list', async () => {
    // C-20. A <select> rather than a text input is what stops "M", "Male" and "male" coexisting.
    renderForm();

    const select = await screen.findByLabelText('Gender');
    expect(select.tagName).toBe('SELECT');
  });

  // --- double submit (E-43, E-46) -----------------------------------------

  it('sends one request for a double-click, with a submission token', async () => {
    // Acceptance criterion 6, client half. The token is the part that matters - the disabled
    // button alone does not survive a retry or a refresh.
    const user = userEvent.setup();
    let resolve: ((r: Response) => void) | undefined;
    const { bodies } = renderForm(
      () => new Promise<Response>((r) => {
        resolve = r;
      }),
    );

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    const button = screen.getByRole('button', { name: 'Register patient' });

    await user.click(button);
    await user.click(button);
    await user.click(button);

    expect(bodies()).toHaveLength(1);
    expect(bodies()[0].submissionId).toEqual(expect.any(String));
    expect(bodies()[0].submissionId).not.toHaveLength(0);

    resolve?.(jsonResponse({ id: 'new-1', fullName: 'Ravi Kumar' }, 201));
    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/patients/new-1'));
  });

  it('disables the save button while the request is in flight', async () => {
    const user = userEvent.setup();
    let resolve: ((r: Response) => void) | undefined;
    renderForm(
      () => new Promise<Response>((r) => {
        resolve = r;
      }),
    );

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    expect(await screen.findByRole('button', { name: 'Saving...' })).toBeDisabled();

    resolve?.(jsonResponse({ id: 'new-1', fullName: 'Ravi Kumar' }, 201));
    await waitFor(() => expect(navigate).toHaveBeenCalled());
  });

  // --- failure keeps the physician's work (E-47) --------------------------

  it('keeps every typed value on screen when the save fails, and allows a retry', async () => {
    // A form that clears itself on failure is how typed work gets lost. The retry re-sends the
    // *same* submission token, so a save that actually succeeded before the connection dropped
    // cannot produce a second patient.
    const user = userEvent.setup();
    let failNext = true;
    const { bodies } = renderForm(() =>
      failNext
        ? problemResponse(500, { detail: 'Something went wrong.' })
        : jsonResponse({ id: 'new-1', fullName: 'Ravi Kumar' }, 201),
    );

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.type(screen.getByLabelText('Phone number'), '98765 43210');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(1));
    expect(screen.getByLabelText('Full name')).toHaveValue('Ravi Kumar');
    expect(screen.getByLabelText('Phone number')).toHaveValue('98765 43210');
    expect(navigate).not.toHaveBeenCalled();

    failNext = false;
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    await waitFor(() => expect(bodies()).toHaveLength(2));
    expect(bodies()[1].submissionId).toBe(bodies()[0].submissionId);
  });

  it('renders a servers field errors against the right inputs', async () => {
    const user = userEvent.setup();
    renderForm(() =>
      problemResponse(400, {
        errors: { DateOfBirth: ['A date of birth cannot be in the future.'] },
      }),
    );

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.click(screen.getByLabelText('Date of birth is known'));
    await user.type(screen.getByLabelText('Date of birth'), '2030-01-01');
    await user.click(screen.getByRole('button', { name: 'Register patient' }));

    expect(
      await screen.findByText('A date of birth cannot be in the future.'),
    ).toBeInTheDocument();
  });

  // --- E-65, inherited from F-2's shared convention -----------------------

  it('turns browser autofill off on the form and on every patient-data input', () => {
    // The consulting-room PC is shared across every patient of the day; a remembered value is a
    // PHI disclosure with no bug behind it.
    const { container } = renderForm();

    expect(container.querySelector('form')).toHaveAttribute('autocomplete', 'off');
    for (const input of container.querySelectorAll('input[type="text"], input[type="date"]')) {
      expect(input).toHaveAttribute('autocomplete', 'off');
    }
  });

  it('puts nothing in web storage', () => {
    renderForm();

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });
});
