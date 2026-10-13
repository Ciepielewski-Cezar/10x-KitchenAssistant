import type { Page } from '@playwright/test';

/**
 * Waits until every prerendered interactive component on the page is live on its circuit.
 *
 * An `@rendermode InteractiveServer` page is first served as static HTML. Until the circuit renders it, clicks and
 * typing are lost (or post a static form), so a non-idempotent action such as "Dodaj" must not run before this.
 * The prerendered HTML marks each component with a `<!--Blazor:{...}-->` comment; blazor.web.js (10.0) renames it
 * `bl-root` when it boots and to `!` when the component's first interactive render arrives. So: no `Blazor:` and
 * no `bl-root` comment left means the page is interactive. This is the one DOM query the suite allows; keep it
 * here rather than in specs.
 */
export async function waitForInteractive(page: Page): Promise<void> {
  await page.waitForFunction(() => {
    const comments = document.createTreeWalker(document, NodeFilter.SHOW_COMMENT);
    while (comments.nextNode()) {
      if (/^\s*(Blazor|\/?bl-root)/.test(comments.currentNode.nodeValue ?? '')) return false;
    }
    return true;
  });
}
