import { expect, test } from '@playwright/test';

/**
 * F-1 test strategy: "Playwright smoke spec PMS.E2E/app-shell.spec.ts - app loads,
 * unauthenticated user is redirected to /login."
 *
 * The redirect half is authentication behaviour delivered by F-2 (RequireAuth guard) and is
 * marked `fixme` here rather than written as a passing assertion against behaviour that does
 * not exist - a spec that passes today for the wrong reason would stop being a gate the day
 * F-2 lands. F-2 removes the `fixme`.
 */

test.describe('app shell', () => {
  test('the SPA loads and mounts', async ({ page }) => {
    const response = await page.goto('/');

    expect(response?.status()).toBe(200);
    await expect(page.locator('#root')).not.toBeEmpty();
  });

  test('the app shell renders its main navigation', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Patients' })).toBeVisible();
  });

  test('a deep client route survives a hard refresh', async ({ page }) => {
    // Proves the server-side SPA fallback, not just client-side routing.
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

  test.fixme(
    'an unauthenticated user is redirected to /login',
    async ({ page }) => {
      // Delivered by F-2 (RequireAuth route guard). Remove the fixme with that feature.
      await page.goto('/patients');
      await expect(page).toHaveURL(/\/login$/);
    },
  );
});
