import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { DuplicateWarningDialog, duplicateCandidatesFrom } from './DuplicateWarningDialog';
import { ProblemDetailsError } from '../../shared/api/problemDetails';
import type { DuplicateCandidate } from './types/patient';

/**
 * F-6 frontend unit tests (plan F-6 point 6): "candidate rows show four disambiguating fields
 * (E-28)".
 *
 * The named case is the first block. The rest covers the two properties this dialog exists to have
 * and which are easy to lose in a refactor: that "Register anyway" is always available (REC-2 —
 * warn, never block), and that household rows are visibly not warnings (E-27).
 */

function aCandidate(overrides: Partial<DuplicateCandidate> = {}): DuplicateCandidate {
  return {
    id: 'existing-1',
    fullName: 'Ravi Kumar',
    phoneTail: '3210',
    ageDisplay: '41',
    dateOfBirth: '1985-03-02',
    gender: 'Male',
    status: 'Active',
    mergedIntoPatientId: null,
    lastVisitDate: null,
    matchReason: 'phone',
    nameSimilarity: 1,
    isLikelyDuplicate: true,
    ...overrides,
  };
}

describe('DuplicateWarningDialog (F-6)', () => {
  // --- E-28 / REC-12: never a name alone ---------------------------------

  it('shows name, phone tail, age/DOB and last visit date on every candidate row', () => {
    // Plan acceptance criterion 4. RSK-12 - "wrong-patient selection from a name-only picker" - is
    // rated Critical, and this is the screen where the physician compares near-identical names.
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate()]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    const row = screen.getByRole('listitem');

    expect(within(row).getByText('Ravi Kumar')).toBeInTheDocument();
    expect(within(row).getByText('…3210')).toBeInTheDocument();
    expect(within(row).getByText('41')).toBeInTheDocument();
    // Matched loosely on purpose: the date is rendered in the browser's own locale, so asserting a
    // literal string would pin this test to whichever locale the test runner happens to have. What
    // the acceptance criterion requires is that the date of birth is on the row, not that it is
    // formatted one particular way.
    expect(within(row).getByText(/\b1985\b/)).toBeInTheDocument();

    // The fourth field is present and rendered even though no Visit entity exists until F-10.
    // "No visits recorded" is information; an absent row would silently shrink the picker.
    expect(within(row).getByText('No visits recorded')).toBeInTheDocument();
  });

  it('states a missing phone rather than leaving the cell blank', () => {
    // A blank is something the eye slides past. "No phone" is itself disambiguating - one of these
    // two people has a number on file and the other does not.
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate({ phoneTail: null, matchReason: 'date-of-birth' })]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByText('No phone')).toBeInTheDocument();
  });

  it('never renders a full phone number', () => {
    // Only the tail is on the DTO, and this list can be visible from the waiting side of the desk
    // before anyone has chosen to open a record.
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate()]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.queryByText(/98765/)).not.toBeInTheDocument();
  });

  it('explains why each candidate was flagged', () => {
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate({ matchReason: 'phone-and-date-of-birth' })]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    // A warning the physician cannot interrogate is a warning they learn to click through.
    expect(
      screen.getByText('Same phone number and the same date of birth.'),
    ).toBeInTheDocument();
  });

  it('says when a candidate has already been marked as a duplicate', () => {
    // Otherwise the physician marks it a second time, or reads it as the current record (E-26).
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate({ mergedIntoPatientId: 'survivor-1', status: 'Inactive' })]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(
      screen.getByText('Already marked as a duplicate of another record.'),
    ).toBeInTheDocument();
    expect(screen.getByText('Inactive')).toBeInTheDocument();
  });

  // --- REC-2: warn, never block ------------------------------------------

  it('always offers a way to register anyway', () => {
    // Acceptance criterion 2. A blocking rule turns a false positive into a patient who cannot be
    // registered, and the desk's response to that is to invent a spelling - producing a duplicate
    // that is also unsearchable.
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate()]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByRole('button', { name: 'Register anyway' })).toBeEnabled();
  });

  it('confirms and cancels through the callbacks it was given', async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    const onCancel = vi.fn();

    render(
      <DuplicateWarningDialog
        candidates={[aCandidate()]}
        onConfirm={onConfirm}
        onCancel={onCancel}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Register anyway' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);

    await user.click(screen.getByRole('button', { name: 'Go back and check' }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it('offers no way to select a candidate', () => {
    // Merge tooling is Phase 2 (plan section 11). A row that looked selectable but only navigated
    // would be worse than no action at all, and E-27's "never auto-select" is structural here:
    // there is nothing to select.
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate(), aCandidate({ id: 'existing-2' })]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getAllByRole('button')).toHaveLength(2);
    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
  });

  // --- E-27: a phone is a household identifier ---------------------------

  it('separates suspected duplicates from other patients on the same phone', () => {
    // E-27. Three Kumars on one number is ordinary. Showing the relatives as context rather than as
    // warnings is what stops the dialog from crying wolf while still showing the whole picture.
    render(
      <DuplicateWarningDialog
        candidates={[
          aCandidate({ id: 'a', fullName: 'Ravi Kumar', isLikelyDuplicate: true }),
          aCandidate({
            id: 'b',
            fullName: 'Sunita Kumar',
            isLikelyDuplicate: false,
            nameSimilarity: 0.5,
          }),
          aCandidate({
            id: 'c',
            fullName: 'Anil Kumar',
            isLikelyDuplicate: false,
            nameSimilarity: 0.4,
          }),
        ]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByText('This patient may already be registered')).toBeInTheDocument();
    expect(screen.getByText('2 other patients use this phone number')).toBeInTheDocument();

    // All three are on screen - none is hidden.
    expect(screen.getByText('Ravi Kumar')).toBeInTheDocument();
    expect(screen.getByText('Sunita Kumar')).toBeInTheDocument();
    expect(screen.getByText('Anil Kumar')).toBeInTheDocument();
  });

  it('counts only the suspected duplicates in its heading', () => {
    render(
      <DuplicateWarningDialog
        candidates={[
          aCandidate({ id: 'a', isLikelyDuplicate: true }),
          aCandidate({ id: 'b', fullName: 'Ravi Kumaar', isLikelyDuplicate: true }),
          aCandidate({ id: 'c', fullName: 'Sunita Kumar', isLikelyDuplicate: false }),
        ]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    // Not "3 patients": overstating the problem is how a warning gets discounted.
    expect(screen.getByText('2 patients on file may be this person')).toBeInTheDocument();
  });

  it('is announced as a modal dialog', () => {
    render(
      <DuplicateWarningDialog
        candidates={[aCandidate()]}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />,
    );

    expect(screen.getByRole('dialog')).toHaveAttribute('aria-modal', 'true');
  });
});

describe('duplicateCandidatesFrom (F-6)', () => {
  it('reads the candidates out of a duplicate 409', () => {
    const error = new ProblemDetailsError(
      {
        status: 409,
        title: 'Conflict',
        ruleType: 'duplicate-confirmation-required',
        candidates: [aCandidate()],
      },
      409,
    );

    expect(duplicateCandidatesFrom(error)).toHaveLength(1);
  });

  it('returns null for any other error', () => {
    // The form branches on this to decide between a dialog and an error banner, so a false positive
    // here would open an empty dialog and swallow a real failure (E-47).
    expect(duplicateCandidatesFrom(new Error('network'))).toBeNull();

    expect(
      duplicateCandidatesFrom(
        new ProblemDetailsError({ status: 409, ruleType: 'merge-cycle' }, 409),
      ),
    ).toBeNull();

    expect(
      duplicateCandidatesFrom(new ProblemDetailsError({ status: 400, title: 'Bad' }, 400)),
    ).toBeNull();
  });

  it('returns null when the 409 carries no usable candidate list', () => {
    // The candidates are an RFC-7807 extension and therefore untyped on the wire. A dialog with no
    // rows in it tells the physician nothing and hides the failure; the error banner is the honest
    // fallback.
    for (const candidates of [undefined, null, [], 'not-an-array', {}]) {
      const error = new ProblemDetailsError(
        { status: 409, ruleType: 'duplicate-confirmation-required', candidates },
        409,
      );

      expect(duplicateCandidatesFrom(error)).toBeNull();
    }
  });
});
