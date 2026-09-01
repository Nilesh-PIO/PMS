import type { RouteObject } from 'react-router-dom';
import { LoginPage } from './features/auth/LoginPage';
import { ClinicProfilePage } from './features/clinic/ClinicProfilePage';
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
      { index: true, element: <PlaceholderPage title="Today" featureId="F-7 / F-9" /> },
      { path: 'patients', element: <PlaceholderPage title="Patients" featureId="F-7" /> },
      { path: 'patients/:id', element: <PlaceholderPage title="Patient profile" featureId="F-5" /> },
      { path: 'visits/:id', element: <PlaceholderPage title="Consultation" featureId="F-10" /> },
      { path: 'settings/clinic', element: <ClinicProfilePage /> },
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
  '/patients/:id',
  '/visits/:id',
  '/settings/clinic',
  '/export',
  '/audit',
] as const;
