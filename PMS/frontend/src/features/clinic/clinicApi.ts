import { httpClient, request } from '../../shared/api/httpClient';
import { isProblemDetailsError } from '../../shared/api/problemDetails';
import type { ClinicProfile, UpsertClinicProfileRequest } from './types/clinicProfile';
import type {
  SettingCategoryValue,
  SettingOption,
  SettingOptionItemRequest,
  VitalRange,
  VitalRangeItemRequest,
} from './types/clinicSettings';

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

  // --- F-4: doctor-configured settings (plan F-4 points 3 and 4) ------------

  /**
   * GET /api/clinic-settings/options?category=...
   *
   * `includeInactive` defaults to `false`, matching the server: a caller that just wants a
   * dropdown gets only options that may still be offered. The settings editor asks for `true`,
   * because it cannot bring back an option it was never shown.
   */
  getOptions: (
    category: SettingCategoryValue,
    includeInactive = false,
    signal?: AbortSignal,
  ): Promise<SettingOption[]> =>
    httpClient.get<SettingOption[]>(
      `/clinic-settings/options?category=${encodeURIComponent(category)}` +
        (includeInactive ? '&includeInactive=true' : ''),
      { signal },
    ),

  /**
   * PUT /api/clinic-settings/options/{category} - the whole list, in display order.
   *
   * There is no delete call here because the API has no delete route: an option omitted from this
   * list is retired (`isActive: false`), never removed, so historical patient records never end up
   * displaying a value that appears nowhere in settings (plan F-4 point 5).
   */
  saveOptions: (
    category: SettingCategoryValue,
    items: SettingOptionItemRequest[],
  ): Promise<SettingOption[]> =>
    httpClient.put<SettingOption[]>(
      `/clinic-settings/options/${encodeURIComponent(category)}`,
      { items },
    ),

  /** GET /api/clinic-settings/vital-ranges - always every metric, configured or not. */
  getVitalRanges: (signal?: AbortSignal): Promise<VitalRange[]> =>
    httpClient.get<VitalRange[]>('/clinic-settings/vital-ranges', { signal }),

  /** PUT /api/clinic-settings/vital-ranges - blank thresholds travel as `null`, never `0`. */
  saveVitalRanges: (items: VitalRangeItemRequest[]): Promise<VitalRange[]> =>
    httpClient.put<VitalRange[]>('/clinic-settings/vital-ranges', { items }),
};
