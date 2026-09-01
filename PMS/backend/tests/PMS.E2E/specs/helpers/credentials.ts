import type { Page } from '@playwright/test';

/**
 * The credential the running instance is seeded with.
 *
 * Defaults to the values committed in `PMS.Api/appsettings.json` under `SeedDoctorUser` - see the
 * DEVIATION note there and in `InitialUserSeedExtensions.cs`. Override with the
 * PMS_E2E_USERNAME / PMS_E2E_PASSWORD environment variables when running against an instance
 * whose credential was supplied properly, through user-secrets or the environment.
 */
export const SEED_USER_NAME = process.env.PMS_E2E_USERNAME ?? 'doctor';
export const SEED_PASSWORD = process.env.PMS_E2E_PASSWORD ?? 'SeedDoctor#2026!';

/** The clinic identity these specs configure when they meet a fresh database (F-3). */
export const E2E_CLINIC = {
  clinicName: 'E2E Test Clinic',
  doctorName: 'Dr E2E Tester',
  registrationNo: 'E2E-000001',
} as const;

/**
 * Fills and submits the first-run setup form if it is on screen, otherwise does nothing.
 *
 * F-3 puts this screen between sign-in and the rest of the application (E-1), so every spec that
 * needs the app shell has to get past it. Written as "complete it if present" rather than
 * "complete it" so the suite works identically on a fresh database and on one that has already
 * been configured by an earlier spec.
 */
export async function ensureClinicSetup(page: Page): Promise<void> {
  const clinicName = page.getByLabel('Clinic name');

  if (!(await clinicName.isVisible().catch(() => false))) {
    return;
  }

  await clinicName.fill(E2E_CLINIC.clinicName);
  await page.getByLabel("Doctor's name").fill(E2E_CLINIC.doctorName);
  await page.getByLabel('Registration number').fill(E2E_CLINIC.registrationNo);
  await page.getByRole('radio', { name: /Celsius/ }).check();
  await page.getByRole('button', { name: 'Save and continue' }).click();
}

/** Signs in through the real login form, clears the F-3 setup gate, and waits for the app shell. */
export async function signIn(page: Page): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('User name').fill(SEED_USER_NAME);
  await page.getByLabel('Password').fill(SEED_PASSWORD);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await ensureClinicSetup(page);
  await page.getByRole('navigation', { name: 'Main' }).waitFor();
}
