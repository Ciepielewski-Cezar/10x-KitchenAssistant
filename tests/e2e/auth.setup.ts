import { test as setup, expect } from '@playwright/test';

const authFile = 'playwright/.auth/user.json';

setup('sign in once and save the session', async ({ page }) => {
  const username = process.env.E2E_USERNAME;
  const password = process.env.E2E_PASSWORD;
  if (!username || !password) {
    throw new Error('Set E2E_USERNAME and E2E_PASSWORD (see context/foundation/test-stack.md, ## E2E)');
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
