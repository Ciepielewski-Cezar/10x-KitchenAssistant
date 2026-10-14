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

  // Either the sign-in worked, or the account does not exist yet (a fresh database). Registering needs an explicit
  // SMOKE_ALLOW_REGISTER=1, so a mistyped secret fails the run instead of creating a stray account on a deployed site.
  const signedIn = page.getByRole('button', { name: 'Logout' });
  const invalid = page.getByText('Invalid login attempt.');
  await expect(signedIn.or(invalid)).toBeVisible();

  if (await invalid.isVisible()) {
    if (process.env.SMOKE_ALLOW_REGISTER !== '1') {
      throw new Error('The smoke account cannot sign in. Check SMOKE_USERNAME/SMOKE_PASSWORD, or set SMOKE_ALLOW_REGISTER=1 to register it on a fresh environment.');
    }

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
