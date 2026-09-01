import type { FormHTMLAttributes, ReactNode } from 'react';

export interface PatientDataFormProps extends Omit<FormHTMLAttributes<HTMLFormElement>, 'autoComplete'> {
  children: ReactNode;
}

/**
 * The `<form>` wrapper every screen that captures patient data uses.
 *
 * **E-65.** `autoComplete="off"` is set on the form as well as on each
 * {@link import('./TextField').TextField}, because browsers apply the two at different levels:
 * a form-level opt-out suppresses the "save this form?" prompt and the whole-form autofill
 * offer, while the field-level one suppresses the per-field history dropdown. On a machine
 * shared by every patient of the day, neither is optional.
 *
 * F-2 establishes this as the shared convention; F-5 onward simply use it instead of each
 * re-deciding, which is what stops one forgotten form from reopening the exposure.
 */
export function PatientDataForm({ children, className, ...rest }: PatientDataFormProps) {
  return (
    <form
      {...rest}
      autoComplete="off"
      className={className ? `patient-form ${className}` : 'patient-form'}
    >
      {children}
    </form>
  );
}
