import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { ClinicProfileForm } from './ClinicProfileForm';
import { useClinicProfile } from './useClinicProfile';

/**
 * The clinic profile as an ordinary settings screen (route `/settings/clinic`,
 * planning-pms-verification.md, F-3 point 4).
 *
 * Same form as first-run setup, different framing: this one is reached from the navigation, sits
 * inside the app layout, and does not navigate anywhere on save.
 */
export function ClinicProfilePage() {
  const { data: profile, isPending, isError, error } = useClinicProfile();

  if (isPending) {
    return (
      <p className="app-status" role="status">
        Loading the clinic profile...
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
    <section className="clinic-page">
      <h1>Clinic profile</h1>
      <p className="clinic-page__intro">
        These details print at the top and bottom of every prescription.
      </p>

      <ClinicProfileForm profile={profile} submitLabel="Save changes" />
    </section>
  );
}
