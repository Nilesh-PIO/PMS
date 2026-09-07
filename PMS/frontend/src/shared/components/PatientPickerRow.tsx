import type { PatientSummary } from '../../features/patients/types/patient';

/**
 * One patient in a selection list (planning-pms-verification.md, F-6 point 4 and F-7 point 4;
 * brainstorm E-28, RSK-12, REC-12).
 *
 * **This component is the wrong-patient guard, expressed as UI.** RSK-12 — "wrong-patient selection
 * from a name-only picker" — is rated Critical, and E-28 states the rule: *never show a name alone
 * in a selection list*. So this is the only way a patient is ever rendered as a choice, anywhere in
 * the application, and it always renders four things:
 *
 * - the **name**, as entered;
 * - the **phone tail**, or an explicit "no phone" — the strongest everyday disambiguator between
 *   two people with one name;
 * - the **age**, server-formatted so it reads the same here as on the prescription;
 * - a **date** — last visit once F-10 exists, registration date until then — because two patients
 *   can share a name, an age *and* have no phone between them, and something still has to tell
 *   them apart.
 *
 * "No phone recorded" is rendered rather than an empty cell on purpose: a blank space looks like a
 * rendering bug, and the physician cannot tell whether the field is missing or the row is broken.
 *
 * **Nothing here selects itself.** The row is a button the physician presses. There is no
 * auto-focus, no "if there is exactly one result, activate it" behaviour, and adding one would
 * reopen the exact risk this component exists to close.
 *
 * **COORDINATION NOTE (F-6):** plan F-6 point 4 names this same path,
 * `shared/components/PatientPickerRow.tsx`, for the duplicate-candidate list. F-6 is being built in
 * parallel on its own branch and will create this file too, so an add/add conflict here at merge
 * time is expected and is the correct outcome — both features are meant to render candidates
 * identically. Resolve by keeping one component, not by letting each feature have its own.
 */
export interface PatientPickerRowProps {
  patient: PatientSummary;
  /** Called with the patient's id when the row is chosen. */
  onSelect?: (id: string) => void;
  /** Rendered instead of a button when the row is informational rather than selectable. */
  as?: 'button' | 'div';
  /** Extra detail rendered after the standard fields, e.g. a duplicate-match reason (F-6). */
  children?: React.ReactNode;
}

export function PatientPickerRow({
  patient,
  onSelect,
  as = 'button',
  children,
}: PatientPickerRowProps) {
  const isGuess = patient.matchKind === 'SimilarName';

  const content = (
    <>
      <span className="picker-row__primary">
        <span className="picker-row__name">{patient.fullName}</span>
        {patient.status === 'Inactive' ? (
          <span className="picker-row__badge picker-row__badge--warn">Inactive</span>
        ) : null}
        {patient.isMerged ? (
          <span className="picker-row__badge picker-row__badge--warn">Merged</span>
        ) : null}
        {isGuess ? (
          <span className="picker-row__badge picker-row__badge--guess">Similar name</span>
        ) : null}
        {patient.isProfileIncomplete ? (
          <span className="picker-row__badge">Profile incomplete</span>
        ) : null}
      </span>

      <span className="picker-row__secondary">
        {/* Every one of these is present on every row - see the component docs. */}
        <span className="picker-row__field">
          {patient.phoneTail ? `Phone ...${patient.phoneTail}` : 'No phone recorded'}
        </span>
        <span className="picker-row__field">{patient.ageDisplay}</span>
        {patient.gender ? <span className="picker-row__field">{patient.gender}</span> : null}
        <span className="picker-row__field">{describeLastSeen(patient)}</span>
      </span>

      {children}
    </>
  );

  if (as === 'div' || !onSelect) {
    return (
      <div className="picker-row picker-row--static" data-testid="patient-picker-row">
        {content}
      </div>
    );
  }

  return (
    <button
      type="button"
      className="picker-row"
      data-testid="patient-picker-row"
      onClick={() => onSelect(patient.id)}
      // The full name is already the visible text; the accessible name adds the disambiguators so a
      // screen-reader user hears the same four facts a sighted user reads, rather than a list of
      // identical "Ravi Kumar" buttons.
      aria-label={accessibleLabel(patient)}
    >
      {content}
    </button>
  );
}

/**
 * The date column.
 *
 * Prefers the last visit, which is what a physician actually wants to know, and falls back to the
 * registration date while `lastVisitDate` is null — which it always is until F-10 introduces
 * `Visit`. The label changes with the meaning, so "Registered 7 Sep 2026" is never misread as a
 * visit that did not happen.
 */
function describeLastSeen(patient: PatientSummary): string {
  if (patient.lastVisitDate) {
    return `Last visit ${formatDate(patient.lastVisitDate)}`;
  }
  return `Registered ${formatDate(patient.registeredOn)}`;
}

/**
 * Formats an ISO date for display.
 *
 * Day-month-year with a written month, because a purely numeric date is read differently on either
 * side of an ocean and this one sits next to a clinical record.
 */
function formatDate(isoDate: string): string {
  const parsed = new Date(isoDate);
  if (Number.isNaN(parsed.getTime())) {
    // Show what the server sent rather than "Invalid Date". A visible oddity is debuggable; a
    // swallowed one is not.
    return isoDate;
  }
  return parsed.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

function accessibleLabel(patient: PatientSummary): string {
  const parts = [
    patient.fullName,
    patient.phoneTail ? `phone ending ${patient.phoneTail}` : 'no phone recorded',
    patient.ageDisplay,
    describeLastSeen(patient),
  ];

  if (patient.status === 'Inactive') {
    parts.push('inactive record');
  }
  if (patient.isMerged) {
    parts.push('merged into another record');
  }
  if (patient.matchKind === 'SimilarName') {
    parts.push('similar name, not an exact match');
  }

  return parts.join(', ');
}
