import { forwardRef, useId, type TextareaHTMLAttributes } from 'react';

export interface TextAreaFieldProps
  extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'id'> {
  /** Visible label. Always rendered - a placeholder is not a label. */
  label: string;
  /** Validation or hint text shown under the control. */
  error?: string;
  /** Optional helper line, e.g. a character limit. */
  hint?: string;
}

/**
 * The multi-line sibling of {@link import('./TextField').TextField}, added by F-3 for the clinic
 * address and prescription footer.
 *
 * `autoComplete` defaults to `off` for exactly the reason it does on `TextField`: the
 * consulting-room PC is shared by every patient of the day, and a browser that remembers what was
 * typed here will offer it back while the next patient is sitting in front of the screen (E-65).
 */
export const TextAreaField = forwardRef<HTMLTextAreaElement, TextAreaFieldProps>(
  function TextAreaField({ label, error, hint, autoComplete = 'off', className, ...rest }, ref) {
    const generatedId = useId();
    const fieldId = rest.name ? `field-${rest.name}` : generatedId;
    const errorId = `${fieldId}-error`;
    const hintId = `${fieldId}-hint`;

    return (
      <div className={className ? `field ${className}` : 'field'}>
        <label className="field__label" htmlFor={fieldId}>
          {label}
        </label>
        <textarea
          {...rest}
          id={fieldId}
          ref={ref}
          className="field__input field__input--multiline"
          autoComplete={autoComplete}
          aria-invalid={error ? true : undefined}
          aria-describedby={
            [error ? errorId : null, hint ? hintId : null].filter(Boolean).join(' ') || undefined
          }
        />
        {hint ? (
          <p className="field__hint" id={hintId}>
            {hint}
          </p>
        ) : null}
        {error ? (
          <p className="field__error" id={errorId}>
            {error}
          </p>
        ) : null}
      </div>
    );
  },
);
