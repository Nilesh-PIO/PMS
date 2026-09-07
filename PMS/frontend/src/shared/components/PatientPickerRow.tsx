import type { ReactNode } from 'react';

/**
 * One patient, rendered the only way this application ever renders a patient in a list
 * (planning-pms-verification.md, F-6 point 4 and F-7 point 4; brainstorm REC-12, RSK-12, E-28).
 *
 * **Why this component exists at all.** RSK-12 is "wrong-patient selection from a name-only
 * picker", and its impact is rated Critical: one clinic has several Ravi Kumars, and a list showing
 * only names offers nothing to tell them apart before a prescription is written against the wrong
 * history. REC-12's answer is that *no* picker anywhere shows a name alone — always name plus phone
 * tail plus age/DOB plus a date.
 *
 * A rule like that, left as a convention, survives exactly as long as the next person who builds a
 * list remembers it. Making it a component means F-6's duplicate candidates, F-7's search results
 * and F-9's appointment picker cannot render a patient row without those fields, because there is
 * nowhere to put a row that does not have them.
 *
 * **Nothing missing is ever blank.** A patient with no phone renders "No phone recorded" rather
 * than an empty cell. A blank is something the eye slides past — and it is indistinguishable from a
 * rendering bug; a stated absence is something the reader registers, and "this one has no phone on
 * file and that one does" is itself disambiguating.
 *
 * **Nothing here selects itself.** With `onSelect` the row is a button the physician presses;
 * without it the row is inert. There is no auto-focus and no "if there is exactly one result,
 * activate it" behaviour, and adding one would reopen the exact risk this component closes.
 *
 * **MERGE NOTE (F-6 + F-7).** Both features independently created this file at the path the plan
 * names for both of them, and both left a note saying whichever merged second should end up with
 * one component rather than two. This is that one component: F-6's `<dl>` fact list, explicit
 * date-of-birth row and stated absences, plus F-7's selectable-button mode, badge row and
 * accessible label. Props are flat rather than a patient object on purpose — `shared/` must not
 * import a `features/` type, and the two features' DTOs (`DuplicateCandidate` and
 * `PatientSummary`) genuinely differ in what they carry.
 */
export interface PatientPickerRowProps {
  id: string;
  fullName: string;
  /** Last four digits, or null when no phone was recorded. */
  phoneTail: string | null;
  /** Server-formatted age string — never re-derived on the client. */
  ageDisplay: string;
  gender?: string | null;
  /**
   * ISO date of birth, or null when none is on file.
   *
   * `undefined` and `null` mean different things here, deliberately. `null` is "this patient has no
   * date of birth recorded" and renders as such; `undefined` is "this row's DTO does not carry a
   * date of birth at all" (F-7's `PatientSummary`) and omits the fact entirely, rather than
   * asserting an absence the caller never claimed.
   */
  dateOfBirth?: string | null;
  /** ISO date of the most recent visit, or null when there are none (always null before F-10). */
  lastVisitDate: string | null;
  /** ISO date the record was created. Carries the date axis until F-10 introduces `Visit`. */
  registeredOn?: string | null;
  status?: 'Active' | 'Inactive';
  /** True when this record points at a survivor (F-6). Rendered as a warning, never hidden. */
  isMerged?: boolean;
  isProfileIncomplete?: boolean;
  /** `SimilarName` means this row is a *guess* from F-7's fuzzy fallback (E-30). */
  matchKind?: 'Name' | 'Phone' | 'SimilarName' | 'Recent';
  /**
   * Given: the row is a button. Omitted: the row is inert — which is what F-6's dialog wants, since
   * merge tooling is Phase 2 and a row that looked selectable but only navigated would be worse
   * than no action at all.
   */
  onSelect?: (id: string) => void;
  /** Rendered after the identifying fields — a match reason, a merged-away note. */
  children?: ReactNode;
}

export function PatientPickerRow(props: PatientPickerRowProps) {
  const {
    id,
    fullName,
    phoneTail,
    ageDisplay,
    gender,
    dateOfBirth,
    status,
    isMerged,
    isProfileIncomplete,
    matchKind,
    onSelect,
    children,
  } = props;

  const content = (
    <>
      <p className="patient-picker-row__name">
        {fullName}
        {status === 'Inactive' ? (
          <span className="patient-picker-row__badge patient-picker-row__badge--warn">
            Inactive
          </span>
        ) : null}
        {isMerged ? (
          <span className="patient-picker-row__badge patient-picker-row__badge--warn">Merged</span>
        ) : null}
        {matchKind === 'SimilarName' ? (
          <span className="patient-picker-row__badge patient-picker-row__badge--guess">
            Similar name
          </span>
        ) : null}
        {isProfileIncomplete ? (
          <span className="patient-picker-row__badge">Profile incomplete</span>
        ) : null}
      </p>

      <dl className="patient-picker-row__facts">
        {/* Every one of these is present on every row - see the component docs. */}
        <div className="patient-picker-row__fact">
          <dt>Phone</dt>
          <dd>{phoneTail ? `…${phoneTail}` : 'No phone recorded'}</dd>
        </div>

        <div className="patient-picker-row__fact">
          <dt>Age</dt>
          <dd>{ageDisplay}</dd>
        </div>

        {gender ? (
          <div className="patient-picker-row__fact">
            <dt>Gender</dt>
            <dd>{gender}</dd>
          </div>
        ) : null}

        {dateOfBirth !== undefined ? (
          <div className="patient-picker-row__fact">
            <dt>Date of birth</dt>
            <dd>{dateOfBirth ? formatDate(dateOfBirth) : 'Not recorded'}</dd>
          </div>
        ) : null}

        <div className="patient-picker-row__fact">
          {/*
            The label changes with the meaning, so "Registered 7 Sep 2026" is never misread as a
            visit that did not happen. The day LastVisitDate starts arriving from F-10, the label
            becomes "Last visit" on its own.
          */}
          <dt>{dateLabel(props)}</dt>
          <dd>{dateValue(props)}</dd>
        </div>
      </dl>

      {children}
    </>
  );

  if (!onSelect) {
    return (
      <div className="patient-picker-row" data-testid="patient-picker-row">
        {content}
      </div>
    );
  }

  return (
    <button
      type="button"
      className="patient-picker-row patient-picker-row--selectable"
      data-testid="patient-picker-row"
      onClick={() => onSelect(id)}
      // The full name is already the visible text; the accessible name adds the disambiguators so a
      // screen-reader user hears the same facts a sighted user reads, rather than a list of
      // identical "Ravi Kumar" buttons.
      aria-label={accessibleLabel(props)}
    >
      {content}
    </button>
  );
}

/** "Last visit" once there is one, "Registered" while the row is falling back to its creation date. */
function dateLabel({ lastVisitDate, registeredOn }: PatientPickerRowProps): string {
  if (!lastVisitDate && registeredOn) {
    return 'Registered';
  }
  return 'Last visit';
}

function dateValue({ lastVisitDate, registeredOn }: PatientPickerRowProps): string {
  if (lastVisitDate) {
    return formatDate(lastVisitDate);
  }
  if (registeredOn) {
    return formatDate(registeredOn);
  }
  return 'No visits recorded';
}

/**
 * Renders an ISO date.
 *
 * Only ever applied to a *date*, never to an age. Ages arrive pre-formatted from the server
 * (`ageDisplay`) precisely so the screen and F-14's printed prescription cannot disagree about
 * them — re-deriving one here would reintroduce the divergence that decision exists to prevent.
 *
 * Day-month-year with a written month, fixed to `en-GB`: a purely numeric date is read differently
 * on either side of an ocean, and this one sits next to a clinical record. Fixing the locale also
 * keeps the rendering identical between a physician's browser and a test runner, so a date
 * assertion means the same thing in both.
 */
function formatDate(isoDate: string): string {
  const parsed = new Date(isoDate);

  if (Number.isNaN(parsed.getTime())) {
    // Show what the server sent rather than "Invalid Date". An unexpected value is still
    // information; a placeholder is not.
    return isoDate;
  }

  return parsed.toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

function accessibleLabel(props: PatientPickerRowProps): string {
  const parts = [
    props.fullName,
    props.phoneTail ? `phone ending ${props.phoneTail}` : 'no phone recorded',
    props.ageDisplay,
    `${dateLabel(props).toLowerCase()} ${dateValue(props)}`,
  ];

  if (props.status === 'Inactive') {
    parts.push('inactive record');
  }
  if (props.isMerged) {
    parts.push('merged into another record');
  }
  if (props.matchKind === 'SimilarName') {
    parts.push('similar name, not an exact match');
  }
  if (props.isProfileIncomplete) {
    parts.push('profile incomplete');
  }

  return parts.join(', ');
}
