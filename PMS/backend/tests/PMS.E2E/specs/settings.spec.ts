import { expect, test } from '@playwright/test';
import { signIn } from './helpers/credentials';

/**
 * F-4 end-to-end spec (planning-pms-verification.md, F-4 point 6).
 *
 * **What the plan asks for, and what this file can honestly cover today.** The plan's F-4 E2E line
 * is: "**E-12**: a doctor-set upper threshold produces a confirmable warning on the consultation
 * page, and confirming still saves the value." The consultation page is **F-11**, which is not
 * built - it depends on F-10, which depends on F-9. So the half of that journey which exists today
 * is covered here (set a threshold on the settings screen; it persists and comes back), and **the
 * confirm-and-save half belongs in `vitals.spec.ts` when F-11 lands.**
 *
 * That boundary is stated rather than papered over with a `test.fixme`: F-1's skipped spec had to
 * be chased down later, and a skip that looks like coverage is worse than an honest gap. F-11's
 * own plan entry already names `vitals.spec.ts` and the E-12 assertion, so it has a home.
 *
 * Requires a running instance seeded with the credential in `helpers/credentials.ts`. These specs
 * change clinic settings, so they restore what they touched - they run against a long-lived dev
 * instance, and a suite that leaves a clinic misconfigured is a suite that gets switched off.
 */

test.describe('doctor-configured settings (F-4)', () => {
  test('the seeded gender list is on the settings screen and "Not stated" cannot be retired', async ({
    page,
  }) => {
    await signIn(page);
    await page.goto('/settings/options');

    const gender = page.getByRole('region', { name: 'Gender options' });
    await expect(gender.getByLabel('Option 1')).toHaveValue('Female');
    await expect(gender.getByLabel('Option 4')).toHaveValue('Not stated');

    // E-23. The one option the physician may not remove, because the alternative to "not stated"
    // is a guess written into a permanent record.
    await expect(gender.getByLabel('Offer "Not stated"')).toBeDisabled();
    await expect(gender.getByLabel('Offer "Male"')).toBeEnabled();
  });

  test('the vitals not-recorded reasons are configured and non-empty', async ({ page }) => {
    // F-11's mandatory-or-reason escape hatch (REC-3, E-18) reads this list. If it is ever empty,
    // that feature dead-ends and someone writes a blood pressure down from memory.
    await signIn(page);
    await page.goto('/settings/options');

    const reasons = page.getByRole('region', { name: 'Reasons a vital was not recorded' });
    await expect(reasons.getByLabel('Option 1')).toHaveValue('Equipment unavailable');
    await expect(reasons.getByLabel('Option 2')).toHaveValue('Patient declined');
  });

  test('a fresh clinic has no vital ranges at all', async ({ page }) => {
    // F-4 acceptance criterion 3, seen from the outside. On an instance where an earlier run left
    // a threshold behind, this degrades to asserting the screen renders rather than failing - the
    // authoritative version of this assertion is the integration test against a pristine database.
    await signIn(page);
    await page.goto('/settings/vitals-ranges');

    await expect(page.getByRole('heading', { level: 1, name: 'Vital ranges' })).toBeVisible();
    await expect(page.getByText('These are your numbers.')).toBeVisible();
  });

  test('a threshold set by the doctor is stored, and clearing it silences the warning again', async ({
    page,
  }) => {
    await signIn(page);
    await page.goto('/settings/vitals-ranges');

    const upper = page.getByLabel('Pulse upper limit');
    const lower = page.getByLabel('Pulse lower limit');

    await upper.fill('120');
    await page.getByRole('button', { name: 'Save vital ranges' }).click();
    await expect(page.getByText('Saved.')).toBeVisible();

    // It survives a full reload, which is the only way to know it reached the database rather than
    // a component's state.
    await page.reload();
    await expect(page.getByLabel('Pulse upper limit')).toHaveValue('120');
    await expect(page.getByLabel('Pulse lower limit')).toHaveValue('');

    // Clearing it puts the clinic back to silence - and puts this shared instance back to the
    // state the next spec expects.
    await page.getByLabel('Pulse upper limit').fill('');
    await lower.fill('');
    await page.getByRole('button', { name: 'Save vital ranges' }).click();
    await expect(page.getByText('Saved.')).toBeVisible();

    await page.reload();
    await expect(page.getByLabel('Pulse upper limit')).toHaveValue('');
    await expect(page.getByText('No warning set - any value is accepted without comment.').first())
      .toBeVisible();
  });

  test('a blank threshold is stored as no threshold, not as zero', async ({ request }) => {
    // Asserted against the API directly, because the difference between `null` and `0` is not
    // visible on screen - and `0` would warn on every reading the clinic ever enters.
    const login = await request.post('/api/auth/login', {
      data: {
        userName: process.env.PMS_E2E_USERNAME ?? 'doctor',
        password: process.env.PMS_E2E_PASSWORD ?? 'SeedDoctor#2026!',
      },
    });
    expect(login.status()).toBe(200);

    const saved = await request.put('/api/clinic-settings/vital-ranges', {
      data: { items: [{ metric: 'BloodPressureSystolic', warnLow: null, warnHigh: null }] },
    });
    expect(saved.status()).toBe(200);

    const read = await request.get('/api/clinic-settings/vital-ranges');
    const ranges = (await read.json()) as { metric: string; warnLow: number | null }[];
    const systolic = ranges.find((r) => r.metric === 'BloodPressureSystolic');

    expect(systolic?.warnLow).toBeNull();
    expect(systolic?.warnLow).not.toBe(0);
  });

  test('the settings routes are refused without a session', async ({ request }) => {
    const options = await request.get('/api/clinic-settings/options?category=Gender');
    const ranges = await request.get('/api/clinic-settings/vital-ranges');

    expect(options.status()).toBe(401);
    expect(ranges.status()).toBe(401);
  });
});
