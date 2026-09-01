import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PatientDataForm } from './PatientDataForm';
import { TextField } from './TextField';

/**
 * F-2 acceptance criterion 5 and E-65: patient-data inputs render with `autocomplete="off"`.
 *
 * The shared primitives are what make that true for every form F-5 onward adds, so they are
 * what gets tested. Asserting it on one screen would only prove that screen.
 */
describe('patient-data form primitives (E-65)', () => {
  it('sets autocomplete="off" on the form', () => {
    const { container } = render(
      <PatientDataForm aria-label="Register patient">
        <TextField label="Full name" name="fullName" />
      </PatientDataForm>,
    );

    expect(container.querySelector('form')).toHaveAttribute('autocomplete', 'off');
  });

  it('sets autocomplete="off" on every field by default', () => {
    render(
      <PatientDataForm>
        <TextField label="Full name" name="fullName" />
        <TextField label="Phone" name="primaryPhone" type="tel" />
        <TextField label="Date of birth" name="dateOfBirth" type="date" />
      </PatientDataForm>,
    );

    expect(screen.getByLabelText('Full name')).toHaveAttribute('autocomplete', 'off');
    expect(screen.getByLabelText('Phone')).toHaveAttribute('autocomplete', 'off');
    expect(screen.getByLabelText('Date of birth')).toHaveAttribute('autocomplete', 'off');
  });

  it('lets a caller opt in explicitly, so the exception is visible at the call site', () => {
    render(<TextField label="Clinic address" name="clinicAddress" autoComplete="street-address" />);

    expect(screen.getByLabelText('Clinic address')).toHaveAttribute(
      'autocomplete',
      'street-address',
    );
  });

  it('gives every field a real label tied to its input', () => {
    render(<TextField label="Full name" name="fullName" />);

    const input = screen.getByLabelText('Full name');
    expect(input.tagName).toBe('INPUT');
    expect(input).toHaveAttribute('id');
  });

  it('marks a field invalid and links its message when there is an error', () => {
    render(<TextField label="Full name" name="fullName" error="Enter the patient's name." />);

    const input = screen.getByLabelText('Full name');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByText("Enter the patient's name.")).toHaveAttribute(
      'id',
      input.getAttribute('aria-describedby'),
    );
  });

  it('has no error markup when there is no error', () => {
    render(<TextField label="Full name" name="fullName" />);

    expect(screen.getByLabelText('Full name')).not.toHaveAttribute('aria-invalid');
  });

  it('forwards the value and change handler through to the input', () => {
    render(<TextField label="Full name" name="fullName" value="Asha Rao" readOnly />);

    expect(screen.getByLabelText('Full name')).toHaveValue('Asha Rao');
  });
});
