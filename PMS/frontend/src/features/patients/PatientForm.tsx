import { useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { PatientDataForm } from '../../shared/components/forms/PatientDataForm';
import { TextField } from '../../shared/components/forms/TextField';
import { useSubmitOnce } from '../../shared/hooks/useSubmitOnce';
import { useSettingOptions } from '../clinic/useClinicSettings';
import { useCreatePatient } from './usePatients';
import type { AgeMode, CreatePatientRequest } from './types/patient';

/**
 * Patient registration (route `/patients/new`, planning-pms-verification.md, F-5 point 4;
 * brainstorm C-18, E-8, E-9, E-13, E-20, E-21, E-43, E-46).
 *
 * **Four things this screen is careful about.**
 *
 * 1. *One name field, and only the name is required* (C-18, E-13). No surname box, no minimum word
 *    count, no script rule. Every extra requirement here is a real patient the front desk has to
 *    invent data for, and invented data is worse than absent data.
 * 2. *Age is a three-way choice, not two optional boxes* (E-9). A date of birth and an approximate
 *    age are mutually exclusive on the server, so offering both as free-standing inputs would let
 *    the physician fill in two and be told off for it. Choosing "approximate" also makes it visible
 *    that the value will be stored *with today's date attached* — which is the whole mechanism that
 *    stops it becoming a lie later (E-21).
 * 3. *Saving with almost nothing is allowed, and says so before you do it* (E-8, E-20). The form
 *    tells you the profile will be marked incomplete rather than refusing the save.
 * 4. *Save cannot fire twice* (E-43, E-46). `useSubmitOnce` disables the button and — more
 *    importantly — sends a submission token, because the button is not what stops a retried
 *    request.
 */
export function PatientForm() {
  const navigate = useNavigate();
  const create = useCreatePatient();
  const submitOnce = useSubmitOnce();
  const [searchParams] = useSearchParams();

  // Only the active options: a gender the clinic has retired must not be selectable for a *new*
  // record, even though existing records keep displaying it (F-4's integrity rule 2).
  const genderOptions = useSettingOptions('Gender');

  // F-7 / E-7. A search that found nothing offers "Register '<typed text>' as a new patient", and
  // this is where the typed text lands. Retyping a name that is already on screen is exactly the
  // friction that makes someone abbreviate it differently the second time - which is how a
  // near-duplicate gets created by the workflow meant to prevent one.
  //
  // Read as lazy state initialisers, not as derived values: after the first render this is an
  // ordinary editable form, and a later URL change must not overwrite what is being typed.
  const [fullName, setFullName] = useState(() => searchParams.get('name')?.trim() ?? '');
  const [ageMode, setAgeMode] = useState<AgeMode>('unknown');
  const [dateOfBirth, setDateOfBirth] = useState('');
  const [approxAgeYears, setApproxAgeYears] = useState('');
  const [gender, setGender] = useState('');
  // A search query that was all digits prefills the *phone*, not the name - someone who searched a
  // phone number and found nobody knows the number, not the spelling.
  const [primaryPhone, setPrimaryPhone] = useState(() => searchParams.get('phone')?.trim() ?? '');
  const [altContact, setAltContact] = useState('');
  const [localErrors, setLocalErrors] = useState<Record<string, string>>({});

  const fieldErrors = isProblemDetailsError(create.error) ? create.error.fieldErrors : {};

  const willBeIncomplete =
    primaryPhone.trim() === '' || gender.trim() === '' || ageMode === 'unknown';

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const errors: Record<string, string> = {};

    if (fullName.trim() === '') {
      errors.fullName = "Enter the patient's name.";
    }

    if (ageMode === 'dateOfBirth' && dateOfBirth.trim() === '') {
      errors.dateOfBirth = 'Enter the date of birth, or choose another option.';
    }

    if (ageMode === 'approximate') {
      const parsed = Number(approxAgeYears);
      if (approxAgeYears.trim() === '' || !Number.isInteger(parsed) || parsed < 0) {
        errors.approxAgeYears = 'Enter the approximate age in whole years.';
      }
    }

    setLocalErrors(errors);
    if (Object.keys(errors).length > 0) {
      return;
    }

    // Exactly one age shape leaves this form. The server enforces the same rule and a check
    // constraint enforces it again; this is simply the layer that makes the rule obvious rather
    // than the layer that guarantees it.
    const request: CreatePatientRequest = {
      fullName,
      dateOfBirth: ageMode === 'dateOfBirth' ? dateOfBirth : null,
      approxAgeYears: ageMode === 'approximate' ? Number(approxAgeYears) : null,
      gender: gender.trim() === '' ? null : gender,
      primaryPhone: primaryPhone.trim() === '' ? null : primaryPhone,
      altContact: altContact.trim() === '' ? null : altContact,
    };

    await submitOnce.submit(async (submissionId) => {
      try {
        const created = await create.mutateAsync({ ...request, submissionId });
        // A fresh token, so the next registration on this screen is a new intent rather than a
        // replay that would be answered with the patient just created.
        submitOnce.reset();
        navigate(`/patients/${created.id}`);
      } catch {
        // Rendered from `create.error` below. Nothing is cleared: every typed character stays on
        // screen so the physician can correct one field rather than retype the form (E-47).
      }
    });
  };

  const formError =
    create.isError && isProblemDetailsError(create.error) && create.error.status !== 400
      ? create.error.userMessage
      : null;

  return (
    <section className="patients-page">
      <h1>Register a patient</h1>

      <PatientDataForm onSubmit={handleSubmit} noValidate className="patient-form">
        {formError ? (
          <p className="clinic-form__error" role="alert">
            {formError}
          </p>
        ) : null}

        <TextField
          label="Full name"
          name="fullName"
          value={fullName}
          onChange={(event) => setFullName(event.target.value)}
          required
          error={localErrors.fullName ?? fieldErrors.FullName?.[0]}
        />
        <p className="field__hint">
          The whole name, however the patient gives it. A single name is fine.
        </p>

        <fieldset className="patient-form__age">
          <legend>Age</legend>

          <label className="patient-form__choice">
            <input
              type="radio"
              name="ageMode"
              value="dateOfBirth"
              checked={ageMode === 'dateOfBirth'}
              onChange={() => setAgeMode('dateOfBirth')}
            />
            Date of birth is known
          </label>

          {ageMode === 'dateOfBirth' ? (
            <TextField
              label="Date of birth"
              name="dateOfBirth"
              type="date"
              value={dateOfBirth}
              onChange={(event) => setDateOfBirth(event.target.value)}
              error={localErrors.dateOfBirth ?? fieldErrors.DateOfBirth?.[0]}
            />
          ) : null}

          <label className="patient-form__choice">
            <input
              type="radio"
              name="ageMode"
              value="approximate"
              checked={ageMode === 'approximate'}
              onChange={() => setAgeMode('approximate')}
            />
            Only an approximate age is known
          </label>

          {ageMode === 'approximate' ? (
            <>
              <TextField
                label="Approximate age in years"
                name="approxAgeYears"
                inputMode="numeric"
                value={approxAgeYears}
                onChange={(event) => setApproxAgeYears(event.target.value)}
                error={localErrors.approxAgeYears ?? fieldErrors.ApproxAgeYears?.[0]}
              />
              {/*
                Stated on screen, not just implied. The physician should know the record will carry
                "about 40, recorded this year" rather than a bare 40 - it is the difference between
                a record that stays honest and one that quietly becomes wrong (E-21).
              */}
              <p className="field__hint">
                This is saved with today&apos;s date attached and always shown as an estimate, for
                example &ldquo;~40 (recorded 2026)&rdquo;. It is never treated as an exact age.
              </p>
            </>
          ) : null}

          <label className="patient-form__choice">
            <input
              type="radio"
              name="ageMode"
              value="unknown"
              checked={ageMode === 'unknown'}
              onChange={() => setAgeMode('unknown')}
            />
            Not known
          </label>
        </fieldset>

        <div className="field">
          <label className="field__label" htmlFor="field-gender">
            Gender
          </label>
          <select
            id="field-gender"
            name="gender"
            className="field__input"
            value={gender}
            onChange={(event) => setGender(event.target.value)}
          >
            <option value="">Not answered</option>
            {(genderOptions.data ?? []).map((option) => (
              <option key={option.id} value={option.value}>
                {option.value}
              </option>
            ))}
          </select>
          {fieldErrors.Gender?.[0] ? (
            <p className="field__error">{fieldErrors.Gender[0]}</p>
          ) : null}
        </div>

        <TextField
          label="Phone number"
          name="primaryPhone"
          inputMode="tel"
          value={primaryPhone}
          onChange={(event) => setPrimaryPhone(event.target.value)}
          error={fieldErrors.PrimaryPhone?.[0]}
        />
        <p className="field__hint">
          Optional, but it is how the clinic finds and contacts this patient later. Any format is
          accepted.
        </p>

        <TextField
          label="Alternate contact"
          name="altContact"
          value={altContact}
          onChange={(event) => setAltContact(event.target.value)}
          error={fieldErrors.AltContact?.[0]}
        />

        {willBeIncomplete ? (
          <p className="patient-form__notice" role="status">
            This profile will be saved and marked <strong>incomplete</strong>, so the missing
            details are visible later. You can add them at any time.
          </p>
        ) : null}

        <button
          className="button button--primary"
          type="submit"
          disabled={submitOnce.isSubmitting}
        >
          {submitOnce.isSubmitting ? 'Saving...' : 'Register patient'}
        </button>
      </PatientDataForm>
    </section>
  );
}
