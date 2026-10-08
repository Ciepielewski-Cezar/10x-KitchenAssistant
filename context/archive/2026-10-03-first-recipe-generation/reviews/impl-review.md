<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: First recipe generation (S-02)

- **Plan**: context/changes/first-recipe-generation/plan.md
- **Scope**: Full plan, covering the completed phases 1–2 of 3 (Phase 3 is not started; the uncommitted Phase 3 prep in the working tree was also read)
- **Reviewed phases**: 1, 2
- **Date**: 2026-10-05
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Automated checks were run on the working tree (commits 214a0a9 and 10bd128 plus the uncommitted edits):

- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test KitchenAssistant.slnx`: 105 passed.
- `dotnet ef migrations has-pending-model-changes`: no changes, exit 0.

The CLAUDE.md hard rules hold:

- Ingredient status is decided only in `RecipeClassifier`.
- AI product IDs are checked against the caller's own `ProductList`.
- Generation uses structured output.
- The page is `[Authorize]` and takes `userId` from the user's claims.
- AI text is rendered only through Razor encoding, with no `MarkupString`.

## Findings

### F1 — Open sign-up with no per-user limit on paid AI calls

- **Severity**: ⚠️ WARNING
- **Impact**: 🔬 HIGH — architectural stakes; think carefully before deciding
- **Dimension**: Safety & Quality
- **Location**: Components/Pages/RecipeSuggestions.razor:140, Program.cs:59-69, appsettings.Production.json:3
- **Detail**: Each click is one call that can produce up to 16,000 output tokens (`Recipes:MaxTokens`). Production sets `RequireConfirmedAccount: false`, so anyone can register and start clicking straight away. The `if (generating)` guard only covers one circuit. Several tabs, or a scripted SignalR client, can run calls in parallel and back to back. The plan defers a per-user rate limit (plan.md:340) and relies on the prod-workspace spend limit (deployment-plan.md:56). That cap bounds cost, but one abusive account can use up the monthly budget, and recipe generation then fails for every user.
- **Fix A ⭐ Recommended**: Record this as an explicit go-live item: spend limit set and verified, plus a per-user throttle tracked as a follow-up before public launch.
  - Strength: Matches the plan's deliberate deferral (plan.md:340), and the spend limit is already planned (deployment-plan.md:56).
  - Tradeoff: The budget can still be used up by one account until the throttle lands.
  - Confidence: MED — this is fine at MVP traffic, but it depends on nobody abusing the app before launch.
  - Blind spot: We don't know how quickly the spend limit takes effect in the Console once it is reached.
- **Fix B**: Add a cheap guard now: a singleton `ConcurrentDictionary<userId, …>` in `RecipeService` that allows one generation in flight per user, plus a short cooldown such as 10 s.
  - Strength: Removes parallel and scripted abuse with about 20 lines in one place, which can be tested with `FakeTimeProvider`.
  - Tradeoff: Scope creep beyond S-02. It is in-memory only, so it does not hold across App Service instances.
  - Confidence: MED — straightforward, but it adds a new user-visible state (a "please wait" message).
  - Blind spot: The scale-out plan for App Service has not been checked.
- **Decision**: FIXED (Fix A) — deployment-plan.md Phase 8 go-live blocker + Phase 10 throttle backlog item; queued in follow-ups/review-fixes.md

### F2 — Number of products sent to the LLM is unbounded

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: Recipes/RecipeService.cs:22-23, Recipes/RecipePrompt.cs:34-44
- **Detail**: Every product the user owns goes into the prompt. `ProductService` limits name length (100) and quantity length (50), but not how many products a user can have. A pantry with hundreds of items makes every click cost more and take longer, and can push it past the 60 s deadline, which shows the user a generic error. Phase 3 measures only a pantry of about 20–30 products.
- **Fix A ⭐ Recommended**: Cap the products sent in `RecipeService`: all "Do zużycia" items first, then Stored items up to a total of N (e.g. 60). Log the number sent.
  - Strength: A bounded prompt keeps the 60 s guarantee honest. It is a single, testable change in the service that owns the deadline.
  - Tradeoff: Products past the cap are left out silently. The AI then can't use them, and the name fallback can't match them.
  - Confidence: MED — the right N is a guess until Phase 3 measures token counts.
  - Blind spot: How large real pantries get.
- **Fix B**: Leave the code as is. Add one large-pantry run (about 150 products) to the Phase 3 spike, and decide on a cap from the measured data.
  - Strength: The decision rests on evidence, with no speculative code.
  - Tradeoff: One more paid call, and the risk stays open until the spike runs.
  - Confidence: HIGH — the spike already exists to measure exactly this kind of thing.
  - Blind spot: None significant.
- **Decision**: FIXED (Fix A) — `Recipes:MaxProducts` (default 60), UseFirst sent first, trimming logged; test `A_large_pantry_sends_use_first_products_first_up_to_MaxProducts`

### F3 — Unknown-ID count now covers only kept proposals (uncommitted change)

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: Recipes/RecipeService.cs:37-47, Recipes/RecipeClassifier.cs:95-98 (working tree)
- **Detail**: The committed code counted unknown product IDs across every returned recipe. The uncommitted `ClassificationStats` counts only the ingredients of kept proposals. When every recipe is dropped (`proposals.Count == 0`), `UnknownIds` is always 0, so the warning never fires, and that is the failure case where it matters most. Recipes dropped as invalid, or beyond the 5-recipe cap, are also left out. As a result, the "ID validity rate" that Phase 3 records in spike.md (plan.md:281, 3.4) understates the AI's mistakes.
- **Fix**: Count unknown IDs over all non-null recipes the AI returned, separately from the per-proposal tally (or record both). Log the stats line on the failure path too.
- **Decision**: FIXED — `ClassificationStats.ProductIds`/`UnknownIds` now cover every returned recipe; the stats line is logged before the usability check

### F4 — Generator log line is written only for calls that succeed

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: Recipes/AnthropicRecipeGenerator.cs:26-36
- **Detail**: The plan asks for "one Information line per call" with the elapsed time. The line is written only after `Messages.Create` returns. Calls that throw (429/529 after the retry, IO errors, per-attempt timeout, deadline) get no elapsed time and no model/effort line, so the Phase 3 spike can't time its failed calls.
- **Fix**: Wrap the call in try/finally, or catch, log the elapsed time and the exception type, and rethrow.
- **Decision**: FIXED — failed calls log model, effort, elapsed ms and exception type at Information, then rethrow

### F5 — DeadlineSeconds and MaxTokens are not validated at startup

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Recipes/RecipeServiceCollectionExtensions.cs:19, Recipes/RecipeService.cs:30
- **Detail**: `Effort` is checked at startup, but the other two settings are not:
  - `DeadlineSeconds` ≤ 0 makes every call fail at once, or throws `ArgumentOutOfRangeException` from the `CancellationTokenSource` constructor. That constructor sits outside the `try`, so the error is logged only by the page's generic catch.
  - `MaxTokens` ≤ 0 becomes an API 400 on the first click.
- **Fix**: Validate both values as > 0 in `AddRecipes`, next to the existing `ParseEffort` check.
- **Decision**: FIXED — `AddRecipes` requires MaxTokens, DeadlineSeconds and MaxProducts > 0; theory test `Non_positive_limit_throws_at_registration`

### F6 — Product line separator `|` is not escaped in the prompt

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Recipes/RecipePrompt.cs:36-40,55
- **Detail**: `OneLine` removes line breaks, but a name such as `ser | Do zużycia | 5 kg` can change how its own line is read. Only the user's own results are affected: IDs are checked in C#, nothing is rendered as HTML, and nothing is stored.
- **Fix**: In `OneLine`, also replace `|` with `/`.
- **Decision**: FIXED — `OneLine` replaces `|` with `/` in names and quantities; test `Separator_in_a_product_name_or_quantity_cannot_add_fields`

### F7 — Unplanned additions not recorded in the plan

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: Recipes/RecipeClassifier.cs:105,121-124; Recipes/RecipeService.cs:49-57; Recipes/AnthropicRecipeGenerator.cs:74-87; Components/Pages/RecipeSuggestions.razor:20-23
- **Detail**: The extras are all benign:
  - An ingredient with no name and no valid ID is dropped.
  - Prep times ≤ 0 become null.
  - `ParseEffort` validates the effort name at startup.
  - The page shows a product-load failure alert.
  - The uncommitted stats line and `ClassificationStats` record are added.

  None of these is in the plan. The stats log fills a real gap: the Phase 3 spike needs owned-by-ID, owned-by-name and unknown-ID counts, but the plan has no code to produce them.
- **Fix**: Add a short "Implementation notes / addenda" section to plan.md that lists them, so later reviews treat them as intended.
- **Decision**: FIXED — plan.md `## Implementation Notes (addenda)` lists them, including the review fixes F1–F6

## Triage summary

- Fixed: F1 (Fix A), F2 (Fix A), F3, F4, F5, F6, F7 (7)
- Skipped / Accepted / Rule: none
- After the fixes: `dotnet test KitchenAssistant.slnx` passes 111 of 111 tests.
