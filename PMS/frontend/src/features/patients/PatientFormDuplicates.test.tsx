import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PatientForm } from './PatientForm';
import { jsonResponse, renderWithProviders, stubFetch } from '../../test/testUtils';
import type { DuplicateCandidate } from './types/patient';

/**
 * F-6's half of the registration screen — the duplicate warning, end to end through the real
 * `httpClient`, `patientsApi` and hooks (planning-pms-verification.md, F-6 point 4).
 *
 * Kept separate from `PatientForm.test.tsx` rather than appended to it: that file is F-5's suite and
 * names itself as such, and a reviewer asking "what did F-6 add to this screen?" should not have to
 * read forty F-5 assertions to find out.
 */

const navigate = vi.fn();
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigate };
});

const GENDER_OPTIONS = [
  { id: 2, category: 'Gender', value: 'Male', displayOrder: 2, isActive: true, isProtected: false },
];

function aCandidate(overrides: Partial<DuplicateCandidate> = {}): DuplicateCandidate {
  return {
    id: 'existing-1',
    fullName: 'Ravi Kumar',
    phoneTail: '3210',
    ageDisplay: '41',
    dateOfBirth: '1985-03-02',
    gender: 'Male',
    status: 'Active',
    mergedIntoPatientId: null,
    lastVisitDate: null,
    matchReason: 'phone',
    nameSimilarity: 1,
    isLikelyDuplicate: true,
    ...overrides,
  };
}

/** The RFC-7807 body the server sends on a duplicate 409, candidates and all. */
function duplicateConflict(candidates: DuplicateCandidate[]): Response {
  return new Response(
    JSON.stringify({
      status: 409,
      title: 'The request conflicts with a domain rule.',
      detail: 'A patient already on file looks like the same person.',
      ruleType: 'duplicate-confirmation-required',
      candidates,
    }),
    { status: 409, headers: { 'Content-Type': 'application/problem+json' } },
  );
}

interface FormStubs {
  /** Answers POST /api/patients (the unconfirmed path). */
  create?: () => Response;
  /** Answers POST /api/patients?confirmDuplicate=true. */
  confirmed?: () => Response;
  /** Answers POST /api/patients/duplicate-check. */
  check?: () => Response;
}

function renderForm({ create, confirmed, check }: FormStubs = {}) {
  // Route keys are matched by suffix, so the confirmed path must be registered under its full
  // query string - `/api/patients` would never match a URL ending in `?confirmDuplicate=true`.
  const stub = stubFetch({
    '/api/clinic-settings/options?category=Gender': () => jsonResponse(GENDER_OPTIONS),
    '/api/patients/duplicate-check': check ?? (() => jsonResponse([])),
    '/api/patients?confirmDuplicate=true':
      confirmed ?? (() => jsonResponse({ id: 'new-1', fullName: 'Ravi Kumar' }, 201)),
    '/api/patients': create ?? (() => jsonResponse({ id: 'new-1', fullName: 'Ravi Kumar' }, 201)),
  });

  const rendered = renderWithProviders(<PatientForm />);

  const postsTo = (suffix: string) =>
    stub.calls.filter((c) => c.url.endsWith(suffix) && c.init?.method === 'POST');

  return { ...rendered, ...stub, postsTo };
}

async function fillAndSubmit(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
  await user.type(screen.getByLabelText('Phone number'), '98765 43210');
  await user.click(screen.getByRole('button', { name: 'Register patient' }));
}

describe('PatientForm duplicate handling (F-6)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    navigate.mockReset();
  });

  it('opens the warning dialog when the server answers 409, and registers nobody', async () => {
    // Acceptance criterion 1 from the client's side. The 409 is a question, not a failure: the
    // candidates arrive with it, so the physician's next action needs no further round trip.
    const user = userEvent.setup();
    const { postsTo } = renderForm({ create: () => duplicateConflict([aCandidate()]) });

    await fillAndSubmit(user);

    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('This patient may already be registered')).toBeInTheDocument();
    expect(navigate).not.toHaveBeenCalled();
    expect(postsTo('confirmDuplicate=true')).toHaveLength(0);
  });

  it('does not render the 409 as an error message', async () => {
    // Showing "The request conflicts with a domain rule" underneath the dialog would tell the
    // physician something broke when nothing did.
    const user = userEvent.setup();
    renderForm({ create: () => duplicateConflict([aCandidate()]) });

    await fillAndSubmit(user);
    await screen.findByRole('dialog');

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('registers the patient when the warning is confirmed', async () => {
    // Acceptance criterion 2: the warning is dismissible and confirming creates the patient.
    const user = userEvent.setup();
    const { postsTo } = renderForm({ create: () => duplicateConflict([aCandidate()]) });

    await fillAndSubmit(user);
    await screen.findByRole('dialog');

    await user.click(screen.getByRole('button', { name: 'Register anyway' }));

    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/patients/new-1'));
    expect(postsTo('confirmDuplicate=true')).toHaveLength(1);
  });

  it('sends the same submission token on the confirmed attempt', async () => {
    // E-43/E-46. Both requests are one intent to register one patient. A fresh token on the
    // confirmation would mean a retry after a timeout could create two records - which is the exact
    // outcome this whole feature exists to prevent.
    const user = userEvent.setup();
    const { postsTo } = renderForm({ create: () => duplicateConflict([aCandidate()]) });

    await fillAndSubmit(user);
    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Register anyway' }));

    await waitFor(() => expect(postsTo('confirmDuplicate=true')).toHaveLength(1));

    const first = JSON.parse(String(postsTo('/api/patients')[0].init?.body));
    const confirmed = JSON.parse(String(postsTo('confirmDuplicate=true')[0].init?.body));

    expect(first.submissionId).toBeTruthy();
    expect(confirmed.submissionId).toBe(first.submissionId);
  });

  it('sends an identical payload on the confirmed attempt', async () => {
    // What is saved after a confirmation must be what the physician was warned about. If the two
    // payloads could differ, the record created would be one nobody reviewed.
    const user = userEvent.setup();
    const { postsTo } = renderForm({ create: () => duplicateConflict([aCandidate()]) });

    await fillAndSubmit(user);
    await screen.findByRole('dialog');
    await user.click(screen.getByRole('button', { name: 'Register anyway' }));

    await waitFor(() => expect(postsTo('confirmDuplicate=true')).toHaveLength(1));

    expect(JSON.parse(String(postsTo('confirmDuplicate=true')[0].init?.body)))
      .toEqual(JSON.parse(String(postsTo('/api/patients')[0].init?.body)));
  });

  it('closes the dialog and keeps every typed character when the warning is dismissed', async () => {
    // Going back must not cost the form. The physician's next action is to check a record and come
    // back, and a form that cleared itself would make them retype everything (E-47).
    const user = userEvent.setup();
    renderForm({ create: () => duplicateConflict([aCandidate()]) });

    await fillAndSubmit(user);
    await screen.findByRole('dialog');

    await user.click(screen.getByRole('button', { name: 'Go back and check' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByLabelText('Full name')).toHaveValue('Ravi Kumar');
    expect(screen.getByLabelText('Phone number')).toHaveValue('98765 43210');
  });

  it('registers straight away when nothing matches', async () => {
    // The ordinary case has to stay quiet, or the dialog becomes something to dismiss reflexively.
    const user = userEvent.setup();
    renderForm();

    await fillAndSubmit(user);

    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/patients/new-1'));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  // --- the advisory check while typing ------------------------------------

  it('warns before Save once a name and phone have been entered', async () => {
    // The check runs ahead of the physician, so the warning lands while they are still thinking
    // about who this patient is rather than after they have moved on.
    const user = userEvent.setup();
    renderForm({ check: () => jsonResponse([aCandidate()]) });

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.type(screen.getByLabelText('Phone number'), '98765 43210');

    expect(
      await screen.findByText(/A patient already on file looks like this person/),
    ).toBeInTheDocument();
  });

  it('does not ask the server until there is something to match on', async () => {
    // The identity rule needs a name plus a phone or a date of birth. Asking with only a name is a
    // round trip that can only come back empty.
    const user = userEvent.setup();
    const { postsTo } = renderForm();

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');

    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(postsTo('/api/patients/duplicate-check')).toHaveLength(0);
  });

  it('debounces the check rather than asking on every keystroke', async () => {
    const user = userEvent.setup();
    const { postsTo } = renderForm({ check: () => jsonResponse([]) });

    await user.type(screen.getByLabelText('Full name'), 'Ravi Kumar');
    await user.type(screen.getByLabelText('Phone number'), '9876543210');

    await waitFor(() => expect(postsTo('/api/patients/duplicate-check').length).toBeGreaterThan(0));

    // Twenty-one keystrokes; nothing close to twenty-one requests.
    expect(postsTo('/api/patients/duplicate-check').length).toBeLessThan(5);
  });

  it('still lets the patient be registered when the advisory check fails', async () => {
    // The check is an early warning, not the guarantee - the server runs it again before writing.
    // A check that could jam the form would be a worse failure than the duplicate it prevents.
    const user = userEvent.setup();
    renderForm({
      check: () => new Response('{}', { status: 500 }),
    });

    await fillAndSubmit(user);

    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/patients/new-1'));
  });
});
