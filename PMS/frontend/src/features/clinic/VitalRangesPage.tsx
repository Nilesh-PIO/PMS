import { useEffect, useState, type FormEvent } from 'react';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { PatientDataForm } from '../../shared/components/forms/PatientDataForm';
import { TextField } from '../../shared/components/forms/TextField';
import { useSaveVitalRanges, useVitalRanges } from './useClinicSettings';
import {
  formatThreshold,
  parseThreshold,
  type VitalMetricValue,
  type VitalRangeItemRequest,
} from './types/clinicSettings';

/**
 * The doctor-defined plausibility thresholds (route `/settings/vitals-ranges`,
 * planning-pms-verification.md, F-4 point 4; brainstorm E-12).
 *
 * **Two things this screen is careful about.**
 *
 * 1. *A blank box means silence, not zero.* `Number('')` is `0` in JavaScript, and a threshold of
 *    zero would warn on every reading the clinic ever enters - the exact opposite of "leave me
 *    alone about this one". Every empty input is sent as `null`, and a test pins it (plan F-4
 *    point 6: "blank threshold submits as null, not zero").
 * 2. *These are the physician's numbers, not the software's.* The application ships with this
 *    table empty and never fills it in. The copy on screen says so, because a doctor who assumes
 *    the software already knows what a dangerous pulse is would be relying on something that does
 *    not exist (plan section 7, "Clinical-rule boundary"; C-31).
 */
export function VitalRangesPage() {
  const query = useVitalRanges();
  const save = useSaveVitalRanges();

  const [draft, setDraft] = useState<Record<string, { low: string; high: string }> | null>(null);
  const [localErrors, setLocalErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (query.data && draft === null) {
      const next: Record<string, { low: string; high: string }> = {};
      for (const range of query.data) {
        next[range.metric] = {
          low: formatThreshold(range.warnLow),
          high: formatThreshold(range.warnHigh),
        };
      }
      setDraft(next);
    }
  }, [query.data, draft]);

  const fieldErrors = isProblemDetailsError(save.error) ? save.error.fieldErrors : {};

  const setBound = (metric: VitalMetricValue, bound: 'low' | 'high', value: string) => {
    setDraft((current) =>
      current ? { ...current, [metric]: { ...current[metric], [bound]: value } } : current,
    );
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!query.data || !draft) {
      return;
    }

    const errors: Record<string, string> = {};
    const items: VitalRangeItemRequest[] = [];

    for (const range of query.data) {
      const entry = draft[range.metric] ?? { low: '', high: '' };
      const low = parseThreshold(entry.low);
      const high = parseThreshold(entry.high);

      if (low === 'invalid') {
        errors[`${range.metric}-low`] = 'Enter a number, or leave it blank for no warning.';
      }
      if (high === 'invalid') {
        errors[`${range.metric}-high`] = 'Enter a number, or leave it blank for no warning.';
      }
      if (low === 'invalid' || high === 'invalid') {
        continue;
      }
      if (low !== null && high !== null && low > high) {
        errors[`${range.metric}-low`] = 'The lower limit must not be above the upper limit.';
        continue;
      }

      // `null`, never `0` - see the component docstring.
      items.push({ metric: range.metric, warnLow: low, warnHigh: high });
    }

    setLocalErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    try {
      const saved = await save.mutateAsync(items);
      const next: Record<string, { low: string; high: string }> = {};
      for (const range of saved) {
        next[range.metric] = {
          low: formatThreshold(range.warnLow),
          high: formatThreshold(range.warnHigh),
        };
      }
      setDraft(next);
    } catch {
      // Rendered from `save.error` below.
    }
  };

  if (query.isPending) {
    return (
      <p className="app-status" role="status">
        Loading the vital ranges...
      </p>
    );
  }

  if (query.isError) {
    return (
      <div className="app-status app-status--error" role="alert">
        <p>
          {isProblemDetailsError(query.error)
            ? query.error.userMessage
            : 'Could not load the vital ranges. Check the connection and try again.'}
        </p>
      </div>
    );
  }

  const formError =
    save.isError && isProblemDetailsError(save.error) && save.error.status !== 400
      ? save.error.userMessage
      : null;

  return (
    <section className="clinic-page">
      <h1>Vital ranges</h1>
      <p className="clinic-page__intro">
        Values outside a range you set here raise a warning during a consultation. The warning can
        always be confirmed and the reading saved exactly as entered - it never blocks anything.
      </p>
      <p className="clinic-page__intro">
        <strong>These are your numbers.</strong> This application ships with no ranges at all and
        will never suggest one. Leave a box empty and nothing is checked for that limit.
      </p>
      {/*
        Observed during the F-4 live smoke and worth saying on screen: changing the clinic's
        temperature unit on the clinic-profile screen does not convert a threshold typed here, in
        exactly the same way it does not convert temperatures already recorded (E-24, and the
        matching sentence on the clinic profile form). A threshold of 42 stays the number 42.
      */}
      <p className="clinic-page__intro">
        Temperature limits are in the unit shown beside the field. Changing the clinic&apos;s
        temperature unit does not convert a limit you have already set here — check them again if
        you change it.
      </p>

      <PatientDataForm onSubmit={handleSubmit} noValidate className="vital-ranges__form">
        {formError ? (
          <p className="clinic-form__error" role="alert">
            {formError}
          </p>
        ) : null}

        <ul className="vital-ranges__list">
          {query.data.map((range, index) => {
            const entry = draft?.[range.metric] ?? { low: '', high: '' };
            const unitSuffix = range.unit ? ` (${range.unit})` : '';
            const isUnset = entry.low.trim() === '' && entry.high.trim() === '';

            return (
              <li className="vital-ranges__row" key={range.metric}>
                <h2 className="vital-ranges__metric">
                  {range.label}
                  {unitSuffix}
                </h2>

                <TextField
                  label={`${range.label} lower limit`}
                  name={`warnLow-${range.metric}`}
                  value={entry.low}
                  onChange={(event) => setBound(range.metric, 'low', event.target.value)}
                  inputMode="decimal"
                  error={
                    localErrors[`${range.metric}-low`] ??
                    fieldErrors[`Items[${index}].WarnLow`]?.[0]
                  }
                />

                <TextField
                  label={`${range.label} upper limit`}
                  name={`warnHigh-${range.metric}`}
                  value={entry.high}
                  onChange={(event) => setBound(range.metric, 'high', event.target.value)}
                  inputMode="decimal"
                  error={
                    localErrors[`${range.metric}-high`] ??
                    fieldErrors[`Items[${index}].WarnHigh`]?.[0]
                  }
                />

                <p className="vital-ranges__state">
                  {isUnset
                    ? 'No warning set - any value is accepted without comment.'
                    : 'A reading outside these limits asks for confirmation.'}
                </p>
              </li>
            );
          })}
        </ul>

        <button className="button button--primary" type="submit" disabled={save.isPending}>
          {save.isPending ? 'Saving...' : 'Save vital ranges'}
        </button>

        {save.isSuccess && !save.isPending ? (
          <p className="settings-list__saved" role="status">
            Saved.
          </p>
        ) : null}
      </PatientDataForm>
    </section>
  );
}
