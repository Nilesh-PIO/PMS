import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { SESSION_QUERY_KEY } from '../auth/useSession';
import { clinicApi } from './clinicApi';
import type { ClinicProfile, UpsertClinicProfileRequest } from './types/clinicProfile';

/** Query key for the clinic profile. Exported so tests and other features can invalidate it. */
export const CLINIC_PROFILE_QUERY_KEY = ['clinic', 'profile'] as const;

/**
 * The clinic profile (planning-pms-verification.md, F-3 point 4).
 *
 * `null` data means first-run setup has never been saved - not an error.
 */
export function useClinicProfile(): UseQueryResult<ClinicProfile | null> {
  return useQuery<ClinicProfile | null>({
    queryKey: CLINIC_PROFILE_QUERY_KEY,
    queryFn: ({ signal }) => clinicApi.getProfile(signal),
    staleTime: 60_000,
  });
}

/**
 * Every write to the profile refreshes both caches.
 *
 * The session refresh is the load-bearing half: `session.setupComplete` is what the router guard
 * reads, so without invalidating it the physician would save a complete profile and still be
 * bounced back to /setup until the 60-second staleTime elapsed (E-1).
 */
function useProfileMutation<TArgs>(mutationFn: (args: TArgs) => Promise<ClinicProfile>) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn,
    onSuccess: (profile) => {
      queryClient.setQueryData(CLINIC_PROFILE_QUERY_KEY, profile);
      void queryClient.invalidateQueries({ queryKey: SESSION_QUERY_KEY });
    },
  });
}

/** Saves the clinic header/footer text. */
export function useSaveClinicProfile() {
  return useProfileMutation((body: UpsertClinicProfileRequest) => clinicApi.saveProfile(body));
}

/** Uploads a PNG signature. */
export function useUploadSignature() {
  return useProfileMutation((file: File) => clinicApi.uploadSignature(file));
}

/** Removes the stored signature; the printed footer falls back to a ruled area. */
export function useDeleteSignature() {
  return useProfileMutation(() => clinicApi.deleteSignature());
}
