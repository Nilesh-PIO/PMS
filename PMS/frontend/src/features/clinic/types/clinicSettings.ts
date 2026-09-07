/**
 * TypeScript mirrors of the API's F-4 DTOs
 * (planning-pms-verification.md, F-4 point 3).
 */

/**
 * The doctor-configured lists.
 *
 * Names rather than numbers, matching the wire format: the plan's own routes are
 * `?category=Gender` and `PUT /options/{category}`, so the payload speaks the same vocabulary
 * as the URL.
 */
export const SettingCategory = {
  Gender: 'Gender',
  VitalsNotRecordedReason: 'VitalsNotRecordedReason',
} as const;

export type SettingCategoryValue = (typeof SettingCategory)[keyof typeof SettingCategory];

/** How each list is titled and explained on the settings screen. */
export const SETTING_CATEGORY_LABELS: Record<
  SettingCategoryValue,
  { title: string; description: string }
> = {
  Gender: {
    title: 'Gender options',
    description:
      'Offered when registering a patient. Keeping this a list rather than a free-text box is what stops "M", "Male" and "male" ending up in the same column.',
  },
  VitalsNotRecordedReason: {
    title: 'Reasons a vital was not recorded',
    description:
      'Offered during a consultation when a vital genuinely cannot be taken, so the record can say so instead of holding a number nobody measured.',
  },
};

export interface SettingOption {
  id: number;
  category: SettingCategoryValue;
  value: string;
  displayOrder: number;
  isActive: boolean;
  /** True for options the server refuses to retire, e.g. gender "Not stated" (E-23). */
  isProtected: boolean;
}

/** One entry in `PUT /api/clinic-settings/options/{category}`. Order in the array is display order. */
export interface SettingOptionItemRequest {
  value: string;
  isActive: boolean;
}

export interface SettingOptionListRequest {
  items: SettingOptionItemRequest[];
}

/** The metrics a plausibility threshold can be set against (E-12). */
export const VitalMetric = {
  Temperature: 'Temperature',
  BloodPressureSystolic: 'BloodPressureSystolic',
  BloodPressureDiastolic: 'BloodPressureDiastolic',
  PulseBpm: 'PulseBpm',
} as const;

export type VitalMetricValue = (typeof VitalMetric)[keyof typeof VitalMetric];

export interface VitalRange {
  metric: VitalMetricValue;
  /** Server-supplied so the two screens that render it cannot drift apart. */
  label: string;
  /** "mmHg", "bpm", or the clinic's temperature symbol; `null` if the unit is not set (E-24). */
  unit: string | null;
  /**
   * `null` means no lower warning - not zero. The distinction is the feature: a blank threshold
   * is silence, while `0` would warn on every reading.
   */
  warnLow: number | null;
  warnHigh: number | null;
  /** `null` when this metric has never been configured. */
  updatedUtc: string | null;
}

export interface VitalRangeItemRequest {
  metric: VitalMetricValue;
  warnLow: number | null;
  warnHigh: number | null;
}

export interface VitalRangeListRequest {
  items: VitalRangeItemRequest[];
}

/** A soft warning. Advisory only - confirming saves the value exactly as entered (E-12). */
export interface VitalWarning {
  metric: VitalMetricValue;
  label: string;
  value: number;
  warnLow: number | null;
  warnHigh: number | null;
  message: string;
}

/** Limits mirrored from ClinicSettingsService, so the form can warn before the round-trip. */
export const CLINIC_SETTINGS_LIMITS = {
  optionValue: 100,
  optionsPerCategory: 50,
  /**
   * Storage limits, not clinical ones - the column is `decimal(6,2)`. This codebase contains no
   * clinical range anywhere, by design (plan section 7, "Clinical-rule boundary").
   */
  thresholdMagnitude: 10000,
  thresholdDecimals: 2,
} as const;

/**
 * Parses a threshold input.
 *
 * An empty box is `null` - "do not warn me about this" - and never `0`, which would arm a warning
 * on every reading the clinic enters. `Number('')` is `0`, so this is written out by hand rather
 * than leaning on the built-in coercion, and a test pins it (plan F-4 point 6).
 */
export function parseThreshold(raw: string): number | null | 'invalid' {
  const trimmed = raw.trim();
  if (trimmed.length === 0) {
    return null;
  }

  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : 'invalid';
}

/** Renders a threshold back into an input box; `null` becomes an empty box, never "0". */
export function formatThreshold(value: number | null): string {
  return value === null ? '' : String(value);
}
