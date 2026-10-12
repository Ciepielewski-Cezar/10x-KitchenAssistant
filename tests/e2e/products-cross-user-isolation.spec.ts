// risk: test-plan.md #4 — abuse (access): a signed-in user reads or changes another user's products through the page flow
// facet: the live (interactive) /products page of user B never lists user A's product, so B can neither see nor
//        act on it. Anonymous redirects and prerendered-HTML isolation belong to the Phase 2 HTTP integration test.
// seed: tests/e2e/seed.spec.ts
import { test as base, expect, type Page } from '@playwright/test';
import { waitForInteractive } from './support/blazor';

const test = base.extend<{ pageB: Page; products: { a: string; b: string } }>({
  // User B in a browser context of their own; `page` stays user A (the project's storageState).
  pageB: async ({ browser, baseURL }, use) => {
    const context = await browser.newContext({ baseURL, storageState: 'playwright/.auth/user-b.json' });
    await use(await context.newPage());
    await context.close();
  },
  products: async ({}, use, testInfo) => {
    const stamp = `${Date.now()}-${testInfo.workerIndex}`;
    const products = { a: `E2E produkt A ${stamp}`, b: `E2E produkt B ${stamp}` };
    testInfo.annotations.push({ type: 'test-data', description: `${products.a}; ${products.b}` });
    await use(products);
  },
});

async function openProducts(page: Page) {
  await page.goto('/products');
  await waitForInteractive(page);
  // The section headings render only once the circuit has loaded the list.
  await expect(page.getByRole('heading', { name: 'W szafkach i zamrażalniku', level: 2 })).toBeVisible();
}

async function addProduct(page: Page, name: string) {
  await page.getByLabel('Nazwa').fill(name);
  await page.getByRole('radio', { name: 'Zużyj w pierwszej kolejności' }).check();
  await page.getByRole('button', { name: 'Dodaj' }).click();
}

// Removes the product if the test got far enough to add it, then proves it is gone after a reload.
async function removeProduct(page: Page, name: string) {
  await openProducts(page);
  const row = page.getByRole('listitem').filter({ hasText: name });
  if ((await row.count()) > 0) {
    await page.getByRole('button', { name: `Usuń ${name}` }).click();
    await row.getByRole('button', { name: 'Usuń', exact: true }).click();
    await expect(row).toBeHidden();
  }
  await page.reload();
  await expect(page.getByRole('heading', { name: 'W szafkach i zamrażalniku', level: 2 })).toBeVisible();
  await expect(row).toHaveCount(0);
}

test.describe('Risk #4 — a user never sees another user\'s products', () => {
  test.afterEach(async ({ page, pageB, products }) => {
    await removeProduct(page, products.a);
    await removeProduct(pageB, products.b);
  });

  test('a product one user adds never appears on another user\'s live products page', async ({ page, pageB, products }) => {
    // User A adds a product over the live connection and sees it.
    await openProducts(page);
    await addProduct(page, products.a);
    await expect(page.getByRole('listitem').filter({ hasText: products.a })).toBeVisible();

    // User B adds a product of their own: the add reloads B's list on B's circuit, so once it shows,
    // the list on screen is the live one, loaded with the circuit's identity.
    await openProducts(pageB);
    await addProduct(pageB, products.b);
    await expect(pageB.getByRole('listitem').filter({ hasText: products.b })).toBeVisible();

    // A's product is not on B's page: no row, so no "Zmień" or "Usuń" for it either.
    await expect(pageB.getByRole('listitem').filter({ hasText: products.a })).toHaveCount(0);
  });
});
