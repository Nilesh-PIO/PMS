import { useMutation, useQuery, useQueryClient, type UseQueryResult } from '@tanstack/react-query';
import { clinicApi } from './clinicApi';
import type {
  SettingCategoryValue,
  SettingOption,
  SettingOptionItemRequest,
  VitalRange,
  VitalRangeItemRequest,
} from './types/clinicSettings';

/**
 * F-4's hooks (planning-pms-verification.md, F-4 point 4:
 * "hooks `useSettingOptions(category)` and `useVitalRanges()`").
 *
 * Kept beside `useClinicProfile.ts` in the same feature folder rather than in a new one - both are
 * "things the clinic configured", and F-11 will import `useSettingOptions` from here for the
 * vitals not-recorded reasons.
 */

/** Query key for one category's option list. Exported so other features can invalidate it. */
export const settingOptionsQueryKey = (
  category: SettingCategoryValue,
  includeInactive: boolean,
) => ['clinic', 'settings', 'options', category, { includeInactive }] as const;

/** Query key for the vital ranges. */
export const VITAL_RANGES_QUERY_KEY = ['clinic', 'settings', 'vital-ranges'] as const;

/**
 * The options in one category.
 *
 * `includeInactive` defaults to `false`, so a dropdown built by F-5 or F-11 gets only what may
 * still be offered without having to remember to ask. The settings editor opts in.
 *
 * `staleTime` is 5 minutes rather than F-3's 60 seconds: these lists change a handful of times in
 * the life of a clinic, and every consultation screen from F-11 on will read one of them.
 */
export function useSettingOptions(
  category: SettingCategoryValue,
  includeInactive = false,
): UseQueryResult<SettingOption[]> {
  return useQuery<SettingOption[]>({
    queryKey: settingOptionsQueryKey(category, includeInactive),
    queryFn: ({ signal }) => clinicApi.getOptions(category, includeInactive, signal),
    staleTime: 5 * 60_000,
  });
}

/** Every metric's threshold, configured or not - the list is never short (E-12). */
export function useVitalRanges(): UseQueryResult<VitalRange[]> {
  return useQuery<VitalRange[]>({
    queryKey: VITAL_RANGES_QUERY_KEY,
    queryFn: ({ signal }) => clinicApi.getVitalRanges(signal),
    staleTime: 5 * 60_000,
  });
}

/**
 * Saves a whole category list.
 *
 * Invalidates *both* variants of the category's key on success. That matters: the editor holds the
 * `includeInactive: true` list and a dropdown elsewhere holds the `false` one, and retiring an
 * option must not leave that dropdown still offering it until its staleTime elapses.
 */
export function useSaveSettingOptions(category: SettingCategoryValue) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (items: SettingOptionItemRequest[]) => clinicApi.saveOptions(category, items),
    onSuccess: (saved) => {
      queryClient.setQueryData(settingOptionsQueryKey(category, true), saved);
      queryClient.setQueryData(
        settingOptionsQueryKey(category, false),
        saved.filter((option) => option.isActive),
      );
      void queryClient.invalidateQueries({ queryKey: ['clinic', 'settings', 'options', category] });
    },
  });
}

/** Saves the thresholds. */
export function useSaveVitalRanges() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (items: VitalRangeItemRequest[]) => clinicApi.saveVitalRanges(items),
    onSuccess: (saved) => {
      queryClient.setQueryData(VITAL_RANGES_QUERY_KEY, saved);
    },
  });
}
