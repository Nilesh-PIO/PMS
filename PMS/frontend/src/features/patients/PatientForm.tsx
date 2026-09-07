import { useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { PatientDataForm } from '../../shared/components/forms/PatientDataForm';
import { TextField } from '../../shared/components/forms/TextField';
import { useSubmitOnce } from '../../shared/hooks/useSubmitOnce';
import { useSettingOptions } from '../clinic/useClinicSettings';
import { DuplicateWarningDialog, duplicateCandidatesFrom } from './DuplicateWarningDialog';
import { useDuplicateCheck } from './usePatientDuplicates';
import { useCreatePatient } from './usePatients';
import type { AgeMode, CreatePatientRequest, DuplicateCandidate } from './types/patient';

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
 *
 * **F-6 adds a fifth: the same person is not registered twice** (REC-2, E-25). The check runs
 * ahead of the physician as the name and phone are filled in, so the warning arrives while they are
 * still thinking about who this patient is rather than after they have moved on. The server runs the
 * same check again before it writes anything, and answers a duplicate with a 409 carrying the
 * candidates — so this screen has two paths into the dialog and neither of them is the guarantee on
 * its own. The advisory check can fail silently; the server's cannot be bypassed.
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

  // The candidates currently being shown. Null means no dialog is open - distinct from an empty
  // array, which would mean "a check ran and found nobody".
  const [pendingDuplicates, setPendingDuplicates] = useState<DuplicateCandidate[] | null>(null);

  const fieldErrors = isProblemDetailsError(create.error) ? create.error.fieldErrors : {};

  // The advisory check, running as the form is filled in (plan F-6 point 4: debounced 400 ms).
  // Deliberately not awaited by submit: the server re-runs it, so a slow or failed check delays
  // nothing and blocks nobody.
  const duplicateProbe = {
    fullName,
    phone: primaryPhone,
    dateOfBirth: ageMode === 'dateOfBirth' ? dateOfBirth : null,
  };
  const duplicates = useDuplicateCheck(duplicateProbe);
  const earlyWarnings = (duplicates.data ?? []).filter((c) => c.isLikelyDuplicate);

  const willBeIncomplete =
    primaryPhone.trim() === '' || gender.trim() === '' || ageMode === 'unknown';

  /**
   * The form's fields as the API wants them.
   *
   * Built in one place because two things now send it — the ordinary submit and the confirmation
   * from the duplicate dialog — and those two must send *identical* payloads. If they could drift,
   * the record created after a confirmation would differ from the one the physician was warned
   * about, which is a quiet way to save something nobody reviewed.
   *
   * Exactly one age shape leaves this form. The server enforces the same rule and a check
   * constraint enforces it again; this is the layer that makes the rule obvious, not the layer that
   * guarantees it.
   */
  const buildRequest = (): CreatePatientRequest => ({
    fullName,
    dateOfBirth: ageMode === 'dateOfBirth' ? dateOfBirth : null,
    approxAgeYears: ageMode === 'approximate' ? Number(approxAgeYears) : null,
    gender: gender.trim() === '' ? null : gender,
    primaryPhone: primaryPhone.trim() === '' ? null : primaryPhone,
    altContact: altContact.trim() === '' ? null : altContact,
  });

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

    await register(buildRequest(), false);
  };

  /**
   * Sends the registration, and turns a duplicate 409 into the dialog rather than an error message.
   *
   * The submission token is *not* regenerated between the refused attempt and the confirmed one:
   * both are the same intent to register one patient, and a new token would mean a retry after a
   * timeout could register them twice (E-43, E-46).
   */
  const register = async (request: CreatePatientRequest, confirmDuplicate: boolean) => {
    await submitOnce.submit(async (submissionId) => {
      try {
        const created = await create.mutateAsync({
          request: { ...request, submissionId },
          confirmDuplicate,
        });
        // A fresh token, so the next registration on this screen is a new intent rather than a
        // replay that would be answered with the patient just created.
        submitOnce.reset();
        setPendingDuplicates(null);
        navigate(`/patients/${created.id}`);
      } catch (error) {
        // F-6. A duplicate 409 is a question, not a failure, and it carries everything needed to
        // answer it. Rendering it as a red error message would be both wrong and useless - the
        // physician's next action is to look at the candidates, which are right here in the body.
        const candidates = duplicateCandidatesFrom(error);
        if (candidates) {
          setPendingDuplicates(candidates);
          return;
        }

        // Anything else is rendered from `create.error` below. Nothing is cleared: every typed
        // character stays on screen so the physician can correct one field rather than retype the
        // form (E-47).
      }
    });
  };

  const handleConfirmDuplicate = async () => {
    await register(buildRequest(), true);
  };

  // A duplicate 409 is excluded alongside the 400: it is answered by the dialog, and showing it as
  // an error banner underneath would tell the physician something failed when nothing did.
  const formError =
    create.isError &&
    isProblemDetailsError(create.error) &&
    create.error.status !== 400 &&
    !duplicateCandidatesFrom(create.error)
      ? create.error.userMessage
      : null;

  return (
    <section className="patients-page">
      <h1>Register a patient</h1>

      {pendingDuplicates ? (
        <DuplicateWarningDialog
          candidates={pendingDuplicates}
          onConfirm={handleConfirmDuplicate}
          onCancel={() => setPendingDuplicates(null)}
          isSubmitting={submitOnce.isSubmitting}
        />
      ) : null}

      <PatientDataForm onSubmit={handleSubmit} noValidate className="patient-form">
        {formError ? (
          <p className="clinic-form__error" role="alert">
            {formError}
          </p>
        ) : null}

        {/*
          The early warning, shown while the form is still being filled in rather than waiting for
          Save. Advisory only - it never disables anything, and the server refuses a duplicate
          independently, so a failed check costs a warning rather than a registration.
        */}
        {pendingDuplicates === null && earlyWarnings.length > 0 ? (
          <p className="patient-form__notice" role="status">
            {earlyWarnings.length === 1
              ? 'A patient already on file looks like this person. '
              : `${earlyWarnings.length} patients already on file look like this person. `}
            You will be shown their records before anything is saved.
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
