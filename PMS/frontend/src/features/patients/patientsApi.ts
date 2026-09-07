import { httpClient } from '../../shared/api/httpClient';
import type {
  CreatePatientRequest,
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
 * F-5's two endpoints, one function each
 * (planning-pms-verification.md, F-5 point 4).
 *
 * Every call goes through the shared `httpClient`, so a failure is always a typed
 * `ProblemDetailsError` and never a resolved promise carrying an error-shaped value (E-47). That
 * matters more here than on the settings screens: the failure being reported is "this patient was
 * not registered", and a swallowed one would leave the physician believing a record exists.
 */
export const patientsApi = {
  /**
   * POST /api/patients — 201 with the new patient, 400 with field errors.
   *
   * Unlike `clinicApi.getProfile`, there is no status this treats as a non-error. A 404 or a 409
   * here means something the caller must see.
   */
  createPatient: (body: CreatePatientRequest): Promise<Patient> =>
    httpClient.post<Patient>('/patients', body),

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
};
