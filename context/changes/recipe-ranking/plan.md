# Recipe ranking (S-03) Implementation Plan

## Overview

A signed-in user who asks for recipes on „Przepisy” (`/recipes`) sees the proposals ordered by the app: first the ones that need no shopping and use the most „zużyj w pierwszej kolejności” products, then the ones with 1–2 missing ingredients. Every card shows „Masz X z Y składników”, the number of missing ingredients and how many use-first products the recipe uses. Proposals with more than 2 missing ingredients are not shown. Score, missing count and order are computed **in C#** from S-02's ingredient classification, never read from the AI (CLAUDE.md hard rule, PRD *Business Logic*). This is roadmap slice S-03 (FR-005, US-01, Business Logic).

## Current State Analysis

S-02 (archived in `context/archive/2026-10-03-first-recipe-generation/`) shipped the generation pipeline: `RecipeService.GenerateAsync` → `IRecipeGenerator` → `RecipeResponseParser` → `RecipeClassifier`. Every ingredient is already classified in C# as `Owned` (with the user's `ProductId` and `Category`), `AlwaysAtHome` or `Missing`. S-02 deliberately left out the score, the sort and the ≤ 2-missing filter (`context/archive/2026-10-03-first-recipe-generation/plan.md:45`), so today:

- proposals appear in the AI's order, capped at the first 5 usable ones;
- a proposal with 3+ missing ingredients is shown (and a test pins that: `Recipe_with_three_missing_ingredients_is_kept`);
- the card shows per-ingredient badges but no score;
- zero usable proposals is reported as `Failed` („Nie udało się wygenerować przepisów”).

## Desired End State

On `/recipes`, after „Zaproponuj przepisy”:

- Proposals with more than 2 `Missing` ingredients are removed; the rest are sorted by: missing count ascending → number of distinct use-first products used descending → owned count (X) descending → the AI's order; then capped at 5.
- Each card shows, under the title, a `RecipeScoreSummary`: „Masz X z Y składników”, a badge „Bez zakupów” (0 missing) or „Brakuje: N” (1–2), and a muted „Zużywa N produktów do szybkiego zużycia” when N > 0.
- When some proposals were removed, a muted note under the list says „Ukryto N propozycji z więcej niż 2 brakami” (Polish plural forms).
- When the AI returned usable recipes but all were removed, the page shows a dedicated message with „Spróbuj ponownie” instead of the generic failure.
- Verified by unit tests (no AI) for every ordering key, tiebreak and counting edge case, `/dev/ui` states for the new component, and a walkthrough on the fake generator.

### Key Discoveries:

- Classification is complete and reusable: `Recipes/RecipeClassifier.cs:120-142` sets `Status`, `ProductId` and `Category` for each ingredient; the score needs no new matching logic.
- The 5-proposal cap is applied inside classification, in AI order (`Recipes/RecipeClassifier.cs:61-64`). Filtering after that cap would discard valid proposals, so the cap must move after filter + sort.
- `RecipeService` maps 0 proposals to `Failed` (`Recipes/RecipeService.cs:62-66`); an all-filtered result needs its own status.
- The prompt already tells the AI „at most 2 additional ingredients” (`Recipes/RecipePrompt.cs:19`), satisfying the PRD's „AI otrzymuje ten sam limit”. The literal 2 should come from the same constant the filter uses, without changing the rendered prompt text (S-07 measures this exact prompt).
- `Components/_Imports.razor` imports `KitchenAssistant.Components.Ui` globally and the page imports `KitchenAssistant.Recipes`; a component named `RecipeScore` would collide with a `RecipeScore` record, hence `RecipeScoreSummary`.
- The fake generator returns 3 recipes, none with 3+ missing (`Recipes/FakeRecipeGenerator.cs:18-71`, asserted in `KitchenAssistant.Tests/Recipes/FakeRecipeGeneratorTests.cs:34`), so the hidden-proposal path is not visible in development yet.
- `context/changes/recipes-ui/research.md:160-161` lists `ScoreSummary` and `RecipeCard` as the missing UI primitives for exactly this slice; Bootstrap `.badge` + `.text-bg-*` are the tokenised roles already used for ingredient badges (`Components/Pages/RecipeSuggestions.razor:110-115`).

## What We're NOT Doing

- No prompt or schema change, and no asking the AI for more than 5 recipes (decision: keep 5; the list may be shorter after filtering). S-07's latency measurement stays valid.
- No progress-bar meter; no extraction of a full `RecipeCard` component (S-04 can do it when the details view needs it).
- No deduplication of ingredient lines for X/Y — counts follow the lines on the card.
- No persistence of ranked proposals, no details view (S-04), no meal-parameter UI (S-05).
- No fuzzy matching changes to ownership or the always-at-home list.
- No change to how `ClassificationStats` measure ID validity.

## Implementation Approach

Keep ownership (S-02's classifier) and ranking (new) as separate pure steps. The classifier keeps deciding each ingredient's status and now also attaches a `RecipeScore` derived only from those statuses; it stops capping. A new static `RecipeRanker` filters by the missing limit, sorts with a stable LINQ order (so the AI's order is the last tiebreak), and caps at 5. `RecipeService` reports the hidden count and distinguishes „nothing usable” (`Failed`) from „everything over the limit” (`NoneWithinMissingLimit`). The page renders the score through a new shared component and the two new states.

Decisions agreed during planning:

- **Sort key:** `MissingCount` asc → `UseFirstCount` desc → `OwnedCount` desc → AI order.
- **Counting:** X = `Owned` lines, Y = `Owned` + `Missing` lines (always-at-home excluded), missing = `Missing` lines; use-first count = distinct `ProductId`s among `Owned` lines whose `Category` is `UseFirst`. Example: „masło” owned and listed twice → X and Y each count 2, use-first counts 1 (if masło is use-first).
- **Y = 0** (recipe built only from always-at-home items): dropped as unusable in classification, not counted as hidden.
- **Empty result:** dedicated status + message with retry; partial hiding shows the „Ukryto N…” note.
- **Score UI:** shared `RecipeScoreSummary` in `Components/Ui/` with badges, proved on `/dev/ui`.

## Critical Implementation Details

**State sequencing** — Order in `RecipeService` is classify (all usable recipes, no cap) → rank (filter > 2 missing, sort, cap 5). The `Failed` check must look at the classifier's usable count, and the new `NoneWithinMissingLimit` check at the ranker's output; checking only the final list would report a broken AI response as „too many missing” or vice versa.

**User experience spec** — Polish plural forms are needed for the hidden note („1 propozycję / 2–4 propozycje / 5+ propozycji”, with 12–14 → „propozycji”), the use-first line („1 produkt / 2–4 produkty / 5+ produktów”) and „Masz X z Y składników” (Y = 1 → „składnika”). Put one plural helper in the feature's labels, not in the view.

## Phase 1: Score, filter and ranking in C#

### Overview

All ranking logic lands as pure, unit-tested code, and the service returns ranked proposals with a hidden count. The existing page keeps working unchanged (it ignores the new data) until Phase 2.

### Changes Required:

#### 1. Score model and result shape

**File**: `Recipes/RecipeModels.cs`

**Intent**: Give every proposal an app-computed score and let the result tell the page how many proposals were hidden and whether all were.

**Contract**: New `public record RecipeScore(int OwnedCount, int CountedCount, int MissingCount, int UseFirstCount)` with a static factory `RecipeScore.From(IReadOnlyList<ProposalIngredient>)` implementing the counting rule above. `RecipeProposal` gains a trailing `RecipeScore Score` parameter. `RecipeGenerationStatus` gains `NoneWithinMissingLimit`. `RecipeGenerationResult` gains `int HiddenCount` (0 for every status except `Succeeded` with hidden proposals and `NoneWithinMissingLimit`).

#### 2. Classifier: score and no cap

**File**: `Recipes/RecipeClassifier.cs`

**Intent**: Attach the score when a proposal is built, drop recipes with `CountedCount == 0` as unusable, and stop capping at 5 so ranking sees every usable recipe.

**Contract**: `Classify(...)` returns all usable proposals in AI order. `MaxProposals` moves to `RecipeRanker` (update every reference). `ClassificationStats.Proposals` now means „usable proposals” — adjust its comment; the log line in `RecipeService` keeps its fields.

#### 3. Ranker

**File**: `Recipes/RecipeRanker.cs` (new)

**Intent**: The single place that applies the PRD's „mała lista braków” limit and order.

**Contract**: `public static class RecipeRanker` with `public const int MaxMissing = 2`, `public const int MaxProposals = 5`, and `public static RankedProposals Rank(IReadOnlyList<RecipeProposal> proposals)` returning `record RankedProposals(IReadOnlyList<RecipeProposal> Proposals, int HiddenCount)`. Filter `Score.MissingCount > MaxMissing` (counted in `HiddenCount`), then stable `OrderBy(MissingCount).ThenByDescending(UseFirstCount).ThenByDescending(OwnedCount)`, then `Take(MaxProposals)`. Proposals cut by the cap are not counted as hidden (the note is only about the missing limit).

#### 4. Service

**File**: `Recipes/RecipeService.cs`

**Intent**: Rank after classifying, and report the two empty outcomes separately.

**Contract**: 0 usable proposals → `Failed` (unchanged log). Usable > 0 but ranked list empty → `NoneWithinMissingLimit` with `HiddenCount`, logged at Information („all N usable proposals exceeded the missing limit”). Otherwise `Succeeded` with ranked proposals and `HiddenCount`. Log the hidden count alongside the classification line.

#### 5. Prompt constant

**File**: `Recipes/RecipePrompt.cs`

**Intent**: Make the AI's limit and the app's filter one value.

**Contract**: Interpolate `RecipeRanker.MaxMissing` (and `RecipeRanker.MaxProposals` for „up to 5”) into `System`. The rendered prompt string must be byte-identical to today's — assert it in `RecipePromptTests`.

#### 6. Score labels

**File**: `Recipes/ScoreLabels.cs` (new)

**Intent**: Polish display text for the score and the hidden note, with correct plural forms, kept out of the view.

**Contract**: `public static class ScoreLabels` with `Owned(RecipeScore)` → „Masz X z Y składników/składnika”, `Missing(int)` → „Bez zakupów” / „Brakuje: N”, `UseFirst(int)` → „Zużywa N produkt/produkty/produktów do szybkiego zużycia”, `Hidden(int)` → „Ukryto N propozycję/propozycje/propozycji z więcej niż 2 brakami” (the 2 from `RecipeRanker.MaxMissing`). One private Polish plural helper (one / few: last digit 2–4 except 12–14 / many).

#### 7. Fake generator

**File**: `Recipes/FakeRecipeGenerator.cs`

**Intent**: Make the hidden-proposal path visible in development at zero cost.

**Contract**: Add a 4th recipe with exactly 3 `null`-ID ingredients outside the always-at-home list (e.g. „śmietana 30%”, „pieczarki”, „natka pietruszki”) plus one owned product. Update the class comment and `FakeRecipeGeneratorTests` (count 4, the new recipe is hidden after ranking).

#### 8. Tests

**Files**: `KitchenAssistant.Tests/Recipes/RecipeRankerTests.cs` (new), `RecipeScoreTests.cs` (new) or within the ranker tests, `ScoreLabelsTests.cs` (new), `RecipeClassifierTests.cs`, `RecipeServiceTests.cs`, `RecipePromptTests.cs`, `FakeRecipeGeneratorTests.cs`

**Intent**: Prove the rule without AI.

**Contract**: Cover at least — 0 missing ranks above 1 and 2 missing; within equal missing, more use-first wins (the A/B case: 1 missing, Masz 2 z 3, 0 use-first vs 1 missing, Masz 6 z 7, 2 use-first → B first); then more owned wins; full ties keep AI order; exactly 2 missing is kept, 3 is hidden and counted; the cap applies after sorting (a 6th better recipe displaces a worse one); cap-cut proposals are not counted as hidden. Score: always-at-home excluded from Y; a duplicated owned line counts twice in X/Y and once in use-first; owned-by-name ingredients count as owned; stored products don't count as use-first. Classifier: Y = 0 recipe is dropped; no cap at 5 any more (replace `Only_the_first_five_usable_recipes_are_kept_in_ai_order`); `Recipe_with_three_missing_ingredients_is_kept` stays valid for the classifier (it doesn't filter) — rename or comment so it's clear the ranker hides it. Service: all-over-limit → `NoneWithinMissingLimit` with `HiddenCount`; unusable → still `Failed`; mixed → `Succeeded`, sorted, `HiddenCount` set. Labels: plural forms for 1, 2, 4, 5, 12, 22.

### Success Criteria:

#### Automated Verification:

- Solution builds without warnings in touched files: `dotnet build`
- All tests pass, including the new ranker, score and label tests: `dotnet test`
- No EF model change: `dotnet ef migrations has-pending-model-changes` exits 0
- Rendered system prompt is unchanged (asserted in `RecipePromptTests`)

#### Manual Verification:

- Review that no score, missing count or order value is read from AI output (only `IngredientStatus` from the classifier feeds `RecipeScore`)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Score on the page

### Overview

The user sees the ranked list with a score on each card, the hidden note, and a dedicated message when everything was hidden.

### Changes Required:

#### 1. Shared score component

**File**: `Components/Ui/RecipeScoreSummary.razor` (new)

**Intent**: One reusable presentation of the score for the proposal list now and the details view in S-04.

**Contract**: Parameter `[EditorRequired] RecipeScore Score`. Renders „Masz X z Y składników” (`ScoreLabels.Owned`), a badge `text-bg-success` „Bez zakupów” when `MissingCount == 0` else `text-bg-warning` „Brakuje: N”, and, when `UseFirstCount > 0`, a `text-body-secondary` line from `ScoreLabels.UseFirst`. Only Bootstrap role classes or `var(--token)`; no literal colours.

#### 2. Dev kitchen sink

**File**: `Components/Pages/DevUi.razor`

**Intent**: Prove the component's states, as CLAUDE.md requires for every new shared component.

**Contract**: A `RecipeScoreSummary` section with four states: 0 missing + use-first 2; 1 missing + use-first 1; 2 missing + use-first 0; Y = 1 (singular „składnika”).

#### 3. Recipes page

**File**: `Components/Pages/RecipeSuggestions.razor`

**Intent**: Show the score on every card, the hidden note, and the all-hidden state.

**Contract**: `<RecipeScoreSummary Score="proposal.Score" />` directly under the card title. After the list, when `HiddenCount > 0`, a `text-body-secondary` paragraph with `ScoreLabels.Hidden`. New page state for `NoneWithinMissingLimit`: an `alert alert-warning` with „Żadna propozycja nie mieści się w limicie 2 brakujących składników. Spróbuj ponownie albo dodaj produkty.” (limit from `RecipeRanker.MaxMissing`) plus „Spróbuj ponownie” (`btn-outline-warning`, same handler/disable rule as the existing retry) and a link to `products`. Reset the new state at the start of each generation, like `failed`.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build`
- All tests pass: `dotnet test`
- No colour literals in touched UI files: `grep -nE '#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|oklch\(|:\s*(white|black)\b|navbar-(dark|light)' Components/Ui/RecipeScoreSummary.razor Components/Pages/RecipeSuggestions.razor Components/Pages/DevUi.razor` prints nothing

#### Manual Verification:

- On the fake generator (`dotnet run --launch-profile https`, Development), `/recipes` shows 3 proposals in rank order with correct „Masz X z Y”, badges and use-first lines, plus „Ukryto 1 propozycję z więcej niż 2 brakami”
- The all-hidden state shows the warning, retry and product link (forced by a temporary, uncommitted edit that makes the fake return only its 3-missing recipe; the status itself is covered by `RecipeServiceTests`)
- `/dev/ui` shows the four `RecipeScoreSummary` states readable at phone width (375 px) with no horizontal scroll
- Existing states still work: no products, generation failure with retry, loading spinner

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- `RecipeRanker`: every sort key in isolation, full-tie stability, the missing limit boundary (2 kept / 3 hidden), cap after sort, hidden count excluding cap cuts.
- `RecipeScore.From`: always-at-home exclusion, duplicate owned lines, use-first distinctness, owned-by-name, stored vs use-first.
- `ScoreLabels`: plural forms at 1, 2, 4, 5, 12, 22.
- `RecipeClassifier`: Y = 0 dropped, no cap.
- `RecipeService`: the three outcomes (`Failed`, `NoneWithinMissingLimit`, `Succeeded` with hidden count).
- `RecipePrompt`: rendered text unchanged.

### Integration Tests:

- Existing `RecipeServiceTests` (SQLite in-memory) extended for the new statuses; no real-API tests.

### Manual Testing Steps:

1. Sign in, make sure some products are „zużyj w pierwszej kolejności”, open `/recipes`, click „Zaproponuj przepisy”.
2. Check the order and each card's score against its ingredient badges by hand.
3. Check the „Ukryto 1 propozycję…” note.
4. Open `/dev/ui` and check the four score states, including at 375 px.

## Performance Considerations

Ranking sorts at most a handful of proposals in memory; no measurable cost. No change to the AI call, so the ≤ 1 min guardrail and S-07's measurement are unaffected.

## Migration Notes

None. No schema or persisted data changes.

## References

- Roadmap slice: `context/foundation/roadmap.md` (S-03)
- PRD rule: `context/foundation/prd.md` § Business Logic
- S-02 plan and boundary: `context/archive/2026-10-03-first-recipe-generation/plan.md:5,45`
- UI primitives: `context/changes/recipes-ui/research.md:151-165`
- Classification: `Recipes/RecipeClassifier.cs:46-142`
- Service flow: `Recipes/RecipeService.cs:20-93`
- Page: `Components/Pages/RecipeSuggestions.razor:53-115`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Score, filter and ranking in C#

#### Automated

- [x] 1.1 Solution builds without warnings in touched files: `dotnet build`
- [x] 1.2 All tests pass, including the new ranker, score and label tests: `dotnet test`
- [x] 1.3 No EF model change: `dotnet ef migrations has-pending-model-changes` exits 0
- [x] 1.4 Rendered system prompt is unchanged (asserted in `RecipePromptTests`)

#### Manual

- [x] 1.5 Review that no score, missing count or order value is read from AI output

### Phase 2: Score on the page

#### Automated

- [ ] 2.1 Solution builds: `dotnet build`
- [ ] 2.2 All tests pass: `dotnet test`
- [ ] 2.3 No colour literals in touched UI files

#### Manual

- [ ] 2.4 Fake generator: ranked proposals with correct scores and the „Ukryto 1 propozycję” note
- [ ] 2.5 All-hidden state shows the warning, retry and product link
- [ ] 2.6 `/dev/ui` shows the four `RecipeScoreSummary` states at phone width
- [ ] 2.7 Existing states still work: no products, failure with retry, loading spinner
