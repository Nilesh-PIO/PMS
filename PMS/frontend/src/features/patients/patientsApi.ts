import { httpClient } from '../../shared/api/httpClient';
import type {
  CreatePatientRequest,
  DuplicateCandidate,
  DuplicateCheckRequest,
  MarkMergedRequest,
  Patient,
  PatientDetail,
} from './types/patient';

/**
 * The patient endpoints, one function each
 * (planning-pms-verification.md, F-5 point 4 and F-6 point 4).
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
