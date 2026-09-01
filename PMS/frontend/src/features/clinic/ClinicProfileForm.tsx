import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import { PatientDataForm } from '../../shared/components/forms/PatientDataForm';
import { TextAreaField } from '../../shared/components/forms/TextAreaField';
import { TextField } from '../../shared/components/forms/TextField';
import { useDeleteSignature, useSaveClinicProfile, useUploadSignature } from './useClinicProfile';
import {
  CLINIC_PROFILE_LIMITS,
  SELECTABLE_TEMPERATURE_UNITS,
  TemperatureUnit,
  type ClinicProfile,
  type TemperatureUnitValue,
  type UpsertClinicProfileRequest,
} from './types/clinicProfile';

export interface ClinicProfileFormProps {
  /** The saved profile, or `null` during first-run setup. */
  profile: ClinicProfile | null;
  /** Label for the primary button - "Save and continue" on /setup, "Save changes" in settings. */
  submitLabel: string;
  /** Called after a successful save. `/setup` uses it to move on. */
  onSaved?: (profile: ClinicProfile) => void;
}

/**
 * The single clinic-profile form, rendered by both `FirstRunSetupPage` and `ClinicProfilePage`
 * (planning-pms-verification.md, F-3 point 4: "both render the same ClinicProfileForm").
 *
 * One component rather than two similar ones on purpose: the first-run screen and the settings
 * screen must capture exactly the same fields with exactly the same rules, or a clinic could pass
 * the setup gate on one screen and fail it on the other.
 */
export function ClinicProfileForm({ profile, submitLabel, onSaved }: ClinicProfileFormProps) {
  const [clinicName, setClinicName] = useState(profile?.clinicName ?? '');
  const [addressLines, setAddressLines] = useState(profile?.addressLines ?? '');
  const [doctorName, setDoctorName] = useState(profile?.doctorName ?? '');
  const [doctorRegistrationNo, setDoctorRegistrationNo] = useState(
    profile?.doctorRegistrationNo ?? '',
  );
  const [prescriptionFooter, setPrescriptionFooter] = useState(profile?.prescriptionFooter ?? '');
  const [temperatureUnit, setTemperatureUnit] = useState<TemperatureUnitValue | null>(
    profile && profile.temperatureUnit !== TemperatureUnit.Unspecified
      ? profile.temperatureUnit
      : null,
  );
  const [signatureError, setSignatureError] = useState<string | null>(null);

  const fileInput = useRef<HTMLInputElement>(null);

  const save = useSaveClinicProfile();
  const upload = useUploadSignature();
  const remove = useDeleteSignature();

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const body: UpsertClinicProfileRequest = {
      clinicName,
      addressLines,
      doctorName,
      doctorRegistrationNo,
      prescriptionFooter,
      // Sent as null when unanswered rather than defaulted, so the server rejects the form
      // instead of silently choosing a unit for the clinic (E-24).
      temperatureUnit,
    };

    try {
      const saved = await save.mutateAsync(body);
      onSaved?.(saved);
    } catch {
      // Rendered from `save.error` below. Re-throwing would become an unhandled rejection and
      // tell the physician nothing.
    }
  };

  const handleSignatureChange = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    // Reset immediately so picking the same file twice after a failure still fires a change.
    event.target.value = '';
    if (!file) {
      return;
    }

    setSignatureError(null);

    // Checked here as well as on the server. Not a substitute for the server rule - it is what
    // stops the physician from waiting for a 200 KB upload only to be told it was too big.
    if (file.size > CLINIC_PROFILE_LIMITS.signatureBytes) {
      setSignatureError(
        `That image is ${Math.ceil(file.size / 1024)} KB. The signature must be ${
          CLINIC_PROFILE_LIMITS.signatureBytes / 1024
        } KB or smaller.`,
      );
      return;
    }

    try {
      await upload.mutateAsync(file);
    } catch (error) {
      setSignatureError(
        isProblemDetailsError(error) ? error.userMessage : 'Could not upload the signature image.',
      );
    }
  };

  const handleSignatureRemove = async () => {
    setSignatureError(null);
    try {
      await remove.mutateAsync();
    } catch (error) {
      setSignatureError(
        isProblemDetailsError(error) ? error.userMessage : 'Could not remove the signature image.',
      );
    }
  };

  const fieldErrors = isProblemDetailsError(save.error) ? save.error.fieldErrors : {};

  const formError = (() => {
    if (!save.isError) {
      return null;
    }
    if (isProblemDetailsError(save.error)) {
      // A 400 is already shown per field; anything else needs a sentence.
      return save.error.status === 400 ? null : save.error.userMessage;
    }
    return 'Could not save the clinic profile. Try again.';
  })();

  return (
    <PatientDataForm onSubmit={handleSubmit} noValidate className="clinic-form">
      {formError ? (
        <p className="clinic-form__error" role="alert">
          {formError}
        </p>
      ) : null}

      <fieldset className="clinic-form__group">
        <legend>Prescription header</legend>

        <TextField
          label="Clinic name"
          name="clinicName"
          value={clinicName}
          onChange={(event) => setClinicName(event.target.value)}
          maxLength={CLINIC_PROFILE_LIMITS.clinicName}
          error={fieldErrors.ClinicName?.[0]}
          required
          autoFocus
        />

        <TextAreaField
          label="Clinic address"
          name="addressLines"
          rows={3}
          value={addressLines}
          onChange={(event) => setAddressLines(event.target.value)}
          maxLength={CLINIC_PROFILE_LIMITS.addressLines}
          hint="Printed exactly as typed, one line per line."
          error={fieldErrors.AddressLines?.[0]}
        />

        <TextField
          label="Doctor's name"
          name="doctorName"
          value={doctorName}
          onChange={(event) => setDoctorName(event.target.value)}
          maxLength={CLINIC_PROFILE_LIMITS.doctorName}
          error={fieldErrors.DoctorName?.[0]}
          required
        />

        <TextField
          label="Registration number"
          name="doctorRegistrationNo"
          value={doctorRegistrationNo}
          onChange={(event) => setDoctorRegistrationNo(event.target.value)}
          maxLength={CLINIC_PROFILE_LIMITS.doctorRegistrationNo}
          error={fieldErrors.DoctorRegistrationNo?.[0]}
          required
        />
      </fieldset>

      <fieldset className="clinic-form__group">
        {/*
          E-24. The unit is asked once, here, and then travels with every temperature this clinic
          ever records. There is deliberately no pre-selected option: a default would let the
          clinic pass this screen without anyone having answered the question.
        */}
        <legend>Temperature unit</legend>
        <p className="clinic-form__hint">
          Every temperature is recorded and printed in this unit. Ask before choosing - changing it
          later does not convert values already recorded.
        </p>

        <div
          className="clinic-form__radios"
          role="radiogroup"
          aria-label="Temperature unit"
          aria-invalid={fieldErrors.TemperatureUnit ? true : undefined}
        >
          {SELECTABLE_TEMPERATURE_UNITS.map((unit) => (
            <label className="clinic-form__radio" key={unit.value}>
              <input
                type="radio"
                name="temperatureUnit"
                value={unit.value}
                checked={temperatureUnit === unit.value}
                onChange={() => setTemperatureUnit(unit.value)}
              />
              {unit.label}
            </label>
          ))}
        </div>

        {fieldErrors.TemperatureUnit?.[0] ? (
          <p className="field__error">{fieldErrors.TemperatureUnit[0]}</p>
        ) : null}
      </fieldset>

      <fieldset className="clinic-form__group">
        <legend>Prescription footer</legend>

        <TextAreaField
          label="Footer text"
          name="prescriptionFooter"
          rows={2}
          value={prescriptionFooter}
          onChange={(event) => setPrescriptionFooter(event.target.value)}
          maxLength={CLINIC_PROFILE_LIMITS.prescriptionFooter}
          hint={`Optional. Up to ${CLINIC_PROFILE_LIMITS.prescriptionFooter} characters.`}
          error={fieldErrors.PrescriptionFooter?.[0]}
        />

        <div className="clinic-form__signature">
          <span className="field__label">Signature image</span>

          {profile?.signatureImageDataUrl ? (
            <img
              className="clinic-form__signature-preview"
              src={profile.signatureImageDataUrl}
              alt="Uploaded signature"
            />
          ) : (
            /*
              Plan F-3 point 1: with no signature the prescription shows a ruled signature area,
              never a broken-image placeholder. The preview shows the same thing, so what the
              physician sees here is what will actually print.
            */
            <div className="clinic-form__signature-rule" data-testid="signature-rule">
              <span className="clinic-form__signature-caption">
                No signature uploaded - prescriptions will print a ruled signature line.
              </span>
            </div>
          )}

          {profile ? (
            <div className="clinic-form__signature-actions">
              <input
                ref={fileInput}
                id="field-signature"
                name="signature"
                type="file"
                accept="image/png"
                onChange={handleSignatureChange}
                disabled={upload.isPending}
                aria-label="Upload a PNG signature"
              />
              {profile.signatureImageDataUrl ? (
                <button
                  className="button button--quiet"
                  type="button"
                  onClick={handleSignatureRemove}
                  disabled={remove.isPending}
                >
                  Remove signature
                </button>
              ) : null}
            </div>
          ) : (
            <p className="clinic-form__hint">
              Save the clinic details first, then a signature image can be uploaded.
            </p>
          )}

          {signatureError ? (
            <p className="field__error" role="alert">
              {signatureError}
            </p>
          ) : null}
        </div>
      </fieldset>

      <button className="button button--primary" type="submit" disabled={save.isPending}>
        {save.isPending ? 'Saving...' : submitLabel}
      </button>
    </PatientDataForm>
  );
}
