import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { PatientPickerRow } from '../../shared/components/PatientPickerRow';
import type { DuplicateCandidate } from './types/patient';

/** The `ruleType` the server puts on a duplicate 409 (F-6). */
export const DUPLICATE_CONFIRMATION_REQUIRED = 'duplicate-confirmation-required';

/**
 * The candidates carried by a duplicate 409, or null if this is any other error.
 *
 * **Why this is defensive about the shape.** The candidates arrive as an RFC-7807 *extension*, which
 * is by definition untyped on the wire, and this value decides whether a failed registration is
 * shown as a question or as an error. If a proxy mangled the body, or the server changed the
 * extension's name, the wrong branch here would leave the physician staring at a dialog with no rows
 * in it and no way to tell what happened. So the caller only gets a non-null result when the rule
 * type matches *and* the payload is a non-empty array — otherwise this reports "not a duplicate
 * error" and the ordinary error banner does its job.
 */
export function duplicateCandidatesFrom(error: unknown): DuplicateCandidate[] | null {
  if (!isProblemDetailsError(error) || error.ruleType !== DUPLICATE_CONFIRMATION_REQUIRED) {
    return null;
  }

  const candidates = error.problem.candidates;

  if (!Array.isArray(candidates) || candidates.length === 0) {
    return null;
  }

  return candidates as DuplicateCandidate[];
}

/**
 * The warning shown when the person being registered may already be on file
 * (planning-pms-verification.md, F-6 point 4; brainstorm REC-2, REC-12, E-25, E-27, E-28).
 *
 * **This dialog asks a question; it does not deliver a verdict.** REC-2 is explicit that the
 * duplicate check warns and never blocks — a blocking rule turns a false positive into a patient who
 * cannot be registered, and the front desk's answer to that is to invent a different spelling, which
 * produces the duplicate the rule was trying to prevent, now with a name nobody can search for. So
 * "Register anyway" is always available and always works.
 *
 * **Two kinds of row, deliberately distinguished.** Suspected duplicates are what the physician is
 * being asked about. Other patients on the same phone number are context — a phone identifies a
 * household, not a person (E-27), and three Kumars sharing a number is ordinary. Showing both, and
 * labelling which is which, is what stops the warning from crying wolf while still letting the
 * physician see the whole picture.
 *
 * **Nothing here is selectable.** There is no "this is the same person, use that record instead"
 * action, because merge tooling is Phase 2 (plan section 11) and a button that looked like it
 * merged records but only navigated would be worse than no button. The rows identify; the physician
 * decides.
 */
export interface DuplicateWarningDialogProps {
  candidates: DuplicateCandidate[];
  /** Register this patient anyway — the warning dismissed and the intent confirmed. */
  onConfirm: () => void;
  /** Go back to the form without registering anyone. */
  onCancel: () => void;
  /** True while the confirmed registration is in flight. */
  isSubmitting?: boolean;
}

export function DuplicateWarningDialog({
  candidates,
  onConfirm,
  onCancel,
  isSubmitting = false,
}: DuplicateWarningDialogProps) {
  const likely = candidates.filter((c) => c.isLikelyDuplicate);
  const household = candidates.filter((c) => !c.isLikelyDuplicate);

  return (
    <div
      className="duplicate-dialog"
      role="dialog"
      aria-modal="true"
      aria-labelledby="duplicate-dialog-title"
    >
      <div className="duplicate-dialog__panel">
        <h2 id="duplicate-dialog-title">
          {likely.length === 1
            ? 'This patient may already be registered'
            : `${likely.length} patients on file may be this person`}
        </h2>

        <p className="duplicate-dialog__lead">
          Check the records below before registering. If this is the same person, open their
          existing record instead of creating a second one — a patient with two records has half
          their history on each.
        </p>

        {likely.length > 0 ? (
          <ul className="duplicate-dialog__list">
            {likely.map((candidate) => (
              <li key={candidate.id} className="duplicate-dialog__item">
                <PatientPickerRow
                  fullName={candidate.fullName}
                  phoneTail={candidate.phoneTail}
                  ageDisplay={candidate.ageDisplay}
                  dateOfBirth={candidate.dateOfBirth}
                  lastVisitDate={candidate.lastVisitDate}
                  status={candidate.status}
                >
                  <p className="duplicate-dialog__reason">{describeMatch(candidate)}</p>
                  {candidate.mergedIntoPatientId ? (
                    <p className="duplicate-dialog__reason">
                      Already marked as a duplicate of another record.
                    </p>
                  ) : null}
                </PatientPickerRow>
              </li>
            ))}
          </ul>
        ) : null}

        {household.length > 0 ? (
          <section className="duplicate-dialog__household">
            {/*
              Not a warning. A phone number is a household identifier (E-27), so these are almost
              certainly relatives - and saying so plainly is what keeps the rows above meaningful.
            */}
            <h3>
              {household.length === 1
                ? 'One other patient uses this phone number'
                : `${household.length} other patients use this phone number`}
            </h3>
            <p className="duplicate-dialog__lead">
              Different names, so probably family rather than a duplicate. Shown for context.
            </p>
            <ul className="duplicate-dialog__list">
              {household.map((candidate) => (
                <li key={candidate.id} className="duplicate-dialog__item">
                  <PatientPickerRow
                    fullName={candidate.fullName}
                    phoneTail={candidate.phoneTail}
                    ageDisplay={candidate.ageDisplay}
                    dateOfBirth={candidate.dateOfBirth}
                    lastVisitDate={candidate.lastVisitDate}
                    status={candidate.status}
                  />
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <div className="duplicate-dialog__actions">
          {/*
            Cancel first and styled as the primary action: the safe choice should be the easy one.
            "Register anyway" is always enabled, though - REC-2's warn-never-block is the point of
            the whole dialog, and a disabled confirm would quietly turn it into a block.
          */}
          <button type="button" className="button button--primary" onClick={onCancel}>
            Go back and check
          </button>
          <button
            type="button"
            className="button"
            onClick={onConfirm}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Registering...' : 'Register anyway'}
          </button>
        </div>
      </div>
    </div>
  );
}

/**
 * Why this row is being shown, in words rather than a slug.
 *
 * A warning the physician cannot interrogate is a warning they learn to click through. "Same phone
 * number and the same date of birth" is a claim that can be evaluated in a second; an unexplained
 * highlighted box is not.
 */
function describeMatch(candidate: DuplicateCandidate): string {
  switch (candidate.matchReason) {
    case 'phone-and-date-of-birth':
      return 'Same phone number and the same date of birth.';
    case 'date-of-birth':
      return 'Same date of birth.';
    case 'phone':
    default:
      return 'Same phone number.';
  }
}
