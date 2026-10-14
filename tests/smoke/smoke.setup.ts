import { test as setup, expect } from '@playwright/test';

const authFile = 'playwright/.auth/smoke.json';

setup('sign in the smoke account, registering it on a fresh environment', async ({ page }) => {
  const username = process.env.SMOKE_USERNAME;
  const password = process.env.SMOKE_PASSWORD;
  if (!username || !password) {
    throw new Error('Set SMOKE_USERNAME and SMOKE_PASSWORD (see context/foundation/test-stack.md, ## Smoke)');
  }

  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(username);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Log in', exact: true }).click();

  // Either the sign-in worked, or the account does not exist yet (a fresh database): register it instead.
  const signedIn = page.getByRole('button', { name: 'Logout' });
  const invalid = page.getByText('Invalid login attempt.');
  await expect(signedIn.or(invalid)).toBeVisible();

  if (await invalid.isVisible()) {
    await page.goto('/Account/Register');
    await page.getByLabel('Email').fill(username);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm Password').fill(password);
    await page.getByRole('button', { name: 'Register' }).click();
  }

  // Production has no email confirmation, so registering signs the user straight in.
  await expect(signedIn).toBeVisible();
  await page.context().storageState({ path: authFile });
});
