import { Navigate, useNavigate } from 'react-router-dom';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { ClinicProfileForm } from '../clinic/ClinicProfileForm';
import { useClinicProfile } from '../clinic/useClinicProfile';
import { useSession } from '../auth/useSession';

/**
 * First-run setup (route `/setup`, planning-pms-verification.md, F-3 point 4).
 *
 * **E-1.** This is the screen that stands between a brand-new install and a prescription printed
 * with no clinic identity on it. It renders outside `AppLayout` deliberately: there is nothing
 * useful to navigate to until the clinic has a name, and offering the navigation would invite the
 * physician to skip the one screen they cannot skip.
 */
export function FirstRunSetupPage() {
  const navigate = useNavigate();
  const { data: session, isPending: sessionPending } = useSession();
  const { data: profile, isPending: profilePending, isError, error } = useClinicProfile();

  if (!sessionPending && session?.setupComplete) {
    // Setup is already done; /settings/clinic is where edits belong. Without this, typing /setup
    // by hand would hand the physician a first-run wizard for a clinic that is already running.
    return <Navigate to="/settings/clinic" replace />;
  }

  if (sessionPending || profilePending) {
    return (
      <p className="app-status" role="status">
        Loading...
      </p>
    );
  }

  if (isError) {
    return (
      <div className="app-status app-status--error" role="alert">
        <p>
          {isProblemDetailsError(error)
            ? error.userMessage
            : 'Could not load the clinic profile. Check the connection and try again.'}
        </p>
      </div>
    );
  }

  return (
    <main className="setup-page">
      <div className="setup-page__panel">
        <h1>First-run setup</h1>
        <p className="setup-page__intro">
          Before the first consultation, tell the system who this clinic is. These details print on
          every prescription, so prescriptions cannot be issued until they are saved.
        </p>

        <ClinicProfileForm
          profile={profile}
          submitLabel="Save and continue"
          onSaved={(saved) => {
            // Only leave once the server confirms the clinic identity is complete. If a future
            // rule makes the gate stricter, this page stays put rather than dropping the
            // physician into an app that will refuse to print.
            if (saved.isSetupComplete) {
              navigate('/', { replace: true });
            }
          }}
        />
      </div>
    </main>
  );
}
