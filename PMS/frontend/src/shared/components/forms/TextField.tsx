import { forwardRef, useId, type InputHTMLAttributes } from 'react';

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  /** Visible label. Always rendered - a placeholder is not a label. */
  label: string;
  /** Validation or hint text shown under the input. */
  error?: string;
}

/**
 * The text input every form in this application uses.
 *
 * **E-65 (browser autofill / cached form data on the clinic machine).** The consulting-room
 * PC is shared across every patient of the day. If the browser is allowed to remember what was
 * typed into a field, the previous patient's name, phone number or complaint can be offered as
 * a dropdown suggestion while the next patient is sitting in front of the screen - a PHI
 * disclosure with no bug behind it. So `autoComplete` defaults to `off` here, once, rather
 * than being remembered on each of the dozens of inputs F-5 through F-13 will add.
 *
 * A caller can still pass `autoComplete` explicitly, because a future non-patient field (a
 * clinic address in F-3, say) may legitimately want it. That has to be a visible, reviewable
 * decision at the call site, which is the point of making `off` the default rather than the
 * rule.
 */
export const TextField = forwardRef<HTMLInputElement, TextFieldProps>(function TextField(
  { label, error, autoComplete = 'off', className, ...rest },
  ref,
) {
  const generatedId = useId();
  const inputId = rest.name ? `field-${rest.name}` : generatedId;
  const errorId = `${inputId}-error`;

  return (
    <div className={className ? `field ${className}` : 'field'}>
      <label className="field__label" htmlFor={inputId}>
        {label}
      </label>
      <input
        {...rest}
        id={inputId}
        ref={ref}
        className="field__input"
        autoComplete={autoComplete}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : rest['aria-describedby']}
      />
      {error ? (
        <p className="field__error" id={errorId}>
          {error}
        </p>
      ) : null}
    </div>
  );
});
