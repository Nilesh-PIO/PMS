import { httpClient, request } from '../../shared/api/httpClient';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import type { ClinicProfile, UpsertClinicProfileRequest } from './types/clinicProfile';

/**
 * The four F-3 endpoints, one function each
 * (planning-pms-verification.md, F-3 point 4).
 *
 * Every call goes through the shared `httpClient`, so a failure is always a typed
 * `ProblemDetailsError` and never a resolved promise carrying an error-shaped value (E-47).
 */
export const clinicApi = {
  /**
   * GET /api/clinic-profile.
   *
   * A 404 means first-run setup has never been saved. That is a legitimate answer - it is the
   * whole reason the /setup screen exists - so it resolves to `null` rather than throwing.
   * Any other failure still throws, because "the server is down" must not render as
   * "you have no clinic profile" and invite the physician to retype one.
   */
  getProfile: async (signal?: AbortSignal): Promise<ClinicProfile | null> => {
    try {
      return await httpClient.get<ClinicProfile>('/clinic-profile', { signal });
    } catch (error) {
      if (isProblemDetailsError(error) && error.status === 404) {
        return null;
      }
      throw error;
    }
  },

  /** PUT /api/clinic-profile - 200 with the saved profile, 400 with field errors. */
  saveProfile: (body: UpsertClinicProfileRequest): Promise<ClinicProfile> =>
    httpClient.put<ClinicProfile>('/clinic-profile', body),

  /**
   * POST /api/clinic-profile/signature - 200, 400 for a non-PNG, 413 over 200 KB.
   *
   * Sent as multipart. No `Content-Type` header is set by hand: the browser has to add the
   * multipart boundary, and overriding it produces a request the server cannot parse.
   */
  uploadSignature: (file: File): Promise<ClinicProfile> => {
    const form = new FormData();
    form.append('file', file);
    return request<ClinicProfile>('/clinic-profile/signature', { method: 'POST', body: form });
  },

  /** DELETE /api/clinic-profile/signature - 200 with the profile, signature cleared. */
  deleteSignature: (): Promise<ClinicProfile> =>
    httpClient.delete<ClinicProfile>('/clinic-profile/signature'),
};
