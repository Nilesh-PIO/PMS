import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { patientsApi } from './patientsApi';
import type { CreatePatientRequest, PatientDetail } from './types/patient';

/**
 * F-5's hooks (planning-pms-verification.md, F-5 point 4:
 * "`useCreatePatient()`, `usePatient(id)`").
 */

/** Query key for one patient's profile. Exported so F-8's edit can invalidate it. */
export const patientQueryKey = (id: string) => ['patients', id] as const;

/**
 * One patient's full profile.
 *
 * `staleTime` is 30 seconds — shorter than the settings lists, because a profile is edited by F-8
 * during a working day whereas a gender list is edited a handful of times in the life of a clinic.
 */
export function usePatient(id: string | undefined): UseQueryResult<PatientDetail> {
  return useQuery<PatientDetail>({
    queryKey: patientQueryKey(id ?? ''),
    queryFn: ({ signal }) => patientsApi.getPatient(id!, signal),
    enabled: Boolean(id),
    staleTime: 30_000,
  });
}

/**
 * Registers a patient.
 *
 * On success the new patient is seeded into the profile cache under its own key, so navigating
 * straight to `/patients/:id` after saving renders the record immediately instead of showing a
 * loading state for a record the client is already holding.
 *
 * Note the seeded value is the *summary* shape widened to the detail shape — every extra field is
 * filled from what was submitted, and the query is left stale so the authoritative profile is
 * refetched. Seeding a half-built object without that refetch would be how a display bug becomes a
 * data bug.
 */
export function useCreatePatient() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreatePatientRequest) => patientsApi.createPatient(request),
    onSuccess: (created) => {
      void queryClient.invalidateQueries({ queryKey: patientQueryKey(created.id) });
    },
  });
}
