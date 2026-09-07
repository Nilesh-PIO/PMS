import { Link, useNavigate } from 'react-router-dom';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { EmptyState } from '../../shared/components/EmptyState';
import { PatientPickerRow } from '../../shared/components/PatientPickerRow';
import { useRecentPatients } from './usePatientSearch';

/**
 * Recent patients, on the home screen (route `/`, planning-pms-verification.md, F-7 point 4;
 * BRD L159 "View recent patients"; brainstorm E-2).
 *
 * **The empty state is the requirement here, not an afterthought.** E-2 is "no patients yet — search
 * and recent-patients are both empty", and its mitigation is an empty state that offers "Register
 * first patient" *as the only action*, explicitly not a blank panel. A brand-new clinic sees this
 * screen before it sees any other, and a blank rectangle on first run is indistinguishable from a
 * broken install.
 *
 * **What "recent" currently means.** Most recently *registered*, because `Visit` is F-10 and does
 * not exist yet — see `PatientRepository.GetRecentAsync` for the assumption and the flag raised for
 * the plan owner. When F-10 lands this becomes most-recently-seen with no change to this component.
 */
export function RecentPatients() {
  const navigate = useNavigate();
  const recent = useRecentPatients();

  if (recent.isLoading) {
    return (
      <section className="recent-patients">
        <h1>Today</h1>
        <p role="status">Loading recent patients...</p>
      </section>
    );
  }

  if (recent.error) {
    // Never rendered as "no recent patients". A failed load and an empty clinic look nothing alike
    // and must not be reported alike - the second invites registering someone who already exists.
    return (
      <section className="recent-patients">
        <h1>Today</h1>
        <div className="patient-results__error" role="alert">
          <p>
            {isProblemDetailsError(recent.error)
              ? recent.error.userMessage
              : 'Recent patients could not be loaded.'}
          </p>
          <button className="button" type="button" onClick={() => void recent.refetch()}>
            Try again
          </button>
        </div>
      </section>
    );
  }

  const patients = recent.data ?? [];

  return (
    <section className="recent-patients">
      <header className="recent-patients__header">
        <h1>Today</h1>
        <Link className="button button--primary" to="/patients/new">
          Register a patient
        </Link>
      </header>

      {patients.length === 0 ? (
        // E-2, and acceptance criterion 5.
        <EmptyState
          title="No patients registered yet"
          description="Once you register a patient they will appear here, so you can pick up where you left off."
          action={
            <Link className="button button--primary" to="/patients/new">
              Register the first patient
            </Link>
          }
        />
      ) : (
        <>
          <h2 className="recent-patients__subtitle">Recent patients</h2>
          <ul className="patient-results__list">
            {patients.map((patient) => (
              <li key={patient.id}>
                {/* The same picker row the search results use - so a patient chosen from here is
                    identified by exactly the same four facts (E-28). */}
                <PatientPickerRow
                  patient={patient}
                  onSelect={(id) => navigate(`/patients/${id}`)}
                />
              </li>
            ))}
          </ul>
          <p className="recent-patients__note">
            Looking for someone else? Press <kbd>/</kbd> to search by name or phone number.
          </p>
        </>
      )}
    </section>
  );
}
