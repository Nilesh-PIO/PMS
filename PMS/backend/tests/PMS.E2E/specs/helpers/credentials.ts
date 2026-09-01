import type { Page } from '@playwright/test';

/**
 * The credential the running instance is seeded with.
 *
 * Defaults to the values committed in `PMS.Api/appsettings.json` under `SeedUser` - see the
 * DEVIATION note there and in `InitialUserSeedExtensions.cs`. Override with the
 * PMS_E2E_USERNAME / PMS_E2E_PASSWORD environment variables when running against an instance
 * whose credential was supplied properly, through user-secrets or the environment.
 */
export const SEED_USER_NAME = process.env.PMS_E2E_USERNAME ?? 'doctor';
export const SEED_PASSWORD = process.env.PMS_E2E_PASSWORD ?? 'SeedDoctor#2026!';

/** Signs in through the real login form and waits for the app shell. */
export async function signIn(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('User name').fill(SEED_USER_NAME);
  await page.getByLabel('Password').fill(SEED_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await page.getByRole('navigation', { name: 'Main' }).waitFor();
}
