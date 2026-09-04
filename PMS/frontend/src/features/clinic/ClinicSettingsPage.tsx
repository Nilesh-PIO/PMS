import { useEffect, useState, type FormEvent } from 'react';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { PatientDataForm } from '../../shared/components/forms/PatientDataForm';
import { TextField } from '../../shared/components/forms/TextField';
import { useSaveSettingOptions, useSettingOptions } from './useClinicSettings';
import {
  CLINIC_SETTINGS_LIMITS,
  SETTING_CATEGORY_LABELS,
  SettingCategory,
  type SettingCategoryValue,
  type SettingOptionItemRequest,
} from './types/clinicSettings';

/**
 * The doctor-configured lists (route `/settings/options`,
 * planning-pms-verification.md, F-4 point 4).
 *
 * Both lists are edited on one screen because they are the same kind of thing and neither is more
 * than a handful of rows.
 */
export function ClinicSettingsPage() {
  return (
    <section className="clinic-page">
      <h1>Lists</h1>
      <p className="clinic-page__intro">
        The values offered in dropdowns elsewhere in the application. Editing a list never changes
        what is already recorded on a patient or a past consultation.
      </p>

      <OptionListEditor category={SettingCategory.Gender} />
      <OptionListEditor category={SettingCategory.VitalsNotRecordedReason} />
    </section>
  );
}

interface EditableOption {
  value: string;
  isActive: boolean;
  isProtected: boolean;
}

/**
 * One category's list.
 *
 * **There is no delete control, and that is deliberate** (plan F-4 point 5). A patient row stores
 * the option's text, not a key, so deleting the row would leave a 2026 record displaying a value
 * that appears nowhere in settings. Clearing "Offered" retires the option instead: it disappears
 * from every dropdown and stays legible in history. The screen says so, because a physician who
 * expects a delete button and cannot find one deserves the reason rather than a missing feature.
 */
function OptionListEditor({ category }: { category: SettingCategoryValue }) {
  const { title, description } = SETTING_CATEGORY_LABELS[category];
  const query = useSettingOptions(category, true);
  const save = useSaveSettingOptions(category);

  const [items, setItems] = useState<EditableOption[] | null>(null);
  const [newValue, setNewValue] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  // Seeded from the server list once it arrives, and then owned locally: this is an editor, and
  // a background refetch overwriting half-finished edits is the kind of thing that makes someone
  // stop trusting a settings screen.
  useEffect(() => {
    if (query.data && items === null) {
      setItems(
        query.data.map((option) => ({
          value: option.value,
          isActive: option.isActive,
          isProtected: option.isProtected,
        })),
      );
    }
  }, [query.data, items]);

  const fieldErrors = isProblemDetailsError(save.error) ? save.error.fieldErrors : {};
  const listError = fieldErrors.Items?.[0];

  const move = (index: number, delta: number) => {
    setItems((current) => {
      if (!current) {
        return current;
      }
      const target = index + delta;
      if (target < 0 || target >= current.length) {
        return current;
      }
      const next = [...current];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  };

  const setActive = (index: number, isActive: boolean) => {
    setItems((current) =>
      current?.map((item, i) => (i === index ? { ...item, isActive } : item)) ?? current,
    );
  };

  const setValue = (index: number, value: string) => {
    setItems((current) =>
      current?.map((item, i) => (i === index ? { ...item, value } : item)) ?? current,
    );
  };

  const addOption = () => {
    const value = newValue.trim();
    setLocalError(null);

    if (value.length === 0) {
      setLocalError('Type the option first.');
      return;
    }

    // Checked here as well as on the server. Not a substitute for the server rule (C-20 is
    // enforced by a unique index and by the service) - it is what stops the physician typing a
    // duplicate, saving, and reading an error about a row they can no longer see on screen.
    if (items?.some((item) => item.value.toLowerCase() === value.toLowerCase())) {
      setLocalError(`"${value}" is already in this list.`);
      return;
    }

    setItems((current) => [...(current ?? []), { value, isActive: true, isProtected: false }]);
    setNewValue('');
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!items) {
      return;
    }

    const payload: SettingOptionItemRequest[] = items.map((item) => ({
      value: item.value,
      isActive: item.isActive,
    }));

    try {
      const saved = await save.mutateAsync(payload);
      // Re-seeded from the server answer, so the display order and any retirement the server
      // applied are what the screen now shows - not what the client hoped it applied.
      setItems(
        saved.map((option) => ({
          value: option.value,
          isActive: option.isActive,
          isProtected: option.isProtected,
        })),
      );
    } catch {
      // Rendered from `save.error` below.
    }
  };

  if (query.isPending) {
    return (
      <section className="settings-list">
        <h2>{title}</h2>
        <p className="app-status" role="status">
          Loading...
        </p>
      </section>
    );
  }

  if (query.isError) {
    return (
      <section className="settings-list">
        <h2>{title}</h2>
        <div className="app-status app-status--error" role="alert">
          <p>
            {isProblemDetailsError(query.error)
              ? query.error.userMessage
              : 'Could not load this list. Check the connection and try again.'}
          </p>
        </div>
      </section>
    );
  }

  return (
    <section className="settings-list" aria-labelledby={`heading-${category}`}>
      <h2 id={`heading-${category}`}>{title}</h2>
      <p className="settings-list__intro">{description}</p>

      <PatientDataForm onSubmit={handleSubmit} noValidate className="settings-list__form">
        {listError ? (
          <p className="clinic-form__error" role="alert">
            {listError}
          </p>
        ) : null}

        <ol className="settings-list__items">
          {(items ?? []).map((item, index) => (
            <li className="settings-list__item" key={`${category}-${index}`}>
              <TextField
                label={`Option ${index + 1}`}
                name={`${category}-value-${index}`}
                value={item.value}
                onChange={(event) => setValue(index, event.target.value)}
                maxLength={CLINIC_SETTINGS_LIMITS.optionValue}
                error={fieldErrors[`Items[${index}].Value`]?.[0]}
                readOnly={item.isProtected}
              />

              <label className="settings-list__offered">
                <input
                  type="checkbox"
                  checked={item.isActive}
                  disabled={item.isProtected}
                  onChange={(event) => setActive(index, event.target.checked)}
                  aria-label={`Offer "${item.value}"`}
                />
                Offered
              </label>

              <div className="settings-list__reorder">
                <button
                  className="button button--quiet"
                  type="button"
                  onClick={() => move(index, -1)}
                  disabled={index === 0}
                  aria-label={`Move "${item.value}" up`}
                >
                  Up
                </button>
                <button
                  className="button button--quiet"
                  type="button"
                  onClick={() => move(index, 1)}
                  disabled={index === (items?.length ?? 0) - 1}
                  aria-label={`Move "${item.value}" down`}
                >
                  Down
                </button>
              </div>

              {item.isProtected ? (
                <p className="settings-list__note">
                  Always offered. Without it, an unknown value has to be guessed, and the guess
                  becomes permanent.
                </p>
              ) : null}
            </li>
          ))}
        </ol>

        <div className="settings-list__add">
          <TextField
            label={`Add an option to ${title.toLowerCase()}`}
            name={`${category}-new`}
            value={newValue}
            onChange={(event) => setNewValue(event.target.value)}
            maxLength={CLINIC_SETTINGS_LIMITS.optionValue}
            error={localError ?? undefined}
          />
          <button className="button button--quiet" type="button" onClick={addOption}>
            Add
          </button>
        </div>

        <p className="settings-list__note">
          Nothing here is ever deleted. Clearing "Offered" takes an option out of the dropdown and
          leaves every record that already uses it exactly as it was.
        </p>

        <button className="button button--primary" type="submit" disabled={save.isPending}>
          {save.isPending ? 'Saving...' : `Save ${title.toLowerCase()}`}
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
