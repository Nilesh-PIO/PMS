import type { ReactNode } from 'react';

/**
 * One patient, rendered the only way this application ever renders a patient in a list
 * (planning-pms-verification.md, F-6 point 4 and F-7 point 4; brainstorm REC-12, RSK-12, E-28).
 *
 * **Why this component exists at all.** RSK-12 is "wrong-patient selection from a name-only
 * picker", and its impact is rated Critical: one clinic has several Ravi Kumars, and a list showing
 * only names offers nothing to tell them apart before a prescription is written against the wrong
 * history. REC-12's answer is that *no* picker anywhere shows a name alone — always name plus phone
 * tail plus age/DOB plus last visit date.
 *
 * A rule like that, left as a convention, survives exactly as long as the next person who builds a
 * list remembers it. Making it a component means F-7's search results, F-9's appointment picker and
 * this dialog cannot render a patient row without the four fields, because there is nowhere to put
 * a row that does not have them.
 *
 * **Nothing missing is ever blank.** A patient with no phone renders "No phone" rather than an
 * empty cell. A blank is something the eye slides past; a stated absence is something the reader
 * registers, and "this one has no phone recorded and that one does" is itself disambiguating.
 */
export interface PatientPickerRowProps {
  fullName: string;
  /** Last four digits, or null. */
  phoneTail: string | null;
  /** Server-formatted age string — never re-derived on the client. */
  ageDisplay: string;
  /** ISO date, or null. */
  dateOfBirth: string | null;
  /** ISO date of the most recent visit, or null when there are none (or before F-10). */
  lastVisitDate: string | null;
  status?: 'Active' | 'Inactive';
  /** Rendered after the identifying fields — a match reason, a merged-away note. */
  children?: ReactNode;
}

export function PatientPickerRow({
  fullName,
  phoneTail,
  ageDisplay,
  dateOfBirth,
  lastVisitDate,
  status,
  children,
}: PatientPickerRowProps) {
  return (
    <div className="patient-picker-row">
      <p className="patient-picker-row__name">
        {fullName}
        {status === 'Inactive' ? (
          <span className="patient-picker-row__badge"> Inactive</span>
        ) : null}
      </p>

      <dl className="patient-picker-row__facts">
        <div className="patient-picker-row__fact">
          <dt>Phone</dt>
          <dd>{phoneTail ? `…${phoneTail}` : 'No phone'}</dd>
        </div>

        <div className="patient-picker-row__fact">
          <dt>Age</dt>
          <dd>{ageDisplay}</dd>
        </div>

        <div className="patient-picker-row__fact">
          <dt>Date of birth</dt>
          <dd>{dateOfBirth ? formatDate(dateOfBirth) : 'Not recorded'}</dd>
        </div>

        <div className="patient-picker-row__fact">
          <dt>Last visit</dt>
          <dd>{lastVisitDate ? formatDate(lastVisitDate) : 'No visits recorded'}</dd>
        </div>
      </dl>

      {children}
    </div>
  );
}

/**
 * Renders an ISO date in the browser's locale.
 *
 * Only ever applied to a *date*, never to an age. Ages arrive pre-formatted from the server
 * (`ageDisplay`) precisely so the screen and F-14's printed prescription cannot disagree about
 * them — re-deriving one here would reintroduce the divergence that decision exists to prevent.
 */
function formatDate(isoDate: string): string {
  const parsed = new Date(isoDate);

  if (Number.isNaN(parsed.getTime())) {
    // Show what the server sent rather than "Invalid Date". An unexpected value is still
    // information; a placeholder is not.
    return isoDate;
  }

  return parsed.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });
}
