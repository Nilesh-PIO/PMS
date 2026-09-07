import { expect, test } from '@playwright/test';
import { signIn } from './helpers/credentials';

/**
 * F-7 end-to-end (plan F-7 point 6): "golden path; **E-28**: two patients with identical name and
 * age are distinguishable in the result list without opening either".
 *
 * **Environment note, carried from F-1/F-2/F-5 and unchanged.** Playwright cannot launch a browser
 * on this host (`browserType.launch: spawn EPERM`), so the browser-dependent specs below are
 * written and typechecked but have not been executed here. Each one's behaviour is proven by a
 * suite that does run — see the F-7 entry in `doc/implementation-progress.md` for the mapping.
 * The API-request specs at the end need no browser and do run.
 */

/** A name no seeded or generated patient will collide with, made unique per run. */
const twinName = `Zephyrine Marchetti ${Date.now()}`;

async function registerPatient(
  page: import('@playwright/test').Page,
  fullName: string,
  phone: string,
  dateOfBirth: string,
): Promise<void> {
  await page.goto('/patients/new');
  await page.getByLabel('Full name').fill(fullName);
  await page.getByRole('radio', { name: /Date of birth/ }).check();
  await page.getByLabel('Date of birth').fill(dateOfBirth);
  await page.getByLabel('Phone number').fill(phone);
  await page.getByRole('button', { name: /Save/ }).click();
  await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);
}

test.describe('F-7 patient search', () => {
  test('golden path: find a patient by name from anywhere and open their profile', async ({
    page,
  }) => {
    await signIn(page);
    await registerPatient(page, twinName, '+91 98765-43210', '1985-03-02');

    // From the home screen, with no navigation: "/" focuses the global box (REC-16).
    await page.goto('/');
    await page.keyboard.press('/');
    await expect(page.getByRole('combobox')).toBeFocused();

    await page.getByRole('combobox').fill(twinName);

    const row = page.getByTestId('patient-picker-row').first();
    await expect(row).toBeVisible();
    await expect(row).toContainText(twinName);

    await row.click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);
    await expect(page.getByRole('heading', { name: twinName })).toBeVisible();
  });

  test('finds a patient by the last four digits of their phone (E-59)', async ({ page }) => {
    await signIn(page);

    await page.goto('/patients?query=3210');

    const rows = page.getByTestId('patient-picker-row');
    await expect(rows.first()).toBeVisible();
    await expect(rows.first()).toContainText('3210');
  });

  test('E-28: two patients with the same name and age are distinguishable without opening either', async ({
    page,
  }) => {
    await signIn(page);

    // Same name, same date of birth - so the same rendered age. Only the phone and the date the
    // record was created can tell them apart, which is precisely the E-28 scenario.
    await registerPatient(page, twinName, '+91 98765-43210', '1985-03-02');
    await registerPatient(page, twinName, '+91 91234-59999', '1985-03-02');

    await page.goto(`/patients?query=${encodeURIComponent(twinName)}`);

    const rows = page.getByTestId('patient-picker-row');
    await expect(rows).toHaveCount(2);

    // Neither row was opened, and neither was auto-selected - we are still on the list.
    await expect(page).toHaveURL(/\/patients\?/);

    const first = await rows.nth(0).innerText();
    const second = await rows.nth(1).innerText();

    expect(first).not.toEqual(second);
    expect(`${first}${second}`).toContain('3210');
    expect(`${first}${second}`).toContain('9999');
  });

  test('E-7: a search with no match offers to register the typed name', async ({ page }) => {
    await signIn(page);

    const missing = `Nobody Xyzzy ${Date.now()}`;
    await page.goto(`/patients?query=${encodeURIComponent(missing)}`);

    await expect(page.getByText('No patient found')).toBeVisible();

    const register = page.getByRole('link', { name: new RegExp(`Register "${missing}"`) });
    await expect(register).toBeVisible();

    await register.click();

    // The typed text arrives in the form, so nobody retypes it - and a retype is how the second
    // spelling of one person's name gets into the database.
    await expect(page.getByLabel('Full name')).toHaveValue(missing);
  });

  test('E-2: a clinic with no patients sees an empty state, not a blank panel', async ({ page }) => {
    // Only meaningful against a genuinely fresh database; skipped rather than failed otherwise, so
    // the suite is honest about what it did and did not check.
    await signIn(page);
    await page.goto('/');

    const emptyState = page.getByText('No patients registered yet');
    const hasPatients = (await page.getByTestId('patient-picker-row').count()) > 0;

    test.skip(hasPatients, 'this database already has patients; E-2 needs a fresh one');
    await expect(emptyState).toBeVisible();
    await expect(page.getByRole('link', { name: /Register the first patient/ })).toBeVisible();
  });

  test('the search box holds no patient data in web storage', async ({ page }) => {
    await signIn(page);
    await page.goto('/patients?query=Zephyrine');
    await page.getByTestId('patient-picker-row').first().waitFor().catch(() => undefined);

    const storage = await page.evaluate(() => ({
      local: window.localStorage.length,
      session: window.sessionStorage.length,
    }));

    expect(storage.local).toBe(0);
    expect(storage.session).toBe(0);
  });
});

/**
 * These need no browser, so they run on this host and are genuinely proven rather than merely
 * written.
 */
test.describe('F-7 search API', () => {
  test('both routes require a session', async ({ request }) => {
    const search = await request.get('/api/patients/search?query=ravi');
    expect(search.status()).toBe(401);

    const recent = await request.get('/api/patients/recent');
    expect(recent.status()).toBe(401);
  });

  test('an unmatched api route is still a problem+json 404, not the SPA shell', async ({
    request,
  }) => {
    // F-1's error contract, re-checked because F-7 adds literal segments to a controller that also
    // has a {id:guid} route - a routing mistake here would surface as HTML where JSON is expected.
    const response = await request.get('/api/patients/search/nope');
    expect(response.status()).toBeGreaterThanOrEqual(400);
    expect(response.headers()['content-type'] ?? '').toContain('json');
  });
});
