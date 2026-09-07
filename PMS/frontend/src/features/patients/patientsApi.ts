import { httpClient } from '../../shared/api/httpClient';
import type { CreatePatientRequest, Patient, PatientDetail } from './types/patient';

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
};
