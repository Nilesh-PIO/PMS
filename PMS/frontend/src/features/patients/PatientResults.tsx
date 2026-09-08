import { Link } from 'react-router-dom';
import { EmptyState } from '../../shared/components/EmptyState';
import { PatientPickerRow } from '../../shared/components/PatientPickerRow';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import type { PatientSummary } from './types/patient';

/**
 * The result list shared by the global search box and the patients page (F-7).
 *
 * Extracted because the two screens must not disagree about what "no match" looks like. E-7 calls
 * the empty result "the moment a typo turns into a duplicate if unhandled", and the handling is the
 * inline register action below — if one screen offered it and the other showed a bare "0 results",
 * the duplicate would just be created from the other screen.
 */

export interface PatientResultsProps {
  query: string;
  results: PatientSummary[] | undefined;
  isLoading: boolean;
  isFetching: boolean;
  error: unknown;
  onSelect: (id: string) => void;
  /** Rendered before the query is long enough to search. */
  idleMessage?: string;
}

export function PatientResults({
  query,
  results,
  isLoading,
  isFetching,
  error,
  onSelect,
  idleMessage,
}: PatientResultsProps) {
  const trimmed = query.trim();

  if (trimmed.length < 2) {
    return idleMessage ? (
      <p className="patient-results__hint" role="status">
        {idleMessage}
      </p>
    ) : null;
  }

  if (error) {
    // The error is shown, never swallowed, and never rendered as "no patients found" - which would
    // be the dangerous lie here, because "not found" is what makes someone register a duplicate.
    return (
      <div className="patient-results__error" role="alert">
        <p>{errorMessage(error)}</p>
        <p className="patient-results__error-note">
          This is a problem reaching the server, not a result. The patient may well exist.
        </p>
      </div>
    );
  }

  if (isLoading || !results) {
    return (
      <p className="patient-results__hint" role="status">
        Searching...
      </p>
    );
  }

  if (results.length === 0) {
    // E-7, and acceptance criterion 3. The typed text is carried into the registration form, so the
    // action is one click rather than "now type it all again".
    return (
      <EmptyState
        title="No patient found"
        description={
          <>
            Nothing matches <strong>{trimmed}</strong>, including names spelled similarly.
          </>
        }
        action={
          <Link className="button button--primary" to={registerLinkFor(trimmed)}>
            Register &quot;{trimmed}&quot; as a new patient
          </Link>
        }
      />
    );
  }

  const isFuzzy = results.every((r) => r.matchKind === 'SimilarName');

  return (
    <div
      className={isFetching ? 'patient-results patient-results--stale' : 'patient-results'}
      aria-busy={isFetching}
    >
      {isFuzzy ? (
        // The fuzzy fallback ran, which means nothing matched exactly. Saying so out loud is what
        // stops a guess being read as a find (E-30).
        <p className="patient-results__note" role="status">
          No exact match. Showing patients with similar names — check the phone number and age
          before choosing one.
        </p>
      ) : null}

      <ul className="patient-results__list">
        {results.map((patient) => (
          <li key={patient.id}>
            {/* Spread rather than field-by-field: PatientSummary's field names *are* the row's
                props, so a field added to the DTO reaches the row without anyone remembering to
                pass it on. */}
            <PatientPickerRow {...patient} onSelect={onSelect} />
          </li>
        ))}
      </ul>

      {/* Always offered, even when there are matches: the person in front of the desk may be a
          different Ravi Kumar from all of them, and that is exactly when a duplicate gets created
          by picking the closest-looking row instead. */}
      <p className="patient-results__register">
        Not one of these?{' '}
        <Link to={registerLinkFor(trimmed)}>Register &quot;{trimmed}&quot; as a new patient</Link>
      </p>
    </div>
  );
}

/**
 * Carries the typed text into the registration form.
 *
 * A query that is all digits is prefilled as a **phone number**, not as a name — someone who typed
 * a phone number and found nothing is registering a patient whose phone they know, and putting
 * "9876543210" in the name field would be a silly thing to make them delete.
 */
export function registerLinkFor(query: string): string {
  const digitsOnly = /^[\d\s()+-]+$/.test(query) && /\d/.test(query);
  const params = new URLSearchParams(digitsOnly ? { phone: query } : { name: query });
  return `/patients/new?${params.toString()}`;
}

function errorMessage(error: unknown): string {
  // `userMessage` is the shared wrapper's own best sentence, including the network-failure case.
  // Re-deriving one here would be a second opinion that drifts from every other screen's.
  return isProblemDetailsError(error)
    ? error.userMessage
    : 'The search could not be completed.';
}
