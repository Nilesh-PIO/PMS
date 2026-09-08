import { keepPreviousData, useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { patientsApi } from './patientsApi';
import type { PatientSummary } from './types/patient';

/**
 * F-7's hooks (planning-pms-verification.md, F-7 point 4:
 * "`usePatientSearch(query)` (TanStack Query, `keepPreviousData`) and `useRecentPatients()`").
 */

/** Debounce before a keystroke becomes a request. The plan's number (C-35). */
export const SEARCH_DEBOUNCE_MS = 300;

/** Shortest query that is sent at all. Matches the server's rule, which is the real one. */
export const MIN_QUERY_LENGTH = 2;

/** Query keys, exported so a test or a later feature can invalidate precisely. */
export const patientSearchQueryKey = (query: string, includeInactive: boolean) =>
  ['patients', 'search', query, includeInactive] as const;

export const recentPatientsQueryKey = (take?: number) =>
  ['patients', 'recent', take ?? 'default'] as const;

/**
 * Delays a fast-changing value until it stops changing for {@link SEARCH_DEBOUNCE_MS}.
 *
 * A physician types roughly five characters a second, so an undebounced search box issues five
 * queries and renders four results nobody reads — and, worse, the results flicker through
 * intermediate states while they are still typing. The debounce is what makes the box feel like it
 * is keeping up rather than arguing.
 */
export function useDebouncedValue<T>(value: T, delayMs: number = SEARCH_DEBOUNCE_MS): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    // Clearing on every change is what makes this a debounce rather than a throttle: the timer
    // restarts with each keystroke and only fires once typing pauses.
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}

export interface UsePatientSearchOptions {
  includeInactive?: boolean;
  take?: number;
  /** Set false to hold the query without unmounting the component that owns it. */
  enabled?: boolean;
}

/**
 * Searches patients by name or phone (BRD L93).
 *
 * **Three deliberate behaviours.**
 *
 * 1. *The query is debounced by the caller, not here.* The hook takes an already-debounced value so
 *    the component can show what was typed immediately while the request lags behind it — the
 *    alternative makes the input itself feel slow.
 * 2. *`placeholderData: keepPreviousData`* (plan F-7 point 4). Without it, every new query blanks
 *    the list to a loading state and the rows the physician was reading vanish under their cursor.
 *    With it, the previous results stay on screen, dimmed, until the new ones arrive.
 * 3. *Below the minimum length the hook does not fire at all.* The server answers a one-character
 *    query with a 400, and rendering that as an error message while someone is still typing the
 *    second letter would be noise. The server rule stands; the client simply does not walk into it.
 */
export function usePatientSearch(
  query: string,
  { includeInactive = false, take, enabled = true }: UsePatientSearchOptions = {},
): UseQueryResult<PatientSummary[]> {
  const trimmed = query.trim();
  const longEnough = trimmed.length >= MIN_QUERY_LENGTH;

  return useQuery<PatientSummary[]>({
    queryKey: patientSearchQueryKey(trimmed, includeInactive),
    queryFn: ({ signal }) =>
      patientsApi.searchPatients(trimmed, { includeInactive, take, signal }),
    enabled: enabled && longEnough,
    placeholderData: keepPreviousData,
    // Short, but not zero. Backspacing one character and retyping it is a single round trip rather
    // than two, while a patient registered moments ago still shows up on the next real search.
    staleTime: 15_000,
  });
}

/**
 * The patients most recently dealt with (BRD L159).
 *
 * `staleTime` is deliberately short: this list is the home screen, and a patient registered a
 * moment ago should be at the top of it when the physician navigates back.
 */
export function useRecentPatients(take?: number): UseQueryResult<PatientSummary[]> {
  return useQuery<PatientSummary[]>({
    queryKey: recentPatientsQueryKey(take),
    queryFn: ({ signal }) => patientsApi.getRecentPatients(take, signal),
    staleTime: 10_000,
  });
}
