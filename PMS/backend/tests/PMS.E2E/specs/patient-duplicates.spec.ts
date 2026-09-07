import { expect, test } from '@playwright/test';
import { signIn } from './helpers/credentials';

/**
 * F-6 end-to-end spec (planning-pms-verification.md, F-6 point 6): "**E-25**: register the same
 * person twice and confirm the warning appears before the second record is created; **E-27**: two
 * family members on one phone both appear in the candidate list".
 *
 * Requires a running instance seeded with the credential in `helpers/credentials.ts`.
 *
 * **These specs create patient rows and cannot clean them up**, for the same reason F-5's cannot:
 * this application ships no delete path and deliberately never will (E-33). Every name is suffixed
 * with a timestamp so a repeated run neither collides with itself nor relies on a record a previous
 * run left behind.
 *
 * They are written against a *shared household phone number that is also unique to the run*, which
 * matters more here than in F-5: the whole point of these specs is what happens when two records
 * share a number, so a hardcoded number would make every run of this suite start finding the
 * previous run's patients.
 */

function uniqueSuffix(): string {
  return Date.now().toString(36);
}

/** A ten-digit number unique to this run, so the duplicate check only ever sees this run's rows. */
function uniquePhone(): string {
  const digits = String(Date.now()).slice(-9);
  return `9${digits}`;
}

/** Fills the registration form. Does not submit. */
async function fillRegistration(
  page: import('@playwright/test').Page,
  { name, phone, dateOfBirth }: { name: string; phone: string; dateOfBirth?: string },
) {
  await page.goto('/patients/new');
  await page.getByLabel('Full name').fill(name);

  if (dateOfBirth) {
    await page.getByLabel('Date of birth is known').check();
    await page.getByLabel('Date of birth').fill(dateOfBirth);
  }

  await page.getByLabel('Phone number').fill(phone);
}

test.describe('duplicate detection (F-6)', () => {
  test('E-25: registering the same person twice warns before the second record is created', async ({
    page,
  }) => {
    // The plan's first named case, and the failure this whole feature exists to prevent: one
    // patient with two records, half their history on each, and nothing on screen saying so.
    const name = `Ravi Kumar ${uniqueSuffix()}`;
    const phone = uniquePhone();

    await signIn(page);

    await fillRegistration(page, { name, phone, dateOfBirth: '1985-03-02' });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    // Second registration, same person, same details.
    await fillRegistration(page, { name, phone, dateOfBirth: '1985-03-02' });
    await page.getByRole('button', { name: 'Register patient' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(name)).toBeVisible();

    // Still on the form. Nothing was written - the warning precedes the insert, which is what makes
    // it a duplicate check rather than a duplicate report.
    await expect(page).toHaveURL(/\/patients\/new$/);
  });

  test('E-25: the warning is dismissible and confirming registers the patient', async ({ page }) => {
    // REC-2: warn, never block. A blocking rule turns a false positive into a patient who cannot be
    // registered, and the desk's answer to that is to invent a spelling.
    const name = `Ravi Kumar ${uniqueSuffix()}`;
    const phone = uniquePhone();

    await signIn(page);

    await fillRegistration(page, { name, phone });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    await fillRegistration(page, { name, phone });
    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page.getByRole('dialog')).toBeVisible();
    await page.getByRole('button', { name: 'Register anyway' }).click();

    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible();
  });

  test('going back from the warning keeps every typed character', async ({ page }) => {
    // The physician's next action is to look at a record and come back. A form that cleared itself
    // would make them retype everything (E-47).
    const name = `Ravi Kumar ${uniqueSuffix()}`;
    const phone = uniquePhone();

    await signIn(page);

    await fillRegistration(page, { name, phone });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    await fillRegistration(page, { name, phone });
    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page.getByRole('dialog')).toBeVisible();
    await page.getByRole('button', { name: 'Go back and check' }).click();

    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(page.getByLabel('Full name')).toHaveValue(name);
    await expect(page.getByLabel('Phone number')).toHaveValue(phone);
  });

  test('E-27: two family members on one phone both appear in the candidate list', async ({
    page,
  }) => {
    // The plan's second named case. A phone number identifies a household, not a person, so both
    // relatives are shown - and neither is presented as a duplicate, because their names are
    // nothing alike. Showing them as context is what stops the dialog from crying wolf.
    const suffix = uniqueSuffix();
    const phone = uniquePhone();
    const ravi = `Ravi Kumar ${suffix}`;
    const sunita = `Sunita Kumar ${suffix}`;

    await signIn(page);

    await fillRegistration(page, { name: ravi, phone });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    // A different person on the same household number registers without any interruption.
    await fillRegistration(page, { name: sunita, phone });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    // Now a genuine duplicate of the first: the dialog shows the suspected duplicate *and* names
    // the other household member as context.
    await fillRegistration(page, { name: ravi, phone });
    await page.getByRole('button', { name: 'Register patient' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText(ravi)).toBeVisible();
    await expect(dialog.getByText('1 other patient uses this phone number')).toBeVisible();
    await expect(dialog.getByText(sunita)).toBeVisible();
  });

  test('E-28: every candidate row shows more than a name', async ({ page }) => {
    // RSK-12, rated Critical. This is the screen where the physician compares near-identical names,
    // so a row showing only a name is the wrong-patient path.
    const name = `Ravi Kumar ${uniqueSuffix()}`;
    const phone = uniquePhone();

    await signIn(page);

    await fillRegistration(page, { name, phone, dateOfBirth: '1985-03-02' });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    await fillRegistration(page, { name, phone, dateOfBirth: '1985-03-02' });
    await page.getByRole('button', { name: 'Register patient' }).click();

    const row = page.getByRole('dialog').getByRole('listitem').first();

    await expect(row.getByText(`…${phone.slice(-4)}`)).toBeVisible();
    await expect(row.getByText('Age')).toBeVisible();
    await expect(row.getByText('Date of birth')).toBeVisible();
    // Present and rendered even though no Visit entity exists until F-10.
    await expect(row.getByText('No visits recorded')).toBeVisible();
  });

  test('Q-13: a country code and a trunk zero are recognised as one number', async ({ page }) => {
    // The gap verification-pms reported against F-5, seen from the browser. The same person typed
    // with "+91" one week and a leading "0" the next must not become two records.
    const name = `Ravi Kumar ${uniqueSuffix()}`;
    const subscriber = uniquePhone();

    await signIn(page);

    await fillRegistration(page, { name, phone: `+91 ${subscriber}` });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    await fillRegistration(page, { name, phone: `0${subscriber}` });
    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page).toHaveURL(/\/patients\/new$/);
  });

  test('an unrelated patient registers with no warning at all', async ({ page }) => {
    // The check has to be quiet in the ordinary case, or it becomes something to dismiss without
    // reading - including on the day it is right.
    const suffix = uniqueSuffix();

    await signIn(page);

    await fillRegistration(page, { name: `Ravi Kumar ${suffix}`, phone: uniquePhone() });
    await page.getByRole('button', { name: 'Register patient' }).click();
    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);

    await fillRegistration(page, { name: `Priya Menon ${suffix}`, phone: uniquePhone() });
    await page.getByRole('button', { name: 'Register patient' }).click();

    await expect(page).toHaveURL(/\/patients\/[0-9a-f-]{36}$/);
    await expect(page.getByRole('dialog')).toBeHidden();
  });
});
