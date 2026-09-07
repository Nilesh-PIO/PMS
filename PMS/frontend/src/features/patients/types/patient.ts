/**
 * TypeScript mirrors of F-5's API DTOs
 * (planning-pms-verification.md, F-5 point 3; section 2 "types/ holding interfaces mirroring the
 * API DTOs").
 */

/** The summary shape — what any list or picker needs to tell two people apart (REC-12). */
export interface Patient {
  id: string;
  fullName: string;
  /** Last four digits, or null when no phone was recorded. */
  phoneTail: string | null;
  /** Formatted on the server so screen and printed prescription can never disagree. */
  ageDisplay: string;
  gender: string | null;
  status: 'Active' | 'Inactive';
  isProfileIncomplete: boolean;
}

/**
 * One row in a picker — a search result or a recent-list entry (F-7, mirroring
 * `PatientSummaryResponse`).
 *
 * **Every disambiguating field is required, and that is the point** (E-28, RSK-12). A row rendered
 * from this type cannot be name-only unless a component deliberately discards fields, which is the
 * structural version of "never show a name alone in a selection list".
 */
export interface PatientSummary {
  id: string;
  fullName: string;
  /** Last four digits, or null when no phone was recorded. */
  phoneTail: string | null;
  /** Server-formatted, so screen and printed prescription can never disagree. */
  ageDisplay: string;
  gender: string | null;
  /**
   * ISO date of the most recent visit.
   *
   * **Always null until F-10 introduces `Visit`** — see `PatientSummaryResponse.LastVisitDate` on
   * the server for the assumption behind it. `registeredOn` carries the date axis in the meantime,
   * so a picker row always has a date to show.
   */
  lastVisitDate: string | null;
  /** ISO date the record was created. */
  registeredOn: string;
  status: 'Active' | 'Inactive';
  /** True when this record points at a survivor (F-6). Rendered as a warning, never hidden. */
  isMerged: boolean;
  isProfileIncomplete: boolean;
  /** Why this row matched. `SimilarName` means it is a *guess* from the fuzzy fallback (E-30). */
  matchKind: PatientMatchKind;
}

/** The values `PatientSummary.matchKind` can take. */
export type PatientMatchKind = 'Name' | 'Phone' | 'SimilarName' | 'Recent';

/** The full profile. */
export interface PatientDetail extends Patient {
  normalizedName: string;
  /** ISO date, or null. */
  dateOfBirth: string | null;
  approxAgeYears: number | null;
  /** ISO date, or null. */
  ageRecordedOn: string | null;
  primaryPhone: string | null;
  altContact: string | null;
  registeredUtc: string;
  inactiveReason: string | null;
  mergedIntoPatientId: string | null;
  /** Which of "phone", "age", "gender" are missing (E-8, E-20). */
  missingFields: string[];
}

/**
 * The registration request.
 *
 * Mirrors `CreatePatientRequest`, and mirrors its omissions just as deliberately: there is no
 * `status`, `normalizedName`, `registeredUtc` or `mergedIntoPatientId` here because the client may
 * not set them.
 */
export interface CreatePatientRequest {
  fullName: string;
  dateOfBirth?: string | null;
  approxAgeYears?: number | null;
  ageRecordedOn?: string | null;
  gender?: string | null;
  primaryPhone?: string | null;
  altContact?: string | null;
  /** Idempotency token from `useSubmitOnce` (E-43, E-46). */
  submissionId?: string;
}

/**
 * How the age was given. The form is a three-way choice rather than two optional boxes, because
 * "date of birth" and "approximate age" are mutually exclusive on the server (E-9) and a UI that
 * lets both be filled in is a UI that produces a 400 the physician did not ask for.
 */
export type AgeMode = 'unknown' | 'dateOfBirth' | 'approximate';

/** Human-readable labels for the server's `missingFields` slugs. */
export const MISSING_FIELD_LABELS: Record<string, string> = {
  phone: 'phone number',
  age: 'age or date of birth',
  gender: 'gender',
};
