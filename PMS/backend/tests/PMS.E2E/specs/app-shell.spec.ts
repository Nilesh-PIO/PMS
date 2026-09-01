import { expect, test } from '@playwright/test';
import { signIn } from './helpers/credentials';

/**
 * F-1 test strategy: "Playwright smoke spec PMS.E2E/app-shell.spec.ts - app loads,
 * unauthenticated user is redirected to /login."
 *
 * F-2 completes it. The redirect assertion below was a `test.fixme` while `RequireAuth` did
 * not exist; it is now a real gate. The two specs that reach the app shell sign in first,
 * because from F-2 onward everything under `/` is behind that guard - they assert the shell,
 * not the guard, and `auth.spec.ts` asserts the guard.
 */

test.describe('app shell', () => {
  test('the SPA loads and mounts', async ({ page }) => {
    const response = await page.goto('/');

    expect(response?.status()).toBe(200);
    await expect(page.locator('#root')).not.toBeEmpty();
  });

  test('the app shell renders its main navigation', async ({ page }) => {
    await signIn(page);

    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Patients' })).toBeVisible();
  });

  test('a deep client route survives a hard refresh', async ({ page }) => {
    // Proves the server-side SPA fallback, not just client-side routing: the server must hand
    // back the SPA shell with a 200 even for a route only React Router knows about.
    await signIn(page);

    const response = await page.goto('/patients/00000000-0000-0000-0000-000000000000');

    expect(response?.status()).toBe(200);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('the health endpoint answers', async ({ request }) => {
    const response = await request.get('/api/health');

    expect(response.status()).toBe(200);
    expect((await response.json()).status).toBe('Healthy');
  });

  test('an unknown API route returns ProblemDetails, never the SPA shell', async ({ request }) => {
    const response = await request.get('/api/not-a-real-endpoint');

    expect(response.status()).toBe(404);
    expect(response.headers()['content-type']).toContain('application/problem+json');
  });

  test('no auth token is written to browser storage', async ({ page }) => {
    // Section 2: cookie auth, never a token in web storage (E-62, E-65).
    await page.goto('/');

    const storage = await page.evaluate(() => ({
      local: window.localStorage.length,
      session: window.sessionStorage.length,
    }));

    expect(storage.local).toBe(0);
    expect(storage.session).toBe(0);
  });

  test('an unauthenticated user is redirected to /login', async ({ page }) => {
    // Was `test.fixme` under F-1 because RequireAuth did not exist. F-2 delivers it, so this
    // is now a real assertion rather than a deferred one.
    await page.goto('/patients');

    await expect(page).toHaveURL(/\/login$/);
  });
});
