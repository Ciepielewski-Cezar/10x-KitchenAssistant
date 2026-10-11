# AI Failure Surfacing (test-plan Phase 1) — Plan Brief

> Full plan: `context/changes/testing-ai-failure-surfacing/plan.md`
> Research: `context/changes/testing-ai-failure-surfacing/research.md`

## What & Why

Test-plan risk #1: if the AI fails, times out, refuses or returns unusable output, the user could see an error page, a frozen page or a silent empty list instead of a clear state they can retry from. Risk #6: per-request caps and product-name handling might not hold. This phase proves, with tests, that every generation outcome reaches `/recipes` as the distinct, recoverable state the archived plans define, and closes the remaining unit gaps for both risks.

## Starting Point

- **The page appears correct but nothing proves it.** The service maps outcomes to four statuses, and the page's catch-all handles everything else. There are no page tests, so a regression in how outcomes are shown would pass the suite.
- **One state is not recoverable.** A failed products load shows a message with no retry.
- **Unit coverage is broad but leaves gaps.** SDK network-error wrapping is assumed, not proved. Some stop reasons, statuses, name-sanitising variants and the 5-proposal cap at service level are untested.

## Desired End State

- **Page suite.** `dotnet test` runs bUnit tests that drive `/recipes` through every outcome and assert the exact Polish text and controls from the archived plans:
  - generating
  - succeeded
  - each failure kind, including the 61 s deadline
  - retry after a failure
  - none within limits
  - no products
  - user leaves mid-generation
- **New behaviour.** A failed products load offers „Spróbuj ponownie” and recovers without a page reload.
- **Unit gaps.** The agreed gaps are covered.
- **Mutation check.** One Stryker run on the generation core has been triaged.
- **Cookbook.** test-plan §6.2 describes the pattern for future page tests.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Oracle for page states | Archived plans' wording (first-recipe-generation, recipe-ranking, meal-parameters) | The PRD has no error-state text; those plans fixed it and the code was built to them. | Research |
| Test layer | bUnit over the real `RecipeService` and `ProductService`, generator scripted at `IRecipeGenerator` | Cheapest layer that shows what the user sees; stubbing the service would mirror the code. | Research + test-plan §2 |
| Products-load failure | Add a „Spróbuj ponownie” that reloads products, written test-first | Test-plan #1 requires a recoverable state; matches the Failed alert's pattern. | Plan |
| Unexpected exceptions | Assert at page level only; fix the misleading `RecipeService` comment | Tests what the user sees with no behaviour change; a service catch-all could hide bugs. | Plan |
| Cap upper bounds | None; lower bounds suffice | Configuration is owner-controlled and the spend limit covers a runaway value; no source defines bounds. | Plan |
| `MaxRetries` / per-attempt timeout | Pinned in test-plan Phase 2 (Production composition) | That test resolves the real client from DI without refactoring. | Plan |
| Unit depth | Targeted gaps + one ad hoc Stryker run | Closes assumed-not-proved SDK behaviour and shows weak assertions. | Plan |

## Scope

**In scope:**
- bUnit package and shared page-test support (SQLite, scripted generator, failing DB factory, auth, `FakeTimeProvider`)
- Page tests for every generation outcome
- Products-load retry (page change + tests)
- Corrected `RecipeService` header comment
- Unit tests:
  - network-error wrapping
  - HTTP statuses 400/401/429/500
  - the `pause_turn` and null stop reasons
  - null ingredient
  - more than 5 usable recipes
  - `\r`, `\r\n`, U+2028 and tab in names
  - the 100/101-character name boundary
- `dotnet-stryker` local tool, one triaged run, test-plan §6.2 / §6.6 / §4 update

**Out of scope:**
- upper bounds on caps
- widening the service catch
- retries and timeout pinning (Phase 2)
- a per-user throttle
- `[Authorize]` and live-circuit tests (Phase 2)
- e2e tests, CI and hooks
- refactoring existing test helpers
- multi-block responses

## Architecture / Approach

The page test renders `RecipeSuggestions` in a bUnit context:
- **Services:** the real `RecipeService` and `ProductService` on in-memory SQLite, with one shared `FakeTimeProvider`.
- **Seams:** the generator is scripted at `IRecipeGenerator`, which returns raw JSON, so the real parse → classify → rank path runs. The products-load failure is injected at `IDbContextFactory`.
- **In-flight states:** held open with a `TaskCompletionSource`, so no test waits on the clock.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. bUnit harness | Package, shared support, smoke render | bUnit 2.x vs net10.0 / xUnit 2.9.3 compatibility |
| 2. Page outcome → state tests | Every existing outcome pinned to the archived wording; comment fix | bUnit may not dispatch clicks to a disabled button (double-click guard) |
| 3. Products-load retry (`/10x-tdd`) | Retry button + reload, test-first | Interaction with the initial-load cancellation handling |
| 4. Unit gaps | Generator, service, classifier, prompt and product-service edge cases | The SDK may not wrap `HttpRequestException`; if so, stop and decide |
| 5. Mutation gate + cookbook | Stryker run triaged; test-plan §6.2 / §6.6 filled | Stryker.NET 5.0.0 support for net10.0 |

**Prerequisites:** `dotnet tool restore`; LocalDB for the Phase 3 manual check; no Anthropic key needed.
**Estimated effort:** ~3–4 sessions across 5 phases.

## Open Risks & Assumptions

- **SDK wrapping.** Assumes the SDK wraps network errors as `AnthropicException`. Phase 4 verifies this and stops if not.
- **Stryker survivors.** Logging-only mutants are expected to survive (`NullLogger`); the report records them as ignored.
- **Router not exercised.** Page tests do not cover the router's `[Authorize]` or the SignalR circuit. Test-plan Phase 2 does.

## Success Criteria (Summary)

- **No silent failure.** Breaking any status → state branch on `/recipes` makes a test fail. No outcome can surface as a blank list or an unhandled exception without the suite noticing.
- **Load recovery.** A user whose products fail to load can retry in place.
- **Pattern documented.** The cookbook lets the next phase add a page test without reading this plan.
