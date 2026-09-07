import { useEffect, useState } from 'react';
import { useMutation, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { patientsApi } from './patientsApi';
import type {
  DuplicateCandidate,
  DuplicateCheckRequest,
  MarkMergedRequest,
  Patient,
} from './types/patient';

/**
 * F-6's hooks (planning-pms-verification.md, F-6 point 4:
 * "`useDuplicateCheck()` — debounced 400 ms on name/phone blur").
 */

/** The debounce the plan specifies, in one place so it is a value rather than a magic number. */
export const DUPLICATE_CHECK_DEBOUNCE_MS = 400;

/**
 * The shortest name that is worth asking about.
 *
 * A single character matches a large fraction of any clinic under a fuzzy comparison, and the
 * answer would be noise arriving while the physician is still typing the first name.
 */
export const MIN_NAME_LENGTH_FOR_CHECK = 2;

/** Query key for a duplicate check, so two identical checks share one request. */
export const duplicateCheckQueryKey = (request: DuplicateCheckRequest) =>
  [
    'patients',
    'duplicate-check',
    request.fullName.trim().toLowerCase(),
    request.phone?.trim() ?? '',
    request.dateOfBirth ?? '',
    request.excludePatientId ?? '',
  ] as const;

/**
 * Asks whether this person may already be on file, as the form is filled in.
 *
 * **Three things this hook is careful about.**
 *
 * 1. *It only asks when the answer could be non-empty.* The server's identity rule needs a name
 *    plus either a phone or a date of birth, so a form with only a name produces no request at all
 *    rather than a round trip that can only come back empty.
 * 2. *It is debounced, not throttled* (plan F-6 point 4: 400 ms). TanStack Query's `enabled` is
 *    driven by a debounced copy of the request, so typing a phone number one digit at a time issues
 *    one request rather than ten. `staleTime` then keeps the answer while the physician reads it.
 * 3. *A failed check never blocks the form.* The registration path runs the same check server-side
 *    before it writes anything, so this hook is an early warning and not the guarantee. If it fails
 *    — offline, slow, whatever — the physician can still register, and the server still refuses a
 *    duplicate with a 409. That ordering is deliberate: a check that could jam the form would be a
 *    worse failure than the duplicate it prevents.
 */
export function useDuplicateCheck(
  request: DuplicateCheckRequest,
  options: { enabled?: boolean; debounceMs?: number } = {},
): UseQueryResult<DuplicateCandidate[]> {
  const { enabled = true, debounceMs = DUPLICATE_CHECK_DEBOUNCE_MS } = options;

  const debounced = useDebouncedValue(request, debounceMs);

  return useQuery<DuplicateCandidate[]>({
    queryKey: duplicateCheckQueryKey(debounced),
    queryFn: ({ signal }) => patientsApi.checkDuplicates(debounced, signal),
    enabled: enabled && isCheckable(debounced),
    staleTime: 30_000,
    // One retry would double every check the moment the network is unhappy, and this is an advisory
    // request the form does not wait on.
    retry: false,
  });
}

/**
 * True when the request could actually match something.
 *
 * Mirrors the server's rule rather than guessing at it: a name plus either a phone or a date of
 * birth. This is the hook's own gate — it is what stops a form with only a name from issuing a
 * request that could only ever come back empty.
 *
 * Exported so a caller that wants to say something about the check's state can distinguish "checked
 * and found nobody" from "never asked". Claiming a clean check that never ran would be worse than
 * staying quiet, and that distinction is not recoverable from an empty result alone.
 */
export function isCheckable(request: DuplicateCheckRequest): boolean {
  const hasName = request.fullName.trim().length >= MIN_NAME_LENGTH_FOR_CHECK;
  const hasPhone = (request.phone ?? '').trim().length > 0;
  const hasDob = (request.dateOfBirth ?? '').trim().length > 0;

  return hasName && (hasPhone || hasDob);
}

/**
 * Records that a patient is a duplicate of another (E-26).
 *
 * No cache invalidation of a patient list here, because F-6 does not have one — F-7 builds search
 * and F-8 the edit path, and each will invalidate what it owns. Seeding a partial list from here
 * would be inventing a cache key for a feature that does not exist yet.
 */
export function useMarkMerged() {
  return useMutation<Patient, Error, { id: string; request: MarkMergedRequest }>({
    mutationFn: ({ id, request }) => patientsApi.markMerged(id, request),
  });
}

/**
 * The debounce itself.
 *
 * Local to this module rather than in `shared/hooks/`: F-7's search debounce is 300 ms against a
 * different endpoint with different cancellation behaviour, and sharing one hook between them
 * prematurely would make the next change to either a change to both.
 *
 * The request is compared by *value*, not by reference. `PatientForm` builds a fresh object on
 * every keystroke, so a reference comparison would restart the timer on every render and the check
 * would either never fire or fire constantly.
 */
function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState<T>(value);
  const serialised = JSON.stringify(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(JSON.parse(serialised) as T), delayMs);
    return () => clearTimeout(timer);
  }, [serialised, delayMs]);

  return debounced;
}
