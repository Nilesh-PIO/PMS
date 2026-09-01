/**
 * TypeScript mirrors of the API's F-3 DTOs
 * (planning-pms-verification.md, F-3 point 3).
 */

/**
 * The unit every temperature in this clinic is recorded and shown in (E-24).
 * Numeric to match the C# enum on the wire; `Unspecified` exists so "not answered yet" is
 * representable and can never be mistaken for a choice.
 */
export const TemperatureUnit = {
  Unspecified: 0,
  Celsius: 1,
  Fahrenheit: 2,
} as const;

export type TemperatureUnitValue = (typeof TemperatureUnit)[keyof typeof TemperatureUnit];

/** The two the physician may actually pick, in the order the radio group renders them. */
export const SELECTABLE_TEMPERATURE_UNITS = [
  { value: TemperatureUnit.Celsius, label: 'Celsius (°C)', symbol: '°C' },
  { value: TemperatureUnit.Fahrenheit, label: 'Fahrenheit (°F)', symbol: '°F' },
] as const;

export interface ClinicProfile {
  clinicName: string;
  addressLines: string;
  doctorName: string;
  doctorRegistrationNo: string;
  prescriptionFooter: string | null;
  temperatureUnit: TemperatureUnitValue;
  /**
   * `data:image/png;base64,...`, or `null` when no signature has been uploaded.
   * `null` is a supported end state: the printed footer shows a ruled signature area instead of
   * a broken image (plan F-3 point 1).
   */
  signatureImageDataUrl: string | null;
  /** Whether the clinic identity is complete enough to print (E-1). Server-derived; read-only. */
  isSetupComplete: boolean;
  updatedUtc: string;
}

/** Request body for `PUT /api/clinic-profile`. */
export interface UpsertClinicProfileRequest {
  clinicName: string;
  addressLines: string;
  doctorName: string;
  doctorRegistrationNo: string;
  prescriptionFooter: string;
  /**
   * `null` until the physician picks one. Sent as `null` rather than defaulted to Celsius so the
   * server rejects an unanswered form instead of guessing (E-24).
   */
  temperatureUnit: TemperatureUnitValue | null;
}

/** Field limits, mirrored from ClinicProfileService so the form can warn before the round-trip. */
export const CLINIC_PROFILE_LIMITS = {
  clinicName: 200,
  doctorName: 200,
  doctorRegistrationNo: 100,
  addressLines: 500,
  prescriptionFooter: 500,
  /** Maximum signature upload, in bytes (plan F-3 point 1: PNG, 200 KB). */
  signatureBytes: 200 * 1024,
} as const;
