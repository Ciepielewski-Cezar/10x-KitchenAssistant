# AI Failure Surfacing (test-plan Phase 1) Implementation Plan

## Overview

Prove that every AI recipe-generation outcome reaches `/recipes` as the distinct, recoverable state the archived plans define, and that the per-request caps and product-name handling hold. This is `context/foundation/test-plan.md` §3 Phase 1, covering risks #1 and #6.

The work:
- adds bUnit page tests over the real services with a scripted generator;
- adds the one missing recovery path, a retry after a failed products load, test-first;
- closes the targeted unit gaps the research found;
- checks the strength of the assertions with a single Stryker.NET run.

## Current State Analysis

Research: `context/changes/testing-ai-failure-surfacing/research.md`.

- **Service.** `RecipeService.GenerateAsync` returns four statuses: `Succeeded`, `NoProducts`, `Failed` and `NoneWithinLimits` (`Recipes/RecipeModels.cs:73-81`).
  - It catches three exception types and returns `Failed` for them: OCE not caused by the caller, `RecipeGenerationException` and `AnthropicException` (`Recipes/RecipeService.cs:98-120`).
  - Every other exception escapes, including any from `GetProductsAsync` (`:28`, outside the try).
- **Page.** `RecipeSuggestions.razor` switches on the status (`:170-189`).
  - Its `catch (Exception)` turns escaped exceptions into the Failed alert (`:195-199`).
  - A re-entry guard (`:155-158`) and disabled controls prevent double generation in one circuit.
  - A failed products load shows only „Nie udało się wczytać produktów.”, with no controls and no retry (`:20-22`, `:132-150`).
- **Tests.** There are no page or component tests; `KitchenAssistant.Tests.csproj` has no bUnit reference.
  - Service tests already use the pattern the page tests need: real `ProductService` on in-memory SQLite, a `StubGenerator`, and `FakeTimeProvider` (`KitchenAssistant.Tests/Recipes/RecipeServiceTests.cs:20-51, 276-365`).
  - These helpers are private nested classes.
- **Unit coverage of odd model output is broad.** The uncovered cases:
  - network-error wrapping, which the tests assume but do not prove (`RecipeServiceTests.cs:279` hand-builds `AnthropicIOException`);
  - HTTP statuses other than 529;
  - the `pause_turn` and null stop reasons;
  - a null ingredient element;
  - more than 5 usable recipes at service level;
  - `\r`, `\r\n`, U+2028 and tab in names (`Recipes/RecipePrompt.cs:56`);
  - the 100/101-character name boundary (`Pantry/ProductService.cs:180`).
- **The service comment overstates the contract.** `Recipes/RecipeService.cs:7-8` claims every generation failure becomes `Failed`, which is true only for the three caught types plus the case where nothing is usable.

## Desired End State

`dotnet test KitchenAssistant.slnx` runs a bUnit suite that drives `/recipes` through every generation outcome and asserts the user-visible state, as defined by the oracle sources below:

| Outcome | Asserted state |
|---|---|
| Generating | „Generuję przepisy… to może potrwać do minuty”; main button, retry buttons and parameter fieldsets disabled; a second click does not call the generator again |
| Succeeded | Proposal cards, caption „Propozycje dla: …”; no alert |
| Failed (invalid JSON, nothing usable, `RecipeGenerationException`, `AnthropicException`, any other exception, deadline passed) | „Nie udało się wygenerować przepisów.” + „Spróbuj ponownie”; no cards, no warning |
| Retry after Failed | Cards replace the alert |
| NoneWithinLimits | „Żadna propozycja nie mieści się w limitach.” + hidden-count lines + „Spróbuj ponownie, zmień parametry albo dodaj produkty.” + retry + products link; no danger alert |
| No products (at load, or removed before the click) | „Najpierw dodaj produkty.” + link; main button disabled; generator not called |
| Products load fails | „Nie udało się wczytać produktów.” + „Spróbuj ponownie”; retry restores the controls |
| User leaves during generation | The generator's token is cancelled; nothing throws |

Oracle sources:
- `context/archive/2026-10-03-first-recipe-generation/plan.md:21,27,67,227,343`
- `context/archive/2026-10-08-recipe-ranking/plan.md:14,23,59`
- `context/archive/2026-10-09-meal-parameters/plan.md:148-151`
- `context/archive/2026-10-03-pantry-add-products/plan.md:198` (no exception reaches the circuit)
- test-plan §2 risk #1

The products-load retry is new; its oracle is the test-plan's "recoverable state" requirement plus the decision made in this plan.

Beyond the page suite:
- The unit gaps listed above are covered.
- The `RecipeService` comment matches the code.
- One Stryker.NET run on `RecipeService.cs` and `RecipeResponseParser.cs` has been triaged.
- test-plan §6.2 / §6.6 describe the page-test pattern.

### Key Discoveries:

- **The generator seam returns raw JSON** (`Recipes/IRecipeGenerator.cs:4-7`). So a scripted generator exercises the real parse → classify → rank path. The page test stubs only the generator, never `RecipeService`, so it is not a mirror test.
- **No new page dependencies needed.** The page injects only `RecipeService`, `ProductService` and `ILogger<RecipeSuggestions>`, and reads the user id from the cascaded `AuthenticationState` `NameIdentifier` claim (`Components/Pages/RecipeSuggestions.razor:12-14, 111-135`). Child components inject nothing.
- **Constructor dependencies:**
  - `RecipeService(ProductService, IRecipeGenerator, IOptions<RecipeOptions>, TimeProvider, ILogger<RecipeService>)` (`Recipes/RecipeService.cs:9-14`)
  - `ProductService(IDbContextFactory<ApplicationDbContext>, TimeProvider)` (`Pantry/ProductService.cs:38`)

  A failing `IDbContextFactory` is therefore the seam for "products load fails", without mocking `ProductService`.
- **bUnit v2 API** (Context7, 2026-10-11): `BunitContext`, `Render<T>()`, `AddAuthorization().SetAuthorized(..)` / `.SetClaims(..)`, `WaitForAssertion`, `WaitForState`. Latest stable on NuGet is 2.11.3. `[Authorize]` is enforced by the router, not the component, so access control stays in test-plan Phase 2.
- **Stryker.NET** latest stable on NuGet is `dotnet-stryker` 5.0.0. Only `dotnet-ef` is in `dotnet-tools.json` today.
- **`ProductService.Normalize`** throws `ArgumentException` when the trimmed name is empty or longer than `MaxNameLength` (`Pantry/ProductService.cs:179-183`).

## What We're NOT Doing

- **No upper bounds on `MaxTokens`, `MaxProducts` or `DeadlineSeconds`.** Lower bounds (already tested) are enough; the configuration is owner-controlled, and the spend limit and budget alert cover a runaway value. (Decision 2026-10-11.)
- **No widening of `RecipeService`'s catch list.** The guarantee for unexpected exceptions is asserted at page level; only the comment at `RecipeService.cs:7-8` is corrected. (Decision 2026-10-11.)
- **No pinning of `MaxRetries = 1` and the 60 s per-attempt timeout** (`Program.cs:65,68`). Test-plan Phase 2's Production-composition test owns these. (Decision 2026-10-11.)
- **No per-user or cross-tab throttle.** None exists, and test-plan §2 assigns total spend to observability.
- **No access-control (`[Authorize]`) or live SignalR-circuit tests.** These belong to Phase 2.
- **No e2e or Playwright tests, CI wiring or agent hooks.** These are outside Lesson 2 and outside this rollout phase.
- **No moving of the existing private helpers** in `RecipeServiceTests` / `ProductServiceTests` into the new shared support code. The existing tests stay as they are.
- **No tests for multiple or non-text content blocks** in the Anthropic response, and none for unknown extra JSON properties. These were not in the agreed gap list.
- **No new shared UI component** for the load-failure alert. It reuses the inline markup of the Failed alert on the same page.

## Implementation Approach

1. **Harness first.** Package, shared test support, and one smoke render. This proves bUnit works with net10.0 and xUnit 2.9.3 before any real assertion is written.
2. **Characterise existing behaviour against the archived oracle.** Production code changes only in one comment.
3. **The one production change, test-first.** The load retry.
4. **Unit gaps** in the existing test classes, following their local conventions.
5. **Mutation run.** Shows whether the service and parser assertions are strong. Then fill in the cookbook so the next phases reuse the pattern.

Every assertion uses literal Polish strings and states copied from the oracle sources, never values computed by app helpers. The one exception: `ScoreLabels.Hidden` / `HiddenOverTime` output may be compared to the literal expected sentence for the seeded counts.

## Critical Implementation Details

**Timing & lifecycle.** Page tests that need an in-flight generation must not depend on wall-clock time:
- Script the generator to await a `TaskCompletionSource` or `Task.Delay(Timeout.Infinite, ct)`.
- Assert the generating state.
- Then complete the source, or `FakeTimeProvider.Advance` past 60 s, and use `WaitForAssertion`.

The deadline CTS uses the injected `TimeProvider` (`Recipes/RecipeService.cs:44`), so the same `FakeTimeProvider` must be registered for both `RecipeService` and `ProductService`.

## Phase 1: bUnit harness

### Overview

Add bUnit and shared test support for interactive pages, and prove one render of `/recipes` for a signed-in user who has products.

### Changes Required:

#### 1. Test project packages

**File**: `KitchenAssistant.Tests/KitchenAssistant.Tests.csproj`

**Intent**: Reference bUnit so C#-authored component tests can render Razor components from the web project.

**Contract**:
- Add a `PackageReference` to `bunit`, latest stable 2.x (2.11.3 at planning time).
- The project SDK stays `Microsoft.NET.Sdk`; tests are written in C#, not `.razor`.
- If restore or build shows that version does not support net10.0 or xUnit 2.9.3, stop and report rather than upgrading xUnit.

#### 2. Shared page-test support

**File**: `KitchenAssistant.Tests/Support/` (new folder; e.g. `SqliteTestDatabase.cs`, `ScriptedRecipeGenerator.cs`, `RecipePageContext.cs`)

**Intent**: One reusable setup for page tests, so each test only seeds data, scripts the generator and asserts.

**Contract**:
- **SQLite database:**
  - An in-memory SQLite connection that stays open for the test.
  - `EnsureCreated`.
  - Seeds `ApplicationUser` rows.
  - Exposes an `IDbContextFactory<ApplicationDbContext>` that can be told to throw on its next N `CreateDbContext` calls. This is the failure seam for "products load fails".
- **Scripted generator:**
  - An `IRecipeGenerator` driven by a `Func<RecipeRequest, CancellationToken, Task<string>>`.
  - Records its requests and the last `CancellationToken`.
  - Exposes a `Called` signal, mirroring `RecipeServiceTests.StubGenerator` (`RecipeServiceTests.cs:347-359`).
- **Page context:**
  - A `BunitContext` subclass or fixture that registers the real `ProductService` and `RecipeService` (scoped), the scripted generator, `Options.Create(new RecipeOptions())` with defaults, one shared `FakeTimeProvider` as `TimeProvider`, and logging (`NullLogger` / `AddLogging`).
  - Calls `AddAuthorization()` with `SetAuthorized(..)` and `SetClaims(new Claim(ClaimTypes.NameIdentifier, userId))`.
  - Has a helper that seeds products through `ProductService.AddProductAsync`.
  - Has a helper that builds recipe JSON in the generator's schema shape, matching the shape the existing service tests use.

#### 3. Smoke test

**File**: `KitchenAssistant.Tests/Pages/RecipeSuggestionsPageTests.cs` (new)

**Intent**: Prove the harness end to end before writing outcome tests.

**Contract**: A user with one product renders the page:
- the heading „Przepisy”;
- the button „Zaproponuj przepisy” enabled;
- no alert;
- the generator not called.

### Success Criteria:

#### Automated Verification:

- Build succeeds with no new warnings: `dotnet build KitchenAssistant.slnx`
- The smoke test passes: `dotnet test --filter "FullyQualifiedName~RecipeSuggestionsPageTests"`
- The full suite passes: `dotnet test KitchenAssistant.slnx`

#### Manual Verification:

- Owner confirms the bUnit package version and the support-folder layout are acceptable as the project's component-test convention.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Page outcome → state tests

### Overview

Characterise today's `/recipes` behaviour for every generation outcome against the archived oracle. The only production edit is the misleading comment.

### Changes Required:

#### 1. Outcome tests

**File**: `KitchenAssistant.Tests/Pages/RecipeSuggestionsPageTests.cs`

**Intent**: Pin each row of the Desired End State table, except the products-load retry (Phase 3), as an observable page state.

**Contract**: Tests, each catching a distinct regression:
- **Generating state.** Generator blocked on a TCS. Assert:
  - the status text „Generuję przepisy… to może potrwać do minuty”;
  - the main button disabled;
  - the three parameter `fieldset`s disabled.

  Then trigger the click a second time and assert the generator was called once. If bUnit refuses to dispatch events to a disabled element, keep the disabled-attribute assertion and say in the test that the server-side guard at `:155-158` is not exercised.
  *Catches: a lost spinner, a lost re-entry guard, controls left enabled.*
- **Succeeded.** The generator returns 2 usable recipes. Assert:
  - 2 cards with the expected titles;
  - the caption „Propozycje dla: Obiad, do 30 minut, 1 porcja”;
  - no `alert`.

  *Catches: a status → state mapping regression.*
- **Failed, as one `[Theory]`** over the cases below. Each asserts the danger alert text „Nie udało się wygenerować przepisów.”, a „Spróbuj ponownie” button, no cards, and no warning alert. *Catches: a swallowed failure shown as a blank list, a narrowed catch-all, a 0-usable result reported as none-within-limits.*
  - invalid JSON;
  - `{"recipes":[]}`;
  - only always-at-home recipes;
  - `RecipeGenerationException`;
  - `AnthropicIOException`;
  - an arbitrary `InvalidOperationException`, which escapes the service and is caught by the page.
- **Deadline.** The generator never completes and `FakeTimeProvider` is advanced 61 s. Assert the Failed alert.
- **Retry after Failed.** The first call throws and the second returns recipes. Click „Spróbuj ponownie” and assert cards with no alert.
- **NoneWithinLimits.** The generator returns usable recipes that all have 3 missing ingredients. Assert:
  - the warning text „Żadna propozycja nie mieści się w limitach.”;
  - the hidden-count sentence for that count;
  - „Spróbuj ponownie, zmień parametry albo dodaj produkty.”;
  - a retry button and the link to `products`;
  - no danger alert.
- **No products at load.** Assert „Najpierw dodaj produkty.”, the link, the main button disabled, and the generator never called.
- **Products removed after load.** Render with one product, delete it through `ProductService`, then click. Assert the no-products text and the main button disabled.
- **User leaves during generation.** The generator blocks on its token. Dispose the rendered component. Assert the recorded token is cancelled and no exception escapes disposal or the pending handler.

#### 2. Service comment

**File**: `Recipes/RecipeService.cs`

**Intent**: Make the header comment match the code: the service turns the known generation failures into `Failed`, and the page handles anything else.

**Contract**:
- Comment only, at `:7-8`.
- Name the three caught types plus the case where nothing is usable, and state that other exceptions propagate to the caller.
- No behaviour change.

### Success Criteria:

#### Automated Verification:

- Page tests pass: `dotnet test --filter "FullyQualifiedName~RecipeSuggestionsPageTests"`
- The full suite passes: `dotnet test KitchenAssistant.slnx`

#### Manual Verification:

- Owner spot-checks that each page test's expected strings come from the cited archive plans, not from the page source.
- Owner temporarily breaks one branch (e.g. changes `default: failed = true` to `break`) and confirms at least one page test fails, then reverts.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Products-load retry (test-first)

### Overview

Make a failed products load recoverable without a page reload. First red test: "after a failed products load, clicking „Spróbuj ponownie” shows the generation controls".

### Changes Required:

#### 1. Load-failure tests

**File**: `KitchenAssistant.Tests/Pages/RecipeSuggestionsPageTests.cs`

**Intent**: Pin the new recovery path before writing it.

**Contract**:
- **Retry recovers.** The DB factory fails the first load and the user has products. Assert:
  - „Nie udało się wczytać produktów.” with a „Spróbuj ponownie” button;
  - no generation controls.

  Click retry, then assert the alert is gone and „Zaproponuj przepisy” is enabled.
- **Retry fails again.** The factory fails twice. After the click the alert stays and the retry button is enabled again.
- **Retry with no products.** The first load fails, the second succeeds with zero products. After the click the no-products state shows.

#### 2. Page change

**File**: `Components/Pages/RecipeSuggestions.razor`

**Intent**: Add a retry to the load-failure alert that re-runs the products load. Keep the existing initial-load behaviour.

**Contract**:
- Extract the products load from `OnInitializedAsync` into one method used by init and by retry.
- The method resets `loadFailed` before loading.
- It is guarded by a loading flag so a fast double click loads once, with the same reasoning as the `generating` guard at `:154-158`.
- It keeps the existing cancellation handling.
- The alert keeps the text „Nie udało się wczytać produktów.” and gains a „Spróbuj ponownie” button that is disabled while loading. Mirror the markup and role classes of the Failed alert at `:57-60` (`alert alert-danger d-flex align-items-center`, `btn btn-outline-danger btn-sm ms-3`).
- No literal colours.

### Success Criteria:

#### Automated Verification:

- The new tests fail before the page change and pass after it: `dotnet test --filter "FullyQualifiedName~RecipeSuggestionsPageTests"`
- The full suite passes: `dotnet test KitchenAssistant.slnx`
- No literal colours in the touched page: `grep -nE '#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|oklch\(|:\s*(white|black)\b|navbar-(dark|light)' Components/Pages/RecipeSuggestions.razor` prints nothing

#### Manual Verification:

- With `dotnet run --launch-profile https`, signed in with products:
  1. Stop LocalDB (`sqllocaldb stop MSSQLLocalDB`) and open `/recipes`. The load alert shows with „Spróbuj ponownie”.
  2. Start LocalDB again and click retry. The controls appear and generation with the fake generator works.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Unit gaps for risks #1 and #6

### Overview

Close the agreed unit-level gaps, where current tests assume behaviour instead of proving it, or where an edge has no case.

### Changes Required:

#### 1. Generator boundary

**File**: `KitchenAssistant.Tests/Recipes/AnthropicRecipeGeneratorTests.cs`

**Intent**: Prove the SDK behaviour the service mapping relies on.

**Contract**:
- **Network error.** An `HttpMessageHandler` whose `SendAsync` throws `HttpRequestException` makes `GenerateJsonAsync` throw an `AnthropicException`. Use `MaxRetries = 0`, as the existing tests do.
  - If the SDK does not wrap it, stop and report. Under the "page test only" decision the user-visible state is already covered by Phase 2. Whether the service should catch it is a new decision.
- **HTTP statuses `[Theory]`** over 400, 401, 429 and 500: each throws an `AnthropicException`. Extend the existing 529 case's pattern at `:121-128`.
- **Stop reasons.** Extend the existing theory at `:101-111` with `pause_turn`, and add a null `stop_reason`. Each throws `RecipeGenerationException`.
  - Oracle: only `end_turn` is a complete answer (archive first-recipe-generation plan.md:27, "truncated or refused → failure").

#### 2. Service and classifier

**File**: `KitchenAssistant.Tests/Recipes/RecipeServiceTests.cs`, `KitchenAssistant.Tests/Recipes/RecipeClassifierTests.cs`

**Intent**: Cover the PRD's "up to 5 proposals" at the service seam, and a structurally broken ingredient list.

**Contract**:
- **More than 5 usable.** The generator returns 7 usable, distinct recipes. The result is `Succeeded` with exactly 5 proposals (PRD *Business Logic*: "Jedno zapytanie zwraca do 5 propozycji"). The kept 5 must be the best by the PRD order, using a hand-built input where the expected 5 titles are obvious (no-missing recipes before missing ones).
- **Null ingredient element.** A recipe whose `ingredients` array contains `null` alongside valid ingredients is still classified. The null element is dropped and the other ingredients are kept. This is the extreme of "an ingredient with no name and no valid ID is dropped".

#### 3. Prompt sanitising

**File**: `KitchenAssistant.Tests/Recipes/RecipePromptTests.cs`

**Intent**: Prove no line-break variant or tab in a name or quantity can add a product line or a field.

**Contract**:
- One `[Theory]` over names and quantities containing `\r`, `\r\n`, U+2028 (line separator) and `\t`.
- Assert the user message has exactly one product line per product, and each line has the expected number of `|` separators.
- Extend the existing pattern at `:35-50`; don't add a new helper.

#### 4. Name length

**File**: `KitchenAssistant.Tests/ProductServiceTests.cs`

**Intent**: Pin the 100-character name boundary after trimming, which caps the size of each name in the prompt.

**Contract**:
- A 100-character name is accepted.
- A 101-character name throws `ArgumentException` and stores nothing.
- A 100-character name with surrounding spaces is accepted and stored trimmed.
- Oracle: `[StringLength(100)]` on `ProductForm.Name` and the archived pantry plan.

### Success Criteria:

#### Automated Verification:

- New tests pass: `dotnet test --filter "FullyQualifiedName~AnthropicRecipeGeneratorTests|FullyQualifiedName~RecipeServiceTests|FullyQualifiedName~RecipeClassifierTests|FullyQualifiedName~RecipePromptTests|FullyQualifiedName~ProductServiceTests"`
- The full suite passes: `dotnet test KitchenAssistant.slnx`

#### Manual Verification:

- Owner confirms the network-error test's outcome. If it revealed unwrapped exceptions, the owner decides whether a follow-up change is needed.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 5: Mutation gate and cookbook

### Overview

Run Stryker.NET once on the generation core. Kill the survivors that would hurt a user, and record the ones deliberately ignored. Then document the page-test pattern for later phases.

### Changes Required:

#### 1. Stryker local tool

**File**: `dotnet-tools.json`, `.gitignore`

**Intent**: Make the mutation run reproducible without a global install, and keep its output out of git.

**Contract**:
- Add `dotnet-stryker` (latest stable, 5.0.0 at planning time) as a local tool.
- Ignore `StrykerOutput/`.
- If the version cannot run against net10.0, stop and report.

#### 2. Mutation run and triage

**File**: `context/changes/testing-ai-failure-surfacing/mutation-report.md` (new), plus test files as needed

**Intent**: Use the mutation score as a selective gate on `Recipes/RecipeService.cs` and `Recipes/RecipeResponseParser.cs`, not as a coverage target.

**Contract**:
- Run from `KitchenAssistant.Tests/` with `dotnet stryker --mutate "**/Recipes/RecipeService.cs" --mutate "**/Recipes/RecipeResponseParser.cs"`.
- For each surviving mutant, record its location and ask: "would this hurt a user or the business?"
  - Yes → add an assertion that kills it.
  - No (equivalent, logging-only, cosmetic) → record why it is ignored.
- The report lists the score before and after, and every survivor with its verdict.
- Log-message mutants are expected survivors, since logging uses `NullLogger`.

#### 3. Cookbook update

**File**: `context/foundation/test-plan.md`

**Intent**: Let later phases add page tests without re-deriving the pattern.

**Contract**:
- **§6.2:** replace "TBD" with the "AI outcome → visible recoverable state" pattern:
  - real `RecipeService` and `ProductService` on SQLite;
  - the scripted generator and failing DB factory from `KitchenAssistant.Tests/Support/`;
  - `FakeTimeProvider` for deadlines;
  - auth claims via `AddAuthorization`;
  - literal oracle strings;
  - in-flight states via TCS rather than delays.
- **§6.6:** append a Phase 1 note covering:
  - what shipped;
  - the Stryker command and when to run it (ad hoc, after a risk phase);
  - the Phase-2-owned items (retries and timeout).
- **§4:** set the bUnit row's version.
- Do not touch §1–§3 or §5 strategy text.

### Success Criteria:

#### Automated Verification:

- The tool restores: `dotnet tool restore`
- The full suite passes after any added assertions: `dotnet test KitchenAssistant.slnx`
- The mutation run completes and the report exists: `context/changes/testing-ai-failure-surfacing/mutation-report.md`

#### Manual Verification:

- Owner reviews the survivor verdicts in `mutation-report.md` and agrees with each "ignore".
- Owner reads test-plan §6.2 and confirms it is enough to write a new page test without this plan.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- **Generator:** network-error wrapping, HTTP statuses 400/401/429/500, the `pause_turn` and null stop reasons.
- **Service:** more than 5 usable recipes gives exactly 5, in PRD order.
- **Classifier:** a null ingredient element.
- **Prompt:** `\r`, `\r\n`, U+2028 and tab cannot add lines or fields.
- **Product service:** the 100/101-character trimmed-name boundary.

### Component Tests (bUnit):

- Every row of the Desired End State table on `/recipes`.
- Real services, with the generator scripted at the `IRecipeGenerator` seam and the database failing at the `IDbContextFactory` seam.
- Failure kinds are one parameterised theory, not separate copies.

### Manual Testing Steps:

1. Phase 2: break the `default:` branch of the status switch, confirm a page test fails, then revert.
2. Phase 3: stop LocalDB, open `/recipes`, start LocalDB, click „Spróbuj ponownie”, and confirm the controls appear and the fake generation works.
3. Phase 5: review the mutation survivors' verdicts.

## Performance Considerations

None at runtime. The bUnit tests run in-process on SQLite. Deadline tests use `FakeTimeProvider`, so the suite never waits 60 s.

## Migration Notes

Not applicable: no schema or data changes.

## References

- Research: `context/changes/testing-ai-failure-surfacing/research.md`
- Test plan: `context/foundation/test-plan.md` §2 risks #1 and #6, §3 Phase 1, §6.2
- Oracle sources:
  - `context/archive/2026-10-03-first-recipe-generation/plan.md:21,27,67,227,343`
  - `context/archive/2026-10-08-recipe-ranking/plan.md:14,23,59`
  - `context/archive/2026-10-09-meal-parameters/plan.md:148-151`
  - `context/archive/2026-10-03-pantry-add-products/plan.md:198`
- Existing patterns:
  - `KitchenAssistant.Tests/Recipes/RecipeServiceTests.cs:276-365` (StubGenerator, deadline, cancellation)
  - `KitchenAssistant.Tests/Recipes/AnthropicRecipeGeneratorTests.cs:27-40, 101-128, 165-174` (client, MessageJson, StubHandler)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: bUnit harness

#### Automated

- [x] 1.1 Build succeeds with no new warnings — 33eca9d
- [x] 1.2 The smoke test passes — 33eca9d
- [x] 1.3 The full suite passes — 33eca9d

#### Manual

- [x] 1.4 Owner confirms the bUnit package version and the support-folder layout — 33eca9d

### Phase 2: Page outcome → state tests

#### Automated

- [x] 2.1 Page tests pass — d28b45f
- [x] 2.2 The full suite passes — d28b45f

#### Manual

- [x] 2.3 Owner spot-checks expected strings come from the cited archive plans — d28b45f
- [x] 2.4 Owner breaks one branch, confirms a page test fails, then reverts — d28b45f

### Phase 3: Products-load retry (test-first)

#### Automated

- [x] 3.1 The new tests fail before the page change and pass after it — acedf7b
- [x] 3.2 The full suite passes — acedf7b
- [x] 3.3 No literal colours in the touched page — acedf7b

#### Manual

- [x] 3.4 With LocalDB stopped then restarted, retry restores the controls and generation works — acedf7b

### Phase 4: Unit gaps for risks #1 and #6

#### Automated

- [x] 4.1 New tests pass — 66f5d45
- [x] 4.2 The full suite passes — 66f5d45

#### Manual

- [x] 4.3 Owner confirms the network-error test's outcome — 66f5d45

### Phase 5: Mutation gate and cookbook

#### Automated

- [x] 5.1 The tool restores — 3bca814
- [x] 5.2 The full suite passes after any added assertions — 3bca814
- [x] 5.3 The mutation run completes and the report exists — 3bca814

#### Manual

- [x] 5.4 Owner reviews the survivor verdicts in mutation-report.md — 3bca814
- [x] 5.5 Owner confirms test-plan §6.2 is enough to write a new page test — 3bca814
