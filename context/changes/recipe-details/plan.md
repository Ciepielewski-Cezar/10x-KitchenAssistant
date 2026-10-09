# Recipe details (S-04) Implementation Plan

## Overview

`/recipes` changes from a list of fully expanded proposals to compact, ranked cards. Each card shows the title, score, summary, prep time and the names of the missing ingredients. A „Pokaż przepis” / „Ukryj przepis” toggle expands the full recipe (ingredients with status badges, then steps) inside the same card. This is roadmap slice S-04 (FR-006, NFR struktura, NFR ≤ 1 min). The details read the `RecipeProposal` the page already holds, so opening a recipe is instant and makes no AI call.

## Current State Analysis

- Each proposal is an inline `<article class="card">` that renders everything at once: title, `RecipeScoreSummary`, summary, prep time, every ingredient with a status badge, and every step (`Components/Pages/RecipeSuggestions.razor:68-105`).
- The status badge is a private `RenderFragment` in the page (`Components/Pages/RecipeSuggestions.razor:129-134`).
- `RecipeProposal` (`Recipes/RecipeModels.cs:55-61`) already carries the full recipe and its code-computed `RecipeScore`, so no model change is needed.
- Proposals live only in the page field `proposals`. `GenerateAsync` sets it to `null` before each new batch (`Components/Pages/RecipeSuggestions.razor:167`).
- `RecipeScoreSummary` was extracted in S-03 for reuse by this slice (`Components/Ui/RecipeScoreSummary.razor`). `recipes-ui/research.md` §5 lists `RecipeCard` and a disclosure pattern as missing primitives.
- Bootstrap's JS bundle is not loaded, so `.collapse` is unavailable (CLAUDE.md, UI section).
- `/dev/ui` (`Components/Pages/DevUi.razor`) is static SSR, so a component's interactive toggle cannot be clicked there. States must be shown through parameters.
- The test project is xUnit with SQLite and has no bUnit. Label logic is unit-tested (`KitchenAssistant.Tests/Recipes/ScoreLabelsTests.cs`), and markup is proved on `/dev/ui` and by hand.

## Desired End State

After „Zaproponuj przepisy”, the user sees up to 5 ranked cards, all collapsed. A collapsed card shows:

- title
- `RecipeScoreSummary`
- summary (when present)
- „ok. N min” (when present)
- „Do kupienia: a, b” (only when something is missing)
- a „Pokaż przepis” button

Clicking the button expands that card's ingredient list (in the AI's order, each with Masz / Zawsze w domu / Brakuje), followed by the numbered steps, and the button reads „Ukryj przepis”. Several cards can be open at once. Generating again shows the new batch collapsed. Leaving or refreshing the page clears the proposals, as it does today.

Verify on `/dev/ui` (both card states plus all three badges) and on `/recipes` with the fake generator.

### Key Discoveries:

- Clearing `proposals` before each generation unmounts every card today, but only because `GenerateAsync` yields (EF query, then the generator) and Blazor renders the null state in between (`Components/Pages/RecipeSuggestions.razor:164-168`). A path that completes synchronously (e.g. a future cache) would reuse cards by position and carry `expanded` over. So the loop keys each card with `@key="proposal"`. Proposals are records whose lists compare by reference, so keys are unique within a batch and never match across batches. No reset logic is needed.
- S-03 decided that „Brakuje: N” counts missing ingredient **lines** (`Recipes/RecipeModels.cs:33-35`, `context/archive/2026-10-08-recipe-ranking/plan-brief.md`). The „Do kupienia” names follow the same rule: every missing line, in order, without removing duplicates. The names then always match the count.
- `Components.Ui` is imported globally (`Components/_Imports.razor`). A new component name must not clash with a type in `KitchenAssistant.Recipes`, which is why the score component is called `RecipeScoreSummary` and not `RecipeScore`. `RecipeCard` and `IngredientStatusBadge` clash with nothing.

## What We're NOT Doing

- No persistence of proposals (database, session storage or circuit-scoped cache). Refresh, navigating away and re-login clear them, as today. The PRD rules out history, and the roadmap's open question was settled as „current page only” (2026-10-09).
- No separate `/recipes/{id}` route and no same-page detail view that replaces the list.
- No new AI call, prompt change or schema change. The details render the existing `RecipeProposal`.
- No grouping of ingredients by status. The details keep the AI's order with badges.
- No `ActionAlert` extraction (S-03 follow-up F2). S-04 adds no alerts, so F2 stays a separate change.
- No bUnit or other component-test framework.
- No meal-parameter UI (S-05) and no ranking changes (S-03 is done).
- No deduplication of ingredient lines (S-03 decision).

## Implementation Approach

Extract the card first, then switch the page over to it. Phase 1 builds two presentational components in `Components/Ui/` and one label helper, and proves them on `/dev/ui` without touching `/recipes`. Phase 2 replaces the page's inline card with `RecipeCard` and deletes the inline badge. The toggle is ordinary Blazor component state: a `bool` in `RecipeCard`, seeded from an `InitiallyExpanded` parameter. The page needs no new state, and `/dev/ui` can render both states statically.

## Critical Implementation Details

**Toggle and accessibility.**
- The toggle is a `<button type="button">` carrying `aria-expanded`, and it stays in place when expanded, so focus does not jump.
- Render the details section only while the card is expanded. Do not hide it with CSS, so collapsed cards stay short and screen readers skip it.
- Keep the title as the card's `h2` (as today), so the page's heading outline does not change.

## Phase 1: Shared recipe card

### Overview

Build `IngredientStatusBadge`, `RecipeCard` and the „Do kupienia” label, and prove them on `/dev/ui`.

### Changes Required:

#### 1. Missing-ingredients label

**File**: `Recipes/ScoreLabels.cs`

**Intent**: Add the Polish text for the collapsed card's „Do kupienia” line. It is built in code from the classifier's statuses, so the names come from the same source as „Brakuje: N”.

**Contract**: A new `public static string? ToBuy(IReadOnlyList<ProposalIngredient> ingredients)` returns `"Do kupienia: " + names joined with ", "`. The names are every ingredient with `IngredientStatus.Missing`, in list order, not deduplicated. The method returns `null` when nothing is missing.

#### 2. Label tests

**File**: `KitchenAssistant.Tests/Recipes/ScoreLabelsTests.cs`

**Intent**: Pin the label's rules.

**Contract**: Cover these cases:
- No missing ingredients → `null`.
- One missing → `„Do kupienia: jajka”`.
- Two missing in list order, with owned and always-at-home lines left out → `„Do kupienia: jajka, mleko”`.
- The same missing name twice → listed twice, matching `MissingCount`.

#### 3. Ingredient status badge

**File**: `Components/Ui/IngredientStatusBadge.razor` (new)

**Intent**: Move the page's private `IngredientBadge` fragment into a shared component, so the card and `/dev/ui` both use it.

**Contract**: `[Parameter, EditorRequired] IngredientStatus Status`. The markup and classes are the same as today:
- Owned → `text-bg-success` „Masz”
- AlwaysAtHome → `text-bg-secondary` „Zawsze w domu”
- Missing → `text-bg-warning` „Brakuje”

All three carry `ms-2`.

#### 4. Recipe card

**File**: `Components/Ui/RecipeCard.razor` (new)

**Intent**: One proposal as a card that is collapsed by default and has an in-place details toggle. It replaces the page's inline `article.card`.

**Contract**: The component has these parameters:
- `[Parameter, EditorRequired] RecipeProposal Proposal`
- `[Parameter] bool InitiallyExpanded` (default `false`), which seeds a private `expanded` field once in `OnInitialized`

Collapsed, the card renders in this order:
1. `h2.card-title.h4` title
2. `RecipeScoreSummary`
3. summary paragraph (when not null)
4. „ok. N min” (when not null; use `text-body-secondary`, not the legacy `text-muted`)
5. the „Do kupienia” label, `ScoreLabels.ToBuy(Proposal.Ingredients)` (when not null)
6. the toggle button (`btn btn-outline-primary btn-sm`, `aria-expanded`, text „Pokaż przepis” / „Ukryj przepis”)

Expanded, the card adds:
- „Składniki” (`h3.h6`) with a `list-group` of name, optional amount (`text-body-secondary`) and `IngredientStatusBadge`
- „Przygotowanie” (`h3.h6`) with an `<ol>` of steps

Keep the card free of literal colours (CLAUDE.md UI rules).

#### 5. Dev UI states

**File**: `Components/Pages/DevUi.razor`

**Intent**: Prove the new components in every state, as CLAUDE.md requires for shared components.

**Contract**: Add an `IngredientStatusBadge` section with all three statuses. Add a `RecipeCard` section with:
- a collapsed card that has missing ingredients (shows „Do kupienia”)
- a collapsed card with nothing missing (no „Do kupienia” line) and null summary and prep time
- an expanded card (`InitiallyExpanded="true"`) that mixes owned, always-at-home and missing ingredients

Build the sample `RecipeProposal`s inline in the page's `@code`, with scores from `RecipeScore.From`.

### Success Criteria:

#### Automated Verification:

- Build succeeds: `dotnet build`
- Label tests pass: `dotnet test --filter "FullyQualifiedName~ScoreLabelsTests"`
- Full test suite passes: `dotnet test`
- No literal colours in the new and changed views: the CLAUDE.md `grep -nE` scan over `Components/Ui/RecipeCard.razor`, `Components/Ui/IngredientStatusBadge.razor` and `Components/Pages/DevUi.razor` prints nothing new

#### Manual Verification:

- `/dev/ui` shows all three badges and the three `RecipeCard` states. The collapsed cards show only the summary block, the expanded card shows ingredients with badges and numbered steps, and „Do kupienia” appears only on the card with missing ingredients.
- At a narrow width (about 375 px), the `/dev/ui` cards wrap without horizontal scroll, and the toggle button stays visible, on one line and inside the card (it can't be clicked there, because `/dev/ui` is static SSR).

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Details on /recipes

### Overview

Switch `/recipes` to `RecipeCard`, so proposals are listed collapsed and open in place.

### Changes Required:

#### 1. Page uses the shared card

**File**: `Components/Pages/RecipeSuggestions.razor`

**Intent**: Replace the inline `article.card` loop body with `<RecipeCard Proposal="proposal" />`, and delete the private `IngredientBadge` fragment. The page keeps its generation, error, hidden-count and empty states unchanged.

**Contract**: `@foreach (var proposal in proposals)` renders one `<RecipeCard @key="proposal" Proposal="proposal" />` per proposal, in the ranked order. No new page state. `InitiallyExpanded` is left at its default (collapsed).

### Success Criteria:

#### Automated Verification:

- Build succeeds: `dotnet build`
- Full test suite passes: `dotnet test`
- No literal colours in the changed page: the CLAUDE.md `grep -nE` scan over `Components/Pages/RecipeSuggestions.razor` prints nothing

#### Manual Verification:

- With the fake generator, „Zaproponuj przepisy” lists the ranked proposals collapsed, each with its score, summary, prep time and, where relevant, „Do kupienia: …” matching its „Brakuje: N” badge.
- „Pokaż przepis” expands that card's ingredients (AI order, correct badges) and steps instantly, the button switches to „Ukryj przepis”, and a second click collapses it. Two cards can be open at the same time.
- Clicking „Zaproponuj przepisy” again shows the new batch with every card collapsed.
- The page's other states are unchanged: no products, generation failure with retry, all proposals over the limit, and the „Ukryto N propozycji” note.
- At a narrow width (about 375 px), the collapsed list and an expanded card read cleanly, with no horizontal scroll.

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- The „Do kupienia” label (`ScoreLabelsTests`): none missing, one, several in order with other statuses excluded, and a repeated name.
- Existing classifier, ranker, score and service tests stay green. This slice does not change them.

### Integration Tests:

- None added. The page has no new service calls, and the generation path is covered by `RecipeServiceTests`.

### Manual Testing Steps:

1. Open `/dev/ui` and check the badge section and the three `RecipeCard` states.
2. Sign in, make sure some products exist, open `/recipes` (fake generator), and generate.
3. Expand and collapse two different cards, and compare each „Do kupienia” line with its „Brakuje” badge and ingredient badges.
4. Generate again and confirm every card is collapsed.
5. Repeat steps 3–4 at about 375 px width.

## Performance Considerations

Opening details is a local re-render of one component and makes no network or AI call, so the PRD's „full recipe within one minute” guardrail is met by generation alone (S-02's 60 s deadline).

## Migration Notes

None. No schema or data change.

## References

- Roadmap slice: `context/foundation/roadmap.md` (S-04)
- PRD: `context/foundation/prd.md` (FR-006, NFR struktura, NFR ≤ 1 min, Non-Goals)
- UI primitives gap list: `context/changes/recipes-ui/research.md` §5 (RecipeCard, Disclosure)
- Prior slice: `context/archive/2026-10-08-recipe-ranking/plan.md` (`RecipeScoreSummary`, line-based missing count)
- Deferred follow-up: `context/archive/2026-10-08-recipe-ranking/follow-ups/review-fixes.md` (F2 `ActionAlert`)
- Current card markup: `Components/Pages/RecipeSuggestions.razor:68-105`, badge `:129-134`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Shared recipe card

#### Automated

- [x] 1.1 Build succeeds: `dotnet build`
- [x] 1.2 Label tests pass: `dotnet test --filter "FullyQualifiedName~ScoreLabelsTests"`
- [x] 1.3 Full test suite passes: `dotnet test`
- [x] 1.4 No literal colours in the new and changed views (CLAUDE.md grep scan)

#### Manual

- [x] 1.5 `/dev/ui` shows all three badges and the three `RecipeCard` states correctly
- [x] 1.6 `/dev/ui` cards wrap at about 375 px without horizontal scroll

### Phase 2: Details on /recipes

#### Automated

- [ ] 2.1 Build succeeds: `dotnet build`
- [ ] 2.2 Full test suite passes: `dotnet test`
- [ ] 2.3 No literal colours in the changed page (CLAUDE.md grep scan)

#### Manual

- [ ] 2.4 Generated proposals are listed collapsed with score, summary, time and a matching „Do kupienia” line
- [ ] 2.5 „Pokaż przepis” / „Ukryj przepis” expands and collapses a card in place, and two cards can be open at once
- [ ] 2.6 Generating again shows the new batch collapsed
- [ ] 2.7 Other page states are unchanged (no products, failure, all over limit, hidden note)
- [ ] 2.8 Collapsed list and expanded card read cleanly at about 375 px
