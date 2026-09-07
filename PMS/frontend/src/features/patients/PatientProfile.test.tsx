import { screen } from '@testing-library/react';
import { Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PatientProfile } from './PatientProfile';
import {
  aPatientDetail,
  jsonResponse,
  problemResponse,
  renderWithProviders,
  stubFetch,
} from '../../test/testUtils';
import type { PatientDetail } from './types/patient';

/**
 * F-5's profile screen (plan F-5 point 4; brainstorm REC-12, E-8, E-20).
 *
 * The plan's named E2E case — "register with name only and confirm the incomplete flag is visible
 * on the profile" — is asserted here as well as in the (unrunnable) Playwright spec, so the
 * behaviour is actually proven by a suite that executes on this host.
 */
function renderProfile(patient: Partial<PatientDetail> = {}) {
  const detail = aPatientDetail(patient);

  stubFetch({
    [`/api/patients/${detail.id}`]: () => jsonResponse(detail),
  });

  return renderWithProviders(
    <Routes>
      <Route path="/patients/:id" element={<PatientProfile />} />
    </Routes>,
    { route: `/patients/${detail.id}` },
  );
}

describe('PatientProfile (F-5)', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  // --- REC-12: never a name on its own ------------------------------------

  it('shows name, phone and age together in the header', async () => {
    // RSK-12 is rated Critical: the clinic has several Ravi Kumars, and a name-only header gives
    // no way to notice the wrong history has been opened before prescribing against it.
    renderProfile();

    expect(await screen.findByRole('heading', { level: 1, name: 'Ravi Kumar' })).toBeInTheDocument();
    expect(screen.getByText('+91 98765-43210')).toBeInTheDocument();
    expect(screen.getByText('41')).toBeInTheDocument();
    expect(screen.getByText('Male')).toBeInTheDocument();
  });

  it('renders an explicit placeholder for each identifying value that is missing', async () => {
    // A missing value must look missing, not look like a rendering glitch - and never collapse
    // the header into a name on its own.
    renderProfile({
      primaryPhone: null,
      phoneTail: null,
      gender: null,
      dateOfBirth: null,
      ageDisplay: 'Age not recorded',
      isProfileIncomplete: true,
      missingFields: ['phone', 'age', 'gender'],
    });

    expect(await screen.findByText('No contact recorded')).toBeInTheDocument();
    expect(screen.getByText('Age not recorded')).toBeInTheDocument();
    expect(screen.getByText('Not answered')).toBeInTheDocument();
  });

  // --- E-8 / E-20: the incomplete flag ------------------------------------

  it('shows the incomplete flag and names each missing detail', async () => {
    // Acceptance criterion 1's display half. "Profile incomplete" alone is a nag; naming the
    // gaps is something the physician can act on in ten seconds.
    renderProfile({
      primaryPhone: null,
      phoneTail: null,
      gender: null,
      dateOfBirth: null,
      ageDisplay: 'Age not recorded',
      isProfileIncomplete: true,
      missingFields: ['phone', 'age', 'gender'],
    });

    expect(await screen.findByRole('heading', { name: 'Profile incomplete' })).toBeInTheDocument();
    expect(screen.getByText('phone number')).toBeInTheDocument();
    expect(screen.getByText('age or date of birth')).toBeInTheDocument();
    expect(screen.getByText('gender')).toBeInTheDocument();
  });

  it('says the patient can still be seen, so the flag never reads as a block', async () => {
    // E-8's mitigation is "flag it", never "prevent it". The wording matters: a physician who
    // reads this as a blocker will invent a phone number to clear it.
    renderProfile({
      primaryPhone: null,
      isProfileIncomplete: true,
      missingFields: ['phone'],
    });

    expect(await screen.findByText(/can be seen and prescribed for as normal/i)).toBeInTheDocument();
  });

  it('shows no incomplete section for a fully recorded profile', async () => {
    renderProfile();

    await screen.findByRole('heading', { level: 1, name: 'Ravi Kumar' });
    expect(screen.queryByRole('heading', { name: 'Profile incomplete' })).toBeNull();
  });

  // --- the age never masquerades as exact (E-21) --------------------------

  it('renders an approximate age with the year it was recorded', async () => {
    renderProfile({
      dateOfBirth: null,
      approxAgeYears: 40,
      ageRecordedOn: '2026-09-07',
      ageDisplay: '~40 (recorded 2026)',
    });

    expect(await screen.findByText('~40 (recorded 2026)')).toBeInTheDocument();
  });

  it('renders a newborns age in days rather than as zero', async () => {
    // E-11. "0" is exactly what a paediatric dosing judgement must not be told.
    renderProfile({ dateOfBirth: '2026-09-07', ageDisplay: '0 days' });

    expect(await screen.findByText('0 days')).toBeInTheDocument();
  });

  // --- non-Latin names (E-57) ---------------------------------------------

  it('renders a non-Latin name unchanged', async () => {
    renderProfile({ fullName: 'रवि कुमार' });

    expect(
      await screen.findByRole('heading', { level: 1, name: 'रवि कुमार' }),
    ).toBeInTheDocument();
  });

  // --- failure states ------------------------------------------------------

  it('reports an unknown patient rather than rendering an empty profile', async () => {
    // An empty profile would look like a patient with no details - which is a real state - so a
    // 404 has to be reported as a different thing entirely.
    stubFetch({ '/api/patients/nope': () => problemResponse(404) });

    renderWithProviders(
      <Routes>
        <Route path="/patients/:id" element={<PatientProfile />} />
      </Routes>,
      { route: '/patients/nope' },
    );

    expect(await screen.findByRole('alert')).toHaveTextContent(/no patient was found/i);
  });

  it('reports a server failure as a failure and not as an empty record', async () => {
    stubFetch({ '/api/patients/abc-123': () => problemResponse(500) });

    renderWithProviders(
      <Routes>
        <Route path="/patients/:id" element={<PatientProfile />} />
      </Routes>,
      { route: '/patients/abc-123' },
    );

    const alert = await screen.findByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Profile incomplete' })).toBeNull();
  });

  // --- F-8's state, rendered correctly when it arrives ---------------------

  it('marks an inactive record as inactive', async () => {
    renderProfile({ status: 'Inactive', inactiveReason: 'Moved away' });

    expect(await screen.findByText(/this record is inactive/i)).toBeInTheDocument();
    expect(screen.getByText(/moved away/i)).toBeInTheDocument();
  });
});
