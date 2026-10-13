// Seed — the exemplar every generated E2E test copies (/10x-e2e-setup).
// Risk #3 (context/foundation/test-plan.md): the interactive page cannot connect, so the user's action is lost.
// Local slice of the Phase 2 critical flow: adding a product works only over the live interactive connection,
// and what was added must still be there after a reload.
import { test, expect } from '@playwright/test';
import { waitForInteractive } from './support/blazor';

test('a product added over the live interactive connection persists after page reload', async ({ page }) => {
  const productName = `E2E produkt ${Date.now()}`;
  await page.goto('/products');
  // "Dodaj" creates a record, so don't retry it: wait once for the circuit, then fill and submit.
  await waitForInteractive(page);

  await page.getByLabel('Nazwa').fill(productName);
  await page.getByRole('radio', { name: 'Zużyj w pierwszej kolejności' }).check();
  await page.getByRole('button', { name: 'Dodaj' }).click();

  const product = page.getByRole('listitem').filter({ hasText: productName });
  await expect(product).toBeVisible();

  await page.reload();
  await expect(product).toBeVisible();

  // Cleanup through the UI, asserted, so a cleanup that fails turns the run red.
  await waitForInteractive(page);
  await page.getByRole('button', { name: `Usuń ${productName}` }).click();
  await product.getByRole('button', { name: 'Usuń', exact: true }).click();
  await expect(product).toBeHidden();
});
