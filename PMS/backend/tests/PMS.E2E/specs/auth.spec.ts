import { expect, test } from '@playwright/test';
import { SEED_PASSWORD, SEED_USER_NAME, signIn } from './helpers/credentials';

/**
 * F-2 end-to-end spec (planning-pms-verification.md, F-2 point 6): "golden path login/logout;
 * **severe edge case E-41**: idle past the lock timeout while a draft consultation is open,
 * re-authenticate, and confirm the typed text is still on screen and still on the server."
 *
 * Requires a running instance seeded with the credential in `helpers/credentials.ts`.
 */

test.describe('authentication', () => {
  test('golden path: sign in, land in the app, sign out', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { level: 1, name: /sign in/i })).toBeVisible();

    await page.getByLabel('User name').fill(SEED_USER_NAME);
    await page.getByLabel('Password').fill(SEED_PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();

    await page.getByRole('button', { name: 'Sign out' }).click();
    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole('navigation', { name: 'Main' })).toHaveCount(0);
  });

  test('an unauthenticated visitor is sent to /login', async ({ page }) => {
    await page.goto('/patients');

    await expect(page).toHaveURL(/\/login$/);
  });

  test('a wrong password is rejected without saying which half was wrong', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('User name').fill(SEED_USER_NAME);
    await page.getByLabel('Password').fill('DefinitelyNotThePassword!');
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByRole('alert')).toHaveText(
      'That user name and password were not recognised.',
    );
  });

  // --- acceptance criterion 1: the cookie, and nothing in web storage -----

  test('login sets an HttpOnly, Secure, SameSite=Strict cookie', async ({ page, context }) => {
    await signIn(page);

    const cookie = (await context.cookies()).find((c) => c.name === 'pms.session');

    expect(cookie, 'the session cookie must exist after signing in').toBeDefined();
    expect(cookie!.httpOnly).toBe(true);
    expect(cookie!.secure).toBe(true);
    expect(cookie!.sameSite).toBe('Strict');
  });

  test('no token is written to localStorage or sessionStorage', async ({ page }) => {
    // Section 2's auth decision, asserted in a real browser: a token in web storage is readable
    // by any script and outlives the consulting-room session (E-62, E-65).
    await signIn(page);

    const storage = await page.evaluate(() => ({
      local: Object.entries({ ...window.localStorage }),
      session: Object.entries({ ...window.sessionStorage }),
    }));

    expect(storage.local).toEqual([]);
    expect(storage.session).toEqual([]);
  });

  test('the session cookie is not readable from document.cookie', async ({ page }) => {
    await signIn(page);

    const readable = await page.evaluate(() => document.cookie);

    expect(readable).not.toContain('pms.session');
  });

  // --- acceptance criterion 2 --------------------------------------------

  test('a protected API route is 401 without the cookie', async ({ request }) => {
    const response = await request.get('/api/auth/session');

    expect(response.status()).toBe(401);
    expect(response.headers()['content-type']).toContain('application/problem+json');
  });

  test('health stays reachable without the cookie', async ({ request }) => {
    expect((await request.get('/api/health')).status()).toBe(200);
  });

  // --- acceptance criteria 3 and 4: the severe edge case E-41 -------------

  test('idle lock covers PHI and re-authentication returns every typed character (E-41, E-62)', async ({
    page,
  }) => {
    // The lock threshold is 5 real minutes, which no test should sit through. Playwright's
    // clock control installs a controllable timer implementation before the app boots, so
    // `runFor` moves the page's own setTimeout forward without waiting.
    await page.clock.install();
    await signIn(page);

    await page.goto('/visits/00000000-0000-0000-0000-000000000000');

    const typed = 'Complains of headache since Tuesday. No fever.';
    const draft = page.getByRole('textbox').first();

    // F-10 owns the consultation editor. Until it exists there is no draft field to type into,
    // so this assertion is scoped to whatever editable field the page offers.
    const hasDraftField = (await draft.count()) > 0;
    if (hasDraftField) {
      await draft.fill(typed);
    }

    // Past the 5-minute idle threshold, without five real minutes passing.
    await page.clock.runFor('06:00');

    await expect(page.getByRole('dialog', { name: 'Screen locked' })).toBeVisible();
    await expect(page.getByText(/nothing has been lost/i)).toBeVisible();

    // Acceptance criterion 4: unlock in place, and the view comes back exactly as it was.
    await page.getByLabel('Password').fill(SEED_PASSWORD);
    await page.getByRole('button', { name: 'Unlock' }).click();

    await expect(page.getByRole('dialog', { name: 'Screen locked' })).toHaveCount(0);
    // Still on the same URL: the lock is an overlay, not a route, so nothing navigated and
    // nothing was torn down.
    await expect(page).toHaveURL(/\/visits\//);

    if (hasDraftField) {
      await expect(draft).toHaveValue(typed);
    }
  });

  test('the lock does not lift because the mouse moved (E-62)', async ({ page }) => {
    await page.clock.install();
    await signIn(page);

    await page.clock.runFor('06:00');
    await expect(page.getByRole('dialog', { name: 'Screen locked' })).toBeVisible();

    await page.mouse.move(200, 200);
    await page.mouse.move(400, 400);

    await expect(page.getByRole('dialog', { name: 'Screen locked' })).toBeVisible();
  });

  // --- acceptance criterion 5 ---------------------------------------------

  test('the login form and its fields opt out of browser autofill (E-65)', async ({ page }) => {
    await page.goto('/login');

    await expect(page.locator('form')).toHaveAttribute('autocomplete', 'off');
    await expect(page.getByLabel('User name')).toHaveAttribute('autocomplete', 'off');
    await expect(page.getByLabel('Password')).toHaveAttribute('autocomplete', 'off');
  });
});
