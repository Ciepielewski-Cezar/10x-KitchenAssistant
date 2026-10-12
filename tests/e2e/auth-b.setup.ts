import { test as setup, expect } from '@playwright/test';

// A second test user, for risks that need two accounts (risk #4: one user's products never reach another).
// Specs that act as this user open their own context with this file; the default storageState stays user A.
const authFile = 'playwright/.auth/user-b.json';

setup('sign in as the second user and save the session', async ({ page }) => {
  const username = process.env.E2E_USERNAME_B;
  const password = process.env.E2E_PASSWORD_B;
  if (!username || !password) {
    throw new Error('Set E2E_USERNAME_B and E2E_PASSWORD_B in .env (see .env.example)');
  }

  // The Account pages are static SSR (a plain form post, no circuit), so there is no hydration race to wait out.
  await page.goto('/Account/Login');
  await page.getByLabel('Email').fill(username);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Log in', exact: true }).click();

  // Wait for a state only a signed-in user reaches.
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'));
  await expect(page.getByRole('button', { name: 'Logout' })).toBeVisible();

  await page.context().storageState({ path: authFile });
});
