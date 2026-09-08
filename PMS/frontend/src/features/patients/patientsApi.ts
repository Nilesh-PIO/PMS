import { httpClient } from '../../shared/api/httpClient';
import type {
  CreatePatientRequest,
  DuplicateCandidate,
  DuplicateCheckRequest,
  MarkMergedRequest,
  Patient,
  PatientDetail,
  PatientSummary,
} from './types/patient';

/** Options for a search request (F-7). */
export interface SearchOptions {
  /** Include retired and merged records. They come back flagged, never silently. */
  includeInactive?: boolean;
  /** Row limit. The server clamps at 50 and defaults to 20. */
  take?: number;
  /**
   * Abort signal. TanStack Query supplies one per query, which is what makes a superseded
   * keystroke stop costing the server anything.
   */
  signal?: AbortSignal;
}

/**
 * The patient endpoints, one function each
 * (planning-pms-verification.md, F-5 point 4, F-6 point 4 and F-7 point 4).
 *
 * Every call goes through the shared `httpClient`, so a failure is always a typed
 * `ProblemDetailsError` and never a resolved promise carrying an error-shaped value (E-47). That
 * matters more here than on the settings screens: the failure being reported is "this patient was
 * not registered", and a swallowed one would leave the physician believing a record exists.
 */
export const patientsApi = {
  /**
   * POST /api/patients — 201 with the new patient, 400 with field errors, **409 when the person may
   * already be on file** (F-6).
   *
   * The 409 carries the candidate list in its problem body, and nothing has been written when it
   * arrives. `confirmDuplicate` is the physician having looked at those candidates and said
   * "register them anyway"; it is always honoured, because the check warns and never blocks
   * (REC-2).
   */
  createPatient: (body: CreatePatientRequest, confirmDuplicate = false): Promise<Patient> =>
    httpClient.post<Patient>(
      confirmDuplicate ? '/patients?confirmDuplicate=true' : '/patients',
      body,
    ),

  /** GET /api/patients/{id} — 200 with the profile, 404 if no patient has this id. */
  getPatient: (id: string, signal?: AbortSignal): Promise<PatientDetail> =>
    httpClient.get<PatientDetail>(`/patients/${encodeURIComponent(id)}`, { signal }),

  /**
   * GET /api/patients/search — 200 with the best matches, 400 on a query under two characters.
   *
   * **Always an array, even for one match.** The caller renders a picker and the physician chooses;
   * there is no shape here that lets a client skip that step, because auto-selecting a single
   * confident-looking result is the wrong-patient path (RSK-12).
   *
   * An empty array is a normal 200 and the caller turns it into E-7's "register this name" action.
   */
  searchPatients: (query: string, options: SearchOptions = {}): Promise<PatientSummary[]> => {
    const params = new URLSearchParams({ query });

    // Only sent when true, so the ordinary request URL stays the one the server sees most and its
    // query-plan cache is not split across two spellings of the same search.
    if (options.includeInactive) {
      params.set('includeInactive', 'true');
    }
    if (options.take !== undefined) {
      params.set('take', String(options.take));
    }

    return httpClient.get<PatientSummary[]>(`/patients/search?${params.toString()}`, {
      signal: options.signal,
    });
  },

  /**
   * GET /api/patients/recent — 200, possibly with an empty array on a fresh install (E-2).
   *
   * An empty result is not an error and is never treated as one: the caller renders an empty state
   * offering "register the first patient" rather than a blank panel.
   */
  getRecentPatients: (take?: number, signal?: AbortSignal): Promise<PatientSummary[]> => {
    const params = new URLSearchParams();
    if (take !== undefined) {
      params.set('take', String(take));
    }
    const query = params.toString();

    return httpClient.get<PatientSummary[]>(`/patients/recent${query ? `?${query}` : ''}`, {
      signal,
    });
  },

  /**
   * POST /api/patients/duplicate-check — 200 with a possibly-empty list. Writes nothing (F-6).
   *
   * A POST rather than a GET even though it only reads: the arguments are a patient's name, phone
   * number and date of birth, and a GET would put all three in the URL, where they end up in server
   * logs, browser history and any proxy in between.
   */
  checkDuplicates: (
    body: DuplicateCheckRequest,
    signal?: AbortSignal,
  ): Promise<DuplicateCandidate[]> =>
    httpClient.post<DuplicateCandidate[]>('/patients/duplicate-check', body, { signal }),

  /**
   * POST /api/patients/{id}/mark-merged — records that this record is a duplicate of another
   * (F-6, E-26).
   *
   * A pointer, never a deletion. There is deliberately no `deletePatient` in this module, because
   * there is no such route: the record being retired is the one carrying a patient's visit history
   * (E-33).
   */
  markMerged: (id: string, body: MarkMergedRequest): Promise<Patient> =>
    httpClient.post<Patient>(`/patients/${encodeURIComponent(id)}/mark-merged`, body),
};
