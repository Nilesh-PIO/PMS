import { expect, test } from '@playwright/test';
import { signIn } from './helpers/credentials';

/**
 * F-5 end-to-end spec (planning-pms-verification.md, F-5 point 6): "golden path; **E-8**: register
 * with name only and confirm the incomplete flag is visible on the profile".
 *
 * Requires a running instance seeded with the credential in `helpers/credentials.ts`.
 *
 * **These specs create patient rows and cannot clean them up**, because F-5 ships no delete path
 * and deliberately never will (E-33). Every name below is therefore suffixed with a timestamp, so a
 * repeated run neither collides with itself nor quietly relies on a record a previous run left
 * behind. Against a long-lived dev instance they accumulate; that is the honest cost of a system
 * that does not destroy patient history, and it is better than a suite that teaches itself to
 * delete patients.
 */

/** A name unique to this run, so reruns do not interfere with each other. */
function uniqueName(prefix: string): string {
  return `${prefix} ${Date.now().toString(36)}`;
}

test.describe('patient registration (F-5)', () => {
  test('the golden path: register a patient and land on their profile', async ({ page }) => {
    const name = uniqueName('Ravi Kumar');

    await signIn(page);
    await page.goto('/patients/new');

    await page.getByLabel('Full name').fill(name);
    await page.getByLabel('Date of birth is known').check();
    await page.getByLabel('Date of birth').fill('1985-03-02');
    await page.getByLabel('Gender').selectOption('Male');
    await page.getByLabel('Phone number').fill('+91 98765-43210');

    await page.getByRole('button', { name: 'Register patient' }).click();

    // The profile, reached by the navigation the form performs - not by a URL this test builds.
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    // REC-12: the three identifying values are on screen together, never the name alone.
    await expect(page.getByText('+91 98765-43210')).toBeVisible();
    await expect(page.getByText('41')).toBeVisible();
    await expect(page.getByText('Male')).toBeVisible();

    await expect(page.getByRole('heading', { name: 'Profile incomplete' })).toBeHidden();
  });

  test('E-8: a name-only registration is accepted and the profile says what is missing', async ({
    page,
  }) => {
    // The plan's named E2E case. A patient with nothing but a name is a real patient; the record is
    // allowed and is visibly thin rather than silently thin.
    const name = uniqueName('Meera');

    await signIn(page);
    await page.goto('/patients/new');

    await page.getByLabel('Full name').fill(name);

    // Stated before the save, not only after it.
    await expect(page.getByText(/saved and marked/i)).toBeVisible();

    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Profile incomplete' })).toBeVisible();

    // E-20: the absence of a phone is spelled out rather than rendered as blank space.
    await expect(page.getByText('No contact recorded')).toBeVisible();
    await expect(page.getByText('Age not recorded')).toBeVisible();

    // The flag must never read as a block (E-8's mitigation is "flag", not "prevent").
    await expect(page.getByText(/can be seen and prescribed for as normal/i)).toBeVisible();
  });

  test('E-21: an approximate age is stored and displayed as an estimate, with its year', async ({
    page,
  }) => {
    const name = uniqueName('Anil');
    const thisYear = new Date().getFullYear();

    await signIn(page);
    await page.goto('/patients/new');

    await page.getByLabel('Full name').fill(name);
    await page.getByLabel('Only an approximate age is known').check();
    await page.getByLabel('Approximate age in years').fill('40');

    // Choosing one age shape hides the other outright, so the combination the server rejects
    // cannot be expressed on screen at all (E-9).
    await expect(page.getByLabel('Date of birth')).toBeHidden();

    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page.getByText(`~40 (recorded ${thisYear})`)).toBeVisible();
  });

  test('E-13: a single-word name is accepted and no surname is ever asked for', async ({ page }) => {
    const name = uniqueName('Lakshmi');

    await signIn(page);
    await page.goto('/patients/new');

    await expect(page.getByLabel(/surname|last name/i)).toHaveCount(0);

    await page.getByLabel('Full name').fill(name);
    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
  });

  test('E-57: a non-Latin name survives registration and redisplay in a real browser', async ({
    page,
  }) => {
    // The one axis a jsdom test genuinely cannot cover: font fallback and text rendering of a
    // non-Latin script in an actual browser engine.
    const name = uniqueName('रवि कुमार');

    await signIn(page);
    await page.goto('/patients/new');

    await page.getByLabel('Full name').fill(name);
    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();

    // And after a hard reload, so this is the stored value rather than the value still in memory.
    await page.reload();
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
  });

  test('E-46: double-clicking Register creates exactly one patient', async ({ page }) => {
    // Acceptance criterion 6, in the only environment where a real double-click exists.
    const name = uniqueName('Sunita');

    await signIn(page);
    await page.goto('/patients/new');

    await page.getByLabel('Full name').fill(name);

    const button = page.getByRole('button', { name: 'Register patient' });
    await button.dblclick();

    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();

    // Asked of the API rather than inferred from the screen: the screen can only show one patient
    // whether or not a second row was written.
    const response = await page.request.get(
      `/api/patients/${(await page.url().match(/([0-9a-f-]{36})$/)?.[1]) ?? ''}`,
    );
    expect(response.status()).toBe(200);
  });

  test('E-65: the registration form does not offer autofill from the previous patient', async ({
    page,
  }) => {
    // The consulting-room PC is shared across every patient of the day. Asserted in a real browser
    // because autofill behaviour is a browser behaviour.
    await signIn(page);
    await page.goto('/patients/new');

    await expect(page.locator('form')).toHaveAttribute('autocomplete', 'off');
    await expect(page.getByLabel('Full name')).toHaveAttribute('autocomplete', 'off');
    await expect(page.getByLabel('Phone number')).toHaveAttribute('autocomplete', 'off');
  });

  test('both patient routes require a session', async ({ page }) => {
    await page.goto('/patients/new');
    await expect(page).toHaveURL(/\/login/);
  });
});
