import type { RouteObject } from 'react-router-dom';
import { LoginPage } from './features/auth/LoginPage';
import { ClinicProfilePage } from './features/clinic/ClinicProfilePage';
import { ClinicSettingsPage } from './features/clinic/ClinicSettingsPage';
import { VitalRangesPage } from './features/clinic/VitalRangesPage';
import { PatientForm } from './features/patients/PatientForm';
import { PatientList } from './features/patients/PatientList';
import { PatientProfile } from './features/patients/PatientProfile';
import { RecentPatients } from './features/patients/RecentPatients';
import { FirstRunSetupPage } from './features/setup/FirstRunSetupPage';
import { AppLayout } from './shared/components/AppLayout';
import { PlaceholderPage } from './shared/components/PlaceholderPage';
import { RequireAuth } from './shared/components/RequireAuth';
import { RequireSetup } from './shared/components/RequireSetup';

/**
 * The route table for the whole application (React Router v6).
 *
 * F-1 registers every Phase-1 path as a placeholder so that later features add a component and
 * remove a placeholder, rather than inventing a URL scheme feature by feature. The paths are
 * exactly those named in the plan's F-1 frontend design.
 *
 * `/login` is outside the AppLayout because F-2's login screen has no navigation chrome.
 * F-2 wraps the layout branch in a RequireAuth guard; F-3 adds `/setup` and wraps the same branch
 * in RequireSetup.
 */
export const routes: RouteObject[] = [
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    // Behind RequireAuth but deliberately *outside* RequireSetup: this is the screen the setup
    // gate redirects to, so gating it on setup being complete would be an infinite redirect.
    path: '/setup',
    element: (
      <RequireAuth>
        <FirstRunSetupPage />
      </RequireAuth>
    ),
  },
  {
    // Everything under here needs a session and a configured clinic. The server is the real gate
    // in both cases - every /api route but health, auth/login and auth/reauth is 401 without the
    // cookie, and the prescription endpoints are 409 until setup is complete - so these guards
    // decide what the physician sees, not what they can reach.
    path: '/',
    element: (
      <RequireAuth>
        <RequireSetup>
          <AppLayout />
        </RequireSetup>
      </RequireAuth>
    ),
    children: [
      // F-7. The home screen is the recent-patients list; F-9 adds today's appointments above it.
      { index: true, element: <RecentPatients /> },
      // F-7. Reads its query from `?query=`, so a search survives a refresh and can be shared.
      { path: 'patients', element: <PatientList /> },
      // F-5. `patients/new` is listed before `patients/:id` for readability only - React Router v6
      // ranks a static segment above a dynamic one regardless of declaration order, so "new" can
      // never be read as a patient id.
      { path: 'patients/new', element: <PatientForm /> },
      { path: 'patients/:id', element: <PatientProfile /> },
      { path: 'visits/:id', element: <PlaceholderPage title="Consultation" featureId="F-10" /> },
      { path: 'settings/clinic', element: <ClinicProfilePage /> },
      // F-4. Both sit inside RequireSetup like every other settings screen: configuring the
      // clinic's lists before the clinic itself has a name would be an odd place to start, and
      // F-3's gate already routes a first-run visitor to /setup.
      { path: 'settings/options', element: <ClinicSettingsPage /> },
      { path: 'settings/vitals-ranges', element: <VitalRangesPage /> },
      { path: 'export', element: <PlaceholderPage title="Export" featureId="F-18" /> },
      { path: 'audit', element: <PlaceholderPage title="Audit log" featureId="F-17" /> },
      {
        path: '*',
        element: <PlaceholderPage title="Page not found" featureId="F-1" />,
      },
    ],
  },
];

/** Every path F-1 registers, exported so a test can assert the table rather than the render. */
export const REGISTERED_PATHS = [
  '/login',
  '/setup',
  '/',
  '/patients',
  '/patients/new',
  '/patients/:id',
  '/visits/:id',
  '/settings/clinic',
  '/settings/options',
  '/settings/vitals-ranges',
  '/export',
  '/audit',
] as const;
