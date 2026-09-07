import { Link, useParams } from 'react-router-dom';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { usePatient } from './usePatients';
import { MISSING_FIELD_LABELS } from './types/patient';

/**
 * A patient's profile (route `/patients/:id`, planning-pms-verification.md, F-5 point 4;
 * brainstorm REC-12, E-8, E-20).
 *
 * **The header is the identity check** (REC-12, RSK-12). Name, phone tail and age are shown
 * together and always, because the clinic has several patients called Ravi Kumar and a screen
 * showing only a name gives the physician no way to notice they have opened the wrong history
 * before prescribing against it. F-7's picker reuses the same three values for the same reason.
 *
 * **An incomplete profile says what is missing, by name** (E-8, E-20). "Profile incomplete" alone
 * is a nag; "no phone number recorded" is something that can be fixed in the next ten seconds.
 */
export function PatientProfile() {
  const { id } = useParams<{ id: string }>();
  const query = usePatient(id);

  if (query.isPending) {
    return (
      <p className="app-status" role="status">
        Loading the patient...
      </p>
    );
  }

  if (query.isError) {
    const notFound = isProblemDetailsError(query.error) && query.error.status === 404;

    return (
      <div className="app-status app-status--error" role="alert">
        <p>
          {notFound
            ? 'No patient was found with this id. The link may be out of date.'
            : isProblemDetailsError(query.error)
              ? query.error.userMessage
              : 'Could not load this patient. Check the connection and try again.'}
        </p>
        <Link to="/patients">Back to patients</Link>
      </div>
    );
  }

  const patient = query.data;

  return (
    <section className="patients-page">
      <header className="patient-profile__header">
        <h1>{patient.fullName}</h1>

        {/*
          The three identifying values, together and unconditional - never the name on its own.
          Each renders an explicit "not recorded" rather than collapsing to an empty space, so a
          missing value is visibly missing instead of looking like a rendering glitch.
        */}
        <dl className="patient-profile__identity">
          <div>
            <dt>Phone</dt>
            <dd>
              {patient.primaryPhone ?? 'No contact recorded'}
              {patient.phoneTail ? <span className="visually-hidden"> ending {patient.phoneTail}</span> : null}
            </dd>
          </div>
          <div>
            <dt>Age</dt>
            <dd>{patient.ageDisplay}</dd>
          </div>
          <div>
            <dt>Gender</dt>
            <dd>{patient.gender ?? 'Not answered'}</dd>
          </div>
        </dl>

        {patient.status === 'Inactive' ? (
          <p className="patient-profile__inactive" role="status">
            This record is inactive
            {patient.inactiveReason ? `: ${patient.inactiveReason}` : '.'}
          </p>
        ) : null}
      </header>

      {patient.isProfileIncomplete ? (
        <section className="patient-profile__incomplete" role="status">
          <h2>Profile incomplete</h2>
          <p>
            This patient can be seen and prescribed for as normal. These details have not been
            recorded yet:
          </p>
          <ul>
            {patient.missingFields.map((field) => (
              <li key={field}>{MISSING_FIELD_LABELS[field] ?? field}</li>
            ))}
          </ul>
        </section>
      ) : null}

      <dl className="patient-profile__details">
        <div>
          <dt>Date of birth</dt>
          <dd>{patient.dateOfBirth ?? 'Not recorded'}</dd>
        </div>
        <div>
          <dt>Alternate contact</dt>
          <dd>{patient.altContact ?? 'None recorded'}</dd>
        </div>
        <div>
          <dt>Registered</dt>
          <dd>{new Date(patient.registeredUtc).toLocaleDateString()}</dd>
        </div>
      </dl>
    </section>
  );
}
