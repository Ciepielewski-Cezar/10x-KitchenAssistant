# First recipe generation (S-02) Implementation Plan

## Overview

A signed-in user with saved products opens „Przepisy” (`/recipes`), asks for recipes, and within a minute gets up to 5 AI-generated proposals built from their products, each with ingredients and preparation steps. Every ingredient is classified **in C#** as owned (linked to the user's product by ID), „zawsze w domu”, or missing. This is roadmap slice S-02 (US-01, FR-005, NFR ≤ 1 min, NFR struktura). Score, sort order and the ≤ 2-missing filter are S-03; meal-parameter UI is S-05; persistence and a details view are S-04.

Following the research decision (`research.md` § Decision 2026-10-04), development runs against a **fake generator** at zero API cost. Real Anthropic calls happen only in the last phase: a measured latency/quality spike and a smoke test.

## Current State Analysis

- **AI client registered, nothing uses it.** `Program.cs:57-64` registers `AnthropicClient` (key from `ANTHROPIC_API_KEY`, `Timeout = 60 s`); `Program.cs:68-71` logs a warning when the key is missing. No recipe service, prompt, schema, UI or tests exist.
- **SDK defaults break the 1-minute guardrail.** Anthropic C# SDK 12.50.0 defaults to `MaxRetries = 2` and retries timeouts, 408, 409, 429 and 5xx with short exponential backoff (checked against the package XML docs and an offline test). With the 60 s per-attempt timeout, one failing click can take ~3 minutes.
- **Missing key fails late.** `AnthropicClient` does not throw on a null/empty key; the first call gets a 401 (`AnthropicUnauthorizedException`).
- **Input data is ready.** `Pantry/ProductService.cs:40-57` `GetProductsAsync(userId)` returns `ProductList(UseFirst, Stored)` of `ProductListItem(int Id, Name, Category, …)`, filtered by user. `Product.Id` was kept a compact `int` for exactly this purpose (`context/archive/2026-10-03-pantry-add-products/plan.md:39`).
- **Page pattern exists.** `Components/Pages/Products.razor` shows the convention: `@rendermode InteractiveServer`, `[Authorize]`, `userId` from `ClaimTypes.NameIdentifier`, service calls wrapped in try/catch with a Polish error and `ILogger`.
- **Tests:** `KitchenAssistant.Tests` (xUnit, SQLite in-memory, `FakeTimeProvider`) — no mocking library.
- **Dev key not set** (`dotnet user-secrets list` has no `ANTHROPIC_API_KEY`; `context/changes/local-dev/local-dev-plan.md:71` deferred). That is expected under the zero-cost path.

## Desired End State

A signed-in user picks „Przepisy” in the nav menu. The page shows a „Zaproponuj przepisy” button. While generating, the button is disabled and the page says „Generuję przepisy… to może potrwać do minuty”. Within 60 s the user sees up to 5 proposal cards, each with a title, a short description, an approximate prep time, an ingredient list and numbered steps, all in Polish. Each ingredient carries one badge:

- **Masz** — the AI returned a product ID that exists on *this user's* list, or returned no ID but a name that exactly matches one of this user's products; the user's own product name is shown.
- **Zawsze w domu** — no valid ID, and the name matches the fixed always-at-home list: sól, olej, woda and 22 common dried spices (Phase 1 §5; PRD amended 2026-10-04).
- **Brakuje** — everything else, shown by the AI's name, including any ID not on this user's list.

A user with no products sees the button disabled and a link to `/products`. Any failure (timeout, overload, refusal, truncated or malformed output, missing key) shows „Nie udało się wygenerować przepisów.” with a „Spróbuj ponownie” button, never later than ~60 s after the click; the cause is logged. Results live only in the page's state.

In Development the app uses the fake generator (`Recipes:Generator = Fake`); every other environment uses Anthropic, and the app refuses to start if the fake is configured outside Development. After Phase 3, `spike.md` records measured latency, tokens and ID validity for the chosen model/effort, and those settings are the committed defaults.

Verify with `dotnet test KitchenAssistant.slnx`, the Phase 2 browser walkthrough (two accounts, fake generator), and the Phase 3 spike log.

### Key Discoveries:

- SDK structured output: `MessageCreateParams.OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = … }, Effort = Effort.Low }`; `Schema` is `IReadOnlyDictionary<string, JsonElement>` (required). `Effort` has `Low/Medium/High/Xhigh/Max`.
- The SDK's `Model` enum has no Sonnet 5.5 member; pass the string `"claude-sonnet-5-5"` (implicit conversion).
- `Message.StopReason` (`ApiEnum<string, StopReason>`): `EndTurn`, `MaxTokens`, `Refusal`, …; `Message.Usage` exposes `InputTokens`, `OutputTokens`, `OutputTokensDetails.ThinkingTokens`.
- `IMessageService.Create(params, CancellationToken)` honours cancellation; a client timeout surfaces as `TaskCanceledException` (inner `TimeoutException`), **not** an Anthropic exception.
- Structured output does not support numeric `minimum`/`maximum`, string length limits or complex array constraints, and every object needs `additionalProperties: false`. The C# SDK does not strip unsupported constraints, so limits (≤ 5 proposals) are enforced in code.
- Sonnet 5.5 / Opus 5.5 reject `ThinkingConfigDisabled` and non-default temperature; omit `Thinking` and `Temperature`.
- `CLAUDE.md`: a `Recipes/` folder must not sit next to a `Recipes.razor` page class — the page is `RecipeSuggestions.razor`.

## What We're NOT Doing

- Score „Masz X z Y”, missing count display, sort order, and **filtering proposals with more than 2 missing ingredients** — S-03. In S-02 a proposal with 3+ missing ingredients is still shown, with each missing ingredient marked.
- Meal-parameter controls (S-05). S-02 passes the PRD defaults (obiad, do 30 minut, 1 porcja) into the prompt.
- Recipe details view, persisting proposals across refresh or re-login (S-04).
- Splitting generation into „list, then full recipe on open” — only if the Phase 3 spike fails, and then via re-planning / S-04.
- Streaming partial results to the UI.
- Multi-provider support or an `IChatClient` abstraction; no free-tier providers.
- Any database change (no migration).
- Fuzzy or prefix matching for „zawsze w domu”: „olej rzepakowy”, „pieprz czarny mielony”, „papryka” (the vegetable), „świeża bazylia” and „czosnek” are **missing** unless the AI uses an exact list name. Spices not on the list (e.g. „za'atar”) are normal ingredients until added to the list.
- Letting the AI flag spices (an `isSpice` field): that would let the LLM decide what counts as missing, against the `CLAUDE.md` hard rule.
- Dropping ingredients or whole proposals because of an unknown product ID.
- A real-API test in the automated suite.

## Implementation Approach

1. **Core first, no API.** A `Recipes/` feature folder holds pure, testable pieces: the JSON schema, the prompt builder, the parser (JSON → DTOs), the classifier (DTOs + the user's `ProductList` → proposals) and `RecipeService`, which owns the user → products → generator → parse → classify flow and the 60 s deadline. The generator is an interface returning **raw JSON text**, so the fake and the real generator share one parse/classify path.
2. **Then the real generator and the page.** `AnthropicRecipeGenerator` makes one structured-output call; DI picks Fake or Anthropic from config; `/recipes` is a thin interactive page over `RecipeService`.
3. **Finally, spend money once.** With a dev key and a low spend limit, measure ≥ 10 real calls, lock the defaults, and smoke-test the production-like stack.

## Critical Implementation Details

### Timing & lifecycle

One 60 s deadline covers the whole click: `RecipeService` links the caller's token with a `CancellationTokenSource(TimeSpan, TimeProvider)` so tests can drive it with `FakeTimeProvider`. The client gets `MaxRetries = 1`, so a fast 429/529 is retried, but the deadline cancels anything still running at 60 s. Tell the two cancellations apart: deadline elapsed → `Failed` (logged as timeout); caller token cancelled (user left the page) → propagate `OperationCanceledException` and render nothing. A per-attempt SDK timeout arrives as `TaskCanceledException` while the caller's token is *not* cancelled — treat it as a failure, not as navigation.

### Schema constraints

The schema lives as one JSON document in code. Keep it to `type`, `properties`, `required`, `items`, `description` and `additionalProperties: false` on every object. The nullable product ID is `"type": ["integer", "null"]`; if the API rejects that form during Phase 3, switch to `anyOf` — this is the only schema detail that cannot be verified offline. The first call after a schema change is slower (one-time compilation, cached 24 h); exclude it from the spike's latency numbers but note it.

### Debug & observability

`AnthropicRecipeGenerator` logs one Information line per call: model, effort, elapsed ms, stop reason, input/output/thinking tokens. Phase 3 reads its numbers from these lines, and in production the deployment plan's request-failure alert catches errors.

## Phase 1: Recipe core with the fake generator

### Overview

Everything needed to turn a user's products into classified proposals, proven by unit tests with no API key and no UI.

### Changes Required:

#### 1. Request and result types

**File**: `Recipes/RecipeModels.cs`

**Intent**: Define the inputs and outputs the rest of the feature shares, so S-03/S-04/S-05 extend them instead of re-shaping them.

**Contract**:
- `MealType { Breakfast, Dinner, Supper, Snack }` (śniadanie, obiad, kolacja, przekąska) and `MealParameters(MealType MealType, int? MaxPrepMinutes, int Servings)` with `static Default = (Dinner, 30, 1)`.
- `RecipeRequest(IReadOnlyList<ProductListItem> Products, MealParameters Meal)`.
- `IngredientStatus { Owned, AlwaysAtHome, Missing }`.
- `ProposalIngredient(string Name, string? Amount, IngredientStatus Status, int? ProductId, ProductCategory? Category)` — `ProductId`/`Category` set only when `Owned`; `Name` is the user's product name when `Owned`, the AI's name otherwise.
- `RecipeProposal(string Title, string? Summary, int? PrepTimeMinutes, IReadOnlyList<ProposalIngredient> Ingredients, IReadOnlyList<string> Steps)`.
- `RecipeGenerationStatus { Succeeded, NoProducts, Failed }` and `RecipeGenerationResult(RecipeGenerationStatus Status, IReadOnlyList<RecipeProposal> Proposals)`.
- `MealLabels` (Polish labels for `MealType`), following `Pantry/ProductLabels.cs`.

#### 2. Options

**File**: `Recipes/RecipeOptions.cs`

**Intent**: Make the generator choice and the model settings configurable so Phase 3 changes them without code.

**Contract**: Bound from the `Recipes` config section: `Generator` (`Fake` | `Anthropic`, default `Anthropic`), `Model` (default `"claude-sonnet-5-5"`), `Effort` (default `"low"`), `MaxTokens` (default 16000; thinking tokens count against it), `DeadlineSeconds` (default 60).

#### 3. Generator interface and errors

**File**: `Recipes/IRecipeGenerator.cs`

**Intent**: The seam between the app and the AI provider; both implementations return the raw JSON the schema describes.

**Contract**: `Task<string> GenerateJsonAsync(RecipeRequest request, CancellationToken ct)`. `RecipeGenerationException(string reason)` for provider-level failures (missing key, refusal, `max_tokens`, empty content).

#### 4. Schema, prompt, parser

**Files**: `Recipes/RecipeSchema.cs`, `Recipes/RecipePrompt.cs`, `Recipes/RecipeResponseParser.cs`

**Intent**: One structured-output schema, one prompt, one way to read the answer.

**Contract**:
- `RecipeSchema.Json` (string) and `RecipeSchema.AsDictionary()` → `IReadOnlyDictionary<string, JsonElement>`. Shape: `{ recipes: [ { title, summary, prepTimeMinutes, ingredients: [ { productId: integer|null, name, amount } ], steps: [string] } ] }`, all objects `additionalProperties: false`, all fields `required` (nullable where optional). No numeric/length/array-count constraints.
- `RecipePrompt.System` (English) states: write all recipe text in Polish; propose up to 5 recipes for the given meal parameters; use only the listed products plus at most 2 additional ingredients per recipe; for an ingredient taken from the list, return its `productId`, otherwise `productId: null`; the always-at-home items are available and must be named exactly as listed (the list is rendered from `RecipeClassifier.AlwaysAtHomeItems`, so prompt and classifier never diverge); prefer products in „zużyj w pierwszej kolejności”; steps are short and logical.
- `RecipePrompt.BuildUserMessage(RecipeRequest)` lists every product as `id | name | category label | quantity?` plus the meal parameters with Polish labels. Product names are data, so they go in the user message, not the system prompt.
- `RecipeResponseParser.Parse(string json)` → `IReadOnlyList<AiRecipe>` (internal DTOs mirroring the schema). Malformed JSON or a missing `recipes` array throws `RecipeGenerationException`.

#### 5. Classifier

**File**: `Recipes/RecipeClassifier.cs`

**Intent**: Apply the PRD ownership rule in code, never trusting the AI's claim.

**Contract**: `static IReadOnlyList<RecipeProposal> Classify(IReadOnlyList<AiRecipe> recipes, ProductList products)`:
- An ingredient is `Owned` if its `productId` exists in this user's `products` (either section); name and category then come from the product.
- Else, if `productId` is null and its trimmed name equals one of this user's product names exactly (pl-PL, ignoring case; `UseFirst` checked before `Stored`), it is also `Owned` from that product. This is an exact match, not fuzzy matching, and it is decided in C#. A non-null unknown ID never falls back to a name match.
- Else `AlwaysAtHome` if its trimmed name equals an entry of `AlwaysAtHomeItems`, ignoring case (pl-PL comparer). The list (decided 2026-10-04): `sól`, `olej`, `woda` + 22 dried spices: `pieprz`, `papryka słodka`, `papryka ostra`, `papryka wędzona`, `chili`, `oregano`, `bazylia suszona`, `tymianek`, `majeranek`, `rozmaryn`, `cynamon`, `kminek`, `kmin rzymski`, `curry`, `kurkuma`, `imbir mielony`, `liść laurowy`, `ziele angielskie`, `gałka muszkatołowa`, `goździki`, `czosnek granulowany`, `zioła prowansalskie`.
- Else `Missing`, by the AI's name; a non-null unknown ID is discarded (the generator path logs it).
- A recipe with no title, no ingredients or no steps is dropped; the result is capped at the first 5 recipes in AI order.
- `AlwaysAtHomeItems` is a public constant list so S-03 and the prompt reuse it.

#### 6. Fake generator

**File**: `Recipes/FakeRecipeGenerator.cs`

**Intent**: Let the whole UI run at zero cost and show every classification state.

**Contract**: Deterministic JSON built from `request.Products`: up to 3 recipes — one using only owned products (+ `sól`), one with 1 missing ingredient, one containing an ingredient with an ID not in the list (renders as Brakuje). About 1.5 s delay (cancellable) so the spinner is visible. Output must pass `RecipeResponseParser`.

#### 7. Service

**File**: `Recipes/RecipeService.cs`

**Intent**: The only entry point pages use; owns privacy, the deadline and error mapping.

**Contract**: `RecipeService(ProductService, IRecipeGenerator, IOptions<RecipeOptions>, TimeProvider, ILogger<RecipeService>)`; `Task<RecipeGenerationResult> GenerateAsync(string userId, CancellationToken ct)`:
- loads products via `ProductService.GetProductsAsync(userId)`; zero products → `NoProducts` without calling the generator;
- calls the generator with `MealParameters.Default` under the linked deadline (see Critical Implementation Details);
- parse + classify; zero proposals after classification → `Failed`;
- `RecipeGenerationException`, `AnthropicException` (the SDK base type: it covers `AnthropicApiException` subtypes and `AnthropicIOException` for network/TLS failures), or `OperationCanceledException` while the caller's token is *not* cancelled (SDK per-attempt timeout or the deadline) → `Failed` + logged; caller cancellation propagates.

#### 8. Tests

**File**: `KitchenAssistant.Tests/Recipes/*.cs`

**Intent**: Prove the rules without any API call.

**Contract**: Hand-written fake `IRecipeGenerator` implementations inside the test project (no mocking library). Cases are listed under Testing Strategy.

### Success Criteria:

#### Automated Verification:

- Build succeeds with no new warnings: `dotnet build`
- All tests pass, including the new Recipes tests: `dotnet test KitchenAssistant.slnx`
- No model changes: `dotnet ef migrations has-pending-model-changes` exits 0

#### Manual Verification:

- The system prompt, a sample user message and the schema read sensibly to you (optionally paste them into Claude under your Max subscription to sanity-check the Polish output)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Anthropic generator and the /recipes page

### Overview

The real structured-output call, config-driven wiring, and the user-facing page — exercised end to end on the fake generator.

### Changes Required:

#### 1. Anthropic generator

**File**: `Recipes/AnthropicRecipeGenerator.cs`

**Intent**: One structured-output call per request, with explicit checks for the failure modes the schema cannot prevent.

**Contract**: Implements `IRecipeGenerator` using the singleton `AnthropicClient` and `RecipeOptions`:
- throws `RecipeGenerationException("missing key")` before calling when `ANTHROPIC_API_KEY` is empty;
- `MessageCreateParams { Model = options.Model, MaxTokens, System = RecipePrompt.System, Messages = [user message], OutputConfig = { Format = new JsonOutputFormat { Schema = RecipeSchema.AsDictionary() }, Effort = parsed options.Effort } }` — no `Thinking`, no `Temperature`;
- `StopReason` other than `EndTurn` (notably `Refusal`, `MaxTokens`) → `RecipeGenerationException`; otherwise concatenate the `TextBlock` texts;
- one Information log line per call (see Debug & observability).
- `internal static BuildParams(...)` so tests can check the request without the network.

#### 2. DI and configuration

**Files**: `Recipes/RecipeServiceCollectionExtensions.cs`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`

**Intent**: Pick the generator from config and make the fake impossible in production.

**Contract**:
- `AddRecipes(this IServiceCollection, IConfiguration, IHostEnvironment)` binds `RecipeOptions`, registers `RecipeService` (scoped) and the chosen `IRecipeGenerator`; throws `InvalidOperationException` at startup when `Generator = Fake` and the environment is not Development.
- `Program.cs`: call `AddRecipes`; add `MaxRetries = 1` to the existing `AnthropicClient` registration (keep `Timeout = 60 s`) and update its comment.
- `appsettings.json`: `Recipes` section with the Anthropic defaults. `appsettings.Development.json`: `"Recipes": { "Generator": "Fake" }`.

#### 3. Page and navigation

**Files**: `Components/Pages/RecipeSuggestions.razor`, `Components/Layout/NavMenu.razor`, `Components/Pages/Home.razor`

**Intent**: The user-facing flow from the Desired End State, following the `Products.razor` conventions.

**Contract**:
- `@page "/recipes"`, `@rendermode InteractiveServer`, `[Authorize]`, `PageTitle` „Przepisy”; injects `RecipeService`, `ProductService`, `ILogger`.
- On init, loads the product list to decide whether the button is enabled; when empty, shows „Najpierw dodaj produkty” with a link to `/products`.
- Button „Zaproponuj przepisy” → `RecipeService.GenerateAsync`; guard against double clicks like the `if (saving)` guard in `Products.razor` `AddAsync`; disabled + spinner text while running; `Failed` → alert „Nie udało się wygenerować przepisów.” + „Spróbuj ponownie”.
- Error handling around the call: catch `OperationCanceledException` after `Dispose` and render nothing; wrap everything else in a general `catch (Exception)` that logs and shows the same Polish error, as `Products.razor` does.
- Cards: title, summary, „ok. N min” when present, ingredient list with badges Masz / Zawsze w domu / Brakuje (+ amount), `<ol>` of steps.
- Implements `IDisposable`: cancels its `CancellationTokenSource` when the user navigates away.
- NavMenu: „Przepisy” link inside `<Authorized>` after „Moje produkty”. Home: a link next to the existing products link.

#### 4. Tests

**File**: `KitchenAssistant.Tests/Recipes/*.cs`

**Intent**: Cover the Anthropic request shape, stop-reason handling and the production guard without network access.

**Contract**: `BuildParams` assertions; an offline test of response handling using a stubbed HTTP handler pointed to by the client (the SDK check confirmed this works for `end_turn`, `refusal` and error statuses); if injecting a handler proves awkward, test the text/stop-reason extraction on a constructed `Message` instead. `AddRecipes` throws for `Fake` in Production and succeeds in Development.

### Success Criteria:

#### Automated Verification:

- Build succeeds with no new warnings: `dotnet build`
- All tests pass, including the Anthropic generator and DI-guard tests: `dotnet test KitchenAssistant.slnx`
- No model changes: `dotnet ef migrations has-pending-model-changes` exits 0

#### Manual Verification:

- On the fake generator, a signed-in user with products opens „Przepisy” from the nav, clicks the button, sees the spinner text, then proposal cards with Masz / Zawsze w domu / Brakuje badges and numbered steps; the unknown-ID ingredient shows as Brakuje
- A user with no products sees the disabled button and the link to `/products`
- A second account sees only its own product names in proposals
- An anonymous visit to `/recipes` redirects to login
- With `Recipes:Generator = Anthropic` and no key, clicking shows the Polish error and „Spróbuj ponownie” within seconds, and the log names the cause

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Live gate — spike and smoke test

### Overview

The only phase that spends money (~10–20 calls, well under $2 at Sonnet 5.5 estimates). It answers the roadmap's open question — do 5 full recipes in one call fit in a minute? — and locks the defaults.

### Changes Required:

#### 1. Dev key and switch (user)

**Intent**: Enable real calls without exposing the key.

**Contract**: You run `dotnet user-secrets set ANTHROPIC_API_KEY <dev-key>` yourself (never in chat), with a low monthly spend limit on the dev workspace in the Console; set `Recipes:Generator = Anthropic` via user-secrets or a local override (do not commit the Development switch). Off VPN if TLS inspection breaks calls (`local-dev-plan.md:99`).

#### 2. Spike log

**File**: `context/changes/first-recipe-generation/spike.md`

**Intent**: Record evidence that decides the defaults and S-04's shape.

**Contract**: For a realistic pantry of ~20–30 Polish products: ≥ 10 calls at Sonnet 5.5 / low (excluding the first, schema-compiling call), each row with elapsed s, stop reason, input/output/thinking tokens, recipe count, owned-by-ID / owned-by-name-fallback / unknown-ID counts, and a quality note (Polish, logical steps). Summary: median and max latency, estimated cost per request, verdict.

#### 3. Decide and lock

**Files**: `appsettings.json`, `context/foundation/roadmap.md` (S-02 Unknowns)

**Intent**: Commit the measured choice.

**Contract**: If every measured call finishes within 60 s, keep (or adjust) `Recipes:Model`/`Effort` defaults and record the answer in the roadmap's S-02 Unknowns. If not, apply levers in order, re-measuring ≥ 5 calls each: Haiku 4.5 → terser recipes (prompt) → stop and re-plan the list/details split with S-04. If the API rejects the nullable `productId` schema, switch to `anyOf` and re-run.

#### 4. Smoke test on the production-like stack

**Intent**: Prove the Production wiring (Anthropic generator, no fake) works in the container.

**Contract**: Put the dev key in `.env`, run `./scripts/local-prod.ps1`, register, add products, generate once at http://localhost:8090/recipes.

### Success Criteria:

#### Automated Verification:

- All tests still pass after the default changes: `dotnet test KitchenAssistant.slnx`

#### Manual Verification:

- Dev key set via user-secrets with a Console spend limit; the startup missing-key warning is gone
- At least 10 real calls on a realistic pantry each finish within 60 s, with latency, tokens and stop reason recorded in `spike.md`
- Recipes are in Polish with logical steps, and the ID validity rate is recorded in `spike.md`
- The chosen model/effort is committed as the `Recipes` defaults in `appsettings.json` and the roadmap S-02 unknown is answered (or the split is escalated to re-planning)
- On the production-like stack (`local-prod.ps1`), one generation returns proposals

**Implementation Note**: This phase is manual-heavy and needs the user's key; the agent prepares `spike.md` and reads the log lines, the user runs the calls.

---

## Testing Strategy

### Unit Tests:

- **Parser:** valid JSON → DTOs; malformed JSON, missing `recipes`, wrong types → `RecipeGenerationException`; nullable `productId` round-trips.
- **Classifier:** valid ID → Owned with the DB name and category (AI's differing name is ignored); unknown ID → Missing by AI name; another user's product ID (not in this user's list) → Missing; `null` ID + `Sól ` (case/whitespace) → AlwaysAtHome; `Papryka słodka` → AlwaysAtHome; `papryka`, `świeża bazylia`, `czosnek`, `pieprz czarny mielony`, `olej rzepakowy` → Missing; valid ID whose name is `sól` → Owned (owned wins); `null` ID + ` Jajka` when the user has `jajka` → Owned by name fallback; unknown non-null ID + an owned product's name → Missing (no fallback); recipe without steps/ingredients/title dropped; 7 recipes → first 5 kept; a recipe with 3 missing ingredients is kept.
- **Schema:** parses as JSON; every object has `additionalProperties: false`; no `minimum`/`maximum`/`minItems`/`maxItems`/`minLength`/`maxLength` anywhere.
- **Prompt:** user message contains every product's ID and name and the Polish meal labels; system prompt contains the Polish-output rule and every `AlwaysAtHomeItems` entry.
- **Fake generator:** output parses; includes one ID not in the request.
- **Service:** no products → `NoProducts` and the generator is never called; only the caller's products reach the generator (two users in SQLite); generator throws → `Failed`; generator waits past the deadline (`FakeTimeProvider.Advance(61 s)`) → `Failed`; caller-cancelled token → `OperationCanceledException`; generator returns only invalid recipes → `Failed`.
- **Anthropic generator:** `BuildParams` sets model, effort, schema, max tokens, and no thinking/temperature; `refusal`/`max_tokens` → `RecipeGenerationException`; empty key → exception before any HTTP call.
- **DI guard:** `Fake` in Production throws; `Fake` in Development registers `FakeRecipeGenerator`.

### Integration Tests:

- None automated against the real API (by decision); Phase 3 is the live check.

### Manual Testing Steps:

1. Phase 2 walkthrough on the fake with two accounts, an empty account and an anonymous browser.
2. Missing-key error path with `Generator = Anthropic`.
3. Phase 3 spike and the container smoke test.

## Performance Considerations

The PRD guardrail (full recipe ≤ 1 min) is enforced by the 60 s deadline, not hoped for: a slow call becomes a visible error, never a 3-minute spinner. Output length dominates latency; low effort and terse steps are the first levers. Cost estimate (research, unmeasured): ~$0.03/request on Sonnet 5.5; Phase 3 replaces it with `Usage` data. There is no per-user rate limit in S-02; any signed-in account can click repeatedly. Before production goes live, set a monthly spend limit on the production workspace in the Anthropic Console, the same as for dev. A per-user rate limit is deferred.

## Migration Notes

No database change. Configuration adds a `Recipes` section; production needs only `ANTHROPIC_API_KEY` (already planned in `deployment-plan.md:55-57`).

## References

- Research and decisions: `context/changes/first-recipe-generation/research.md`
- PRD: `context/foundation/prd.md` (FR-005, Business Logic, NFR; „zawsze w domu” amended 2026-10-04 to include spices)
- Roadmap slice: `context/foundation/roadmap.md` (S-02; S-03/S-04/S-05 boundaries)
- Product input: `Pantry/ProductService.cs:40-57`
- Page pattern: `Components/Pages/Products.razor`
- Client registration: `Program.cs:57-71`
- Prior plan: `context/archive/2026-10-03-pantry-add-products/plan.md`
- Local dev key step and TLS gotcha: `context/changes/local-dev/local-dev-plan.md:71,99`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Recipe core with the fake generator

#### Automated

- [x] 1.1 Build succeeds with no new warnings: `dotnet build`
- [x] 1.2 All tests pass, including the new Recipes tests: `dotnet test KitchenAssistant.slnx`
- [x] 1.3 No model changes: `dotnet ef migrations has-pending-model-changes` exits 0

#### Manual

- [x] 1.4 The system prompt, a sample user message and the schema read sensibly to you (optionally paste them into Claude under your Max subscription to sanity-check the Polish output)

### Phase 2: Anthropic generator and the /recipes page

#### Automated

- [ ] 2.1 Build succeeds with no new warnings: `dotnet build`
- [ ] 2.2 All tests pass, including the Anthropic generator and DI-guard tests: `dotnet test KitchenAssistant.slnx`
- [ ] 2.3 No model changes: `dotnet ef migrations has-pending-model-changes` exits 0

#### Manual

- [ ] 2.4 On the fake generator, a signed-in user with products opens „Przepisy” from the nav, clicks the button, sees the spinner text, then proposal cards with Masz / Zawsze w domu / Brakuje badges and numbered steps; the unknown-ID ingredient shows as Brakuje
- [ ] 2.5 A user with no products sees the disabled button and the link to `/products`
- [ ] 2.6 A second account sees only its own product names in proposals
- [ ] 2.7 An anonymous visit to `/recipes` redirects to login
- [ ] 2.8 With `Recipes:Generator = Anthropic` and no key, clicking shows the Polish error and „Spróbuj ponownie” within seconds, and the log names the cause

### Phase 3: Live gate — spike and smoke test

#### Automated

- [ ] 3.1 All tests still pass after the default changes: `dotnet test KitchenAssistant.slnx`

#### Manual

- [ ] 3.2 Dev key set via user-secrets with a Console spend limit; the startup missing-key warning is gone
- [ ] 3.3 At least 10 real calls on a realistic pantry each finish within 60 s, with latency, tokens and stop reason recorded in `spike.md`
- [ ] 3.4 Recipes are in Polish with logical steps, and the ID validity rate is recorded in `spike.md`
- [ ] 3.5 The chosen model/effort is committed as the `Recipes` defaults in `appsettings.json` and the roadmap S-02 unknown is answered (or the split is escalated to re-planning)
- [ ] 3.6 On the production-like stack (`local-prod.ps1`), one generation returns proposals
