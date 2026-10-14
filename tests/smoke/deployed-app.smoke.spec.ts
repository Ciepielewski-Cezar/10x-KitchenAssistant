// risk: test-plan.md #3 — silent production misconfiguration: the deployed app starts but its AI path or live connection is broken
// facet: beyond liveness — a signed-in page, the interactive circuit, and a real recipe generation
// run: npm run smoke (SMOKE_BASE_URL, SMOKE_USERNAME, SMOKE_PASSWORD); spends one real AI call
import { test, expect, type Page } from '@playwright/test';
import { waitForInteractive } from '../e2e/support/blazor';

const productsHeading = (page: Page) => page.getByRole('heading', { name: 'W szafkach i zamrażalniku', level: 2 });

test('the app is alive', async ({ request }) => {
  const response = await request.get('/healthz');
  expect(response.status()).toBe(200);
});

test.describe('signed-in flow over the live connection', () => {
  let productName: string | undefined;

  // Removes the product if the test got far enough to add it.
  test.afterEach(async ({ page }) => {
    if (!productName) return;
    const name = productName;
    await page.goto('/products');
    await waitForInteractive(page);
    await expect(productsHeading(page)).toBeVisible();
    const row = page.getByRole('listitem').filter({ hasText: name });
    if ((await row.count()) > 0) {
      await page.getByRole('button', { name: `Usuń ${name}` }).click();
      await row.getByRole('button', { name: 'Usuń', exact: true }).click();
      await expect(row).toBeHidden();
    }
    await page.reload();
    await expect(productsHeading(page)).toBeVisible();
    await expect(row).toHaveCount(0);
  });

  test('add a product, generate recipes with the real AI and open one', async ({ page }) => {
    const name = `Smoke produkt ${Date.now()}`;
    productName = name;

    await page.goto('/products');
    await waitForInteractive(page);
    await page.getByLabel('Nazwa').fill(name);
    await page.getByRole('radio', { name: 'Zużyj w pierwszej kolejności' }).check();
    await page.getByRole('button', { name: 'Dodaj' }).click();
    await expect(page.getByRole('listitem').filter({ hasText: name })).toBeVisible();

    await page.goto('/recipes');
    await waitForInteractive(page);
    await page.getByRole('button', { name: 'Zaproponuj przepisy' }).click();

    const show = page.getByRole('button', { name: 'Pokaż przepis' }).first();
    await expect(show).toBeVisible({ timeout: 90_000 });
    await show.click();
    await expect(page.getByRole('button', { name: 'Ukryj przepis' }).first()).toBeVisible();
  });
});
