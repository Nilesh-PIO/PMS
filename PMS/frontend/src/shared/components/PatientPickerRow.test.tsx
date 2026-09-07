import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PatientPickerRow, type PatientPickerRowProps } from './PatientPickerRow';

/**
 * E-28 / RSK-12, asserted at the component that is supposed to make them impossible.
 *
 * "Never show a name alone in a selection list" is only a real rule if something fails when it is
 * broken. These are that something.
 *
 * **MERGE NOTE (F-6 + F-7).** F-6 and F-7 each built a `PatientPickerRow`; they are now one
 * component with flat props, so these tests pass props directly rather than a `PatientSummary`
 * object. The last two blocks cover the shapes each feature actually supplies — F-7's summary
 * (no date of birth, a registration date, selectable) and F-6's duplicate candidate (a date of
 * birth, no registration date, inert).
 */

/** The shape F-7's search and recent lists supply — `PatientSummary` spread onto the row. */
function aPatientSummary(
  overrides: Partial<PatientPickerRowProps> = {},
): PatientPickerRowProps {
  return {
    id: 'patient-1',
    fullName: 'Ravi Kumar',
    phoneTail: '3210',
    ageDisplay: '41',
    gender: 'Male',
    lastVisitDate: null,
    registeredOn: '2026-09-07',
    status: 'Active',
    isMerged: false,
    isProfileIncomplete: false,
    matchKind: 'Name',
    ...overrides,
  };
}

describe('PatientPickerRow - the wrong-patient guard (E-28, RSK-12)', () => {
  it('renders name, phone tail, age and a date on every row', () => {
    render(<PatientPickerRow {...aPatientSummary()} onSelect={vi.fn()} />);

    expect(screen.getByText('Ravi Kumar')).toBeInTheDocument();
    expect(screen.getByText(/3210/)).toBeInTheDocument();
    expect(screen.getByText('41')).toBeInTheDocument();
    expect(screen.getByText(/Registered/)).toBeInTheDocument();
  });

  it('says "no phone recorded" rather than leaving the field blank', () => {
    // A blank cell is indistinguishable from a rendering bug, and the reader cannot tell whether
    // the number is missing or the row is broken.
    render(<PatientPickerRow {...aPatientSummary({ phoneTail: null })} onSelect={vi.fn()} />);

    expect(screen.getByText(/No phone recorded/i)).toBeInTheDocument();
  });

  it('shows the last visit date when one exists, and labels it as a visit', () => {
    // Pins the F-10 hand-off: the day LastVisitDate starts arriving, the row must say "Last visit"
    // and not keep showing the registration date.
    render(
      <PatientPickerRow
        {...aPatientSummary({ lastVisitDate: '2026-08-14' })}
        onSelect={vi.fn()}
      />,
    );

    expect(screen.getByText(/Last visit/)).toBeInTheDocument();
    expect(screen.queryByText(/Registered/)).not.toBeInTheDocument();
  });

  it('distinguishes two patients who share a name and an age (E-28, acceptance criterion 2)', () => {
    const dobTwin = { fullName: 'Ravi Kumar', ageDisplay: '41' };

    render(
      <>
        <PatientPickerRow
          {...aPatientSummary({ ...dobTwin, id: 'a', phoneTail: '3210', registeredOn: '2026-01-05' })}
          onSelect={vi.fn()}
        />
        <PatientPickerRow
          {...aPatientSummary({ ...dobTwin, id: 'b', phoneTail: '9999', registeredOn: '2026-08-20' })}
          onSelect={vi.fn()}
        />
      </>,
    );

    // Same name, same age...
    expect(screen.getAllByText('Ravi Kumar')).toHaveLength(2);
    expect(screen.getAllByText('41')).toHaveLength(2);

    // ...and still two rows a human can tell apart, on two independent axes.
    expect(screen.getByText(/3210/)).toBeInTheDocument();
    expect(screen.getByText(/9999/)).toBeInTheDocument();
    expect(screen.getByText(/5 Jan 2026/)).toBeInTheDocument();
    expect(screen.getByText(/20 Aug 2026/)).toBeInTheDocument();
  });

  it('gives a screen-reader user the same disambiguators a sighted user reads', () => {
    // Otherwise an assistive technology announces a list of identical "Ravi Kumar" buttons, which
    // is the name-only picker again with extra steps.
    render(<PatientPickerRow {...aPatientSummary()} onSelect={vi.fn()} />);

    const label = screen.getByRole('button').getAttribute('aria-label')!;
    expect(label).toContain('Ravi Kumar');
    expect(label).toContain('3210');
    expect(label).toContain('41');
  });

  it('never selects itself - selection requires an actual click', async () => {
    const onSelect = vi.fn();
    render(<PatientPickerRow {...aPatientSummary()} onSelect={onSelect} />);

    // Rendered, and nothing has happened.
    expect(onSelect).not.toHaveBeenCalled();

    await userEvent.click(screen.getByRole('button'));
    expect(onSelect).toHaveBeenCalledExactlyOnceWith('patient-1');
  });

  it('flags an inactive record rather than letting it look ordinary', () => {
    render(<PatientPickerRow {...aPatientSummary({ status: 'Inactive' })} onSelect={vi.fn()} />);

    expect(screen.getByText('Inactive')).toBeInTheDocument();
  });

  it('flags a merged record, because attaching work to it splits a history', () => {
    render(<PatientPickerRow {...aPatientSummary({ isMerged: true })} onSelect={vi.fn()} />);

    expect(screen.getByText('Merged')).toBeInTheDocument();
  });

  it('marks a fuzzy match as a similar name and not as a find (E-30)', () => {
    render(
      <PatientPickerRow {...aPatientSummary({ matchKind: 'SimilarName' })} onSelect={vi.fn()} />,
    );

    expect(screen.getByText(/Similar name/i)).toBeInTheDocument();
    expect(screen.getByRole('button').getAttribute('aria-label')).toContain('not an exact match');
  });

  it('surfaces an incomplete profile at the moment of choosing, not only after opening it', () => {
    render(
      <PatientPickerRow {...aPatientSummary({ isProfileIncomplete: true })} onSelect={vi.fn()} />,
    );

    expect(screen.getByText(/Profile incomplete/i)).toBeInTheDocument();
  });

  it('renders as a non-interactive row when no select handler is given', () => {
    render(<PatientPickerRow {...aPatientSummary()} />);

    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    // Still carries every disambiguating field - a display-only row is not an excuse to drop them.
    expect(screen.getByText(/3210/)).toBeInTheDocument();
    expect(screen.getByText('41')).toBeInTheDocument();
  });

  // --- the two callers' shapes, on one component (F-6 + F-7 merge) --------

  it("renders F-6's shape: a date of birth, no registration date, and no visits yet", () => {
    // A DuplicateCandidate carries a date of birth and no registeredOn. The row has to show the
    // date of birth (F-6 acceptance criterion 4 asks for name, phone tail, age/DOB and last visit)
    // and has to say "No visits recorded" rather than silently dropping the fourth field.
    render(
      <PatientPickerRow
        id="existing-1"
        fullName="Ravi Kumar"
        phoneTail="3210"
        ageDisplay="41"
        dateOfBirth="1985-03-02"
        lastVisitDate={null}
        status="Active"
      />,
    );

    expect(screen.getByText('Date of birth')).toBeInTheDocument();
    expect(screen.getByText('2 Mar 1985')).toBeInTheDocument();
    expect(screen.getByText('No visits recorded')).toBeInTheDocument();
  });

  it("omits the date-of-birth fact entirely for a caller whose DTO has no such field", () => {
    // undefined and null are different claims. F-7's PatientSummary carries no date of birth at
    // all, so asserting "Not recorded" for it would be the row inventing an absence nobody
    // reported; a caller that does carry the field and has none says so explicitly.
    render(<PatientPickerRow {...aPatientSummary()} onSelect={vi.fn()} />);
    expect(screen.queryByText('Date of birth')).not.toBeInTheDocument();

    render(<PatientPickerRow {...aPatientSummary({ dateOfBirth: null })} onSelect={vi.fn()} />);
    expect(screen.getByText('Date of birth')).toBeInTheDocument();
    expect(screen.getByText('Not recorded')).toBeInTheDocument();
  });
});
