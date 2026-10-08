<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Recipe ranking (S-03)

- **Plan**: context/changes/recipe-ranking/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2
- **Date**: 2026-10-08
- **Verdict**: APPROVED
- **Findings**: 0 critical, 2 warnings, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Evidence: the diff `ea5b287..HEAD` touches exactly the files the plan lists. All 11 planned items match the plan's intent. The only extras are justified test additions and the `ClassifyOne` helper adaptation. `dotnet build` reports 0 warnings, `dotnet test` passes 163/163, `has-pending-model-changes` exits 0, the prompt literal test is green and the colour scan finds nothing. The CLAUDE.md hard rule holds: `RecipeScore.From` reads only classifier statuses. Manual rows 1.5 and 2.4–2.7 were confirmed by the user in-session.

## Findings

### F1 — Retry button has unreadable contrast in the all-hidden alert

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (accessibility)
- **Location**: Components/Pages/RecipeSuggestions.razor:58
- **Detail**:
  - The plan asked for `btn-outline-warning` inside `alert-warning`.
  - `wwwroot/app.css:207-211` leaves the warning role at Bootstrap's values and does not re-point `.btn-outline-warning` (only `.btn-outline-danger`, at :278).
  - The button text therefore renders #ffc107 on the alert's #fff3cd background, about 1.5:1, far below the WCAG AA minimum of 4.5:1.
  - The plan specified this class, so the flaw is in the plan, not the implementation.
- **Fix**: Change the button to `btn btn-warning btn-sm`. Bootstrap's solid warning button uses dark text on yellow, which passes AA, and needs no token work.
- **Decision**: FIXED (button switched to `btn-warning`)

### F2 — Fourth inline copy of "alert with retry action"

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Pattern Consistency
- **Location**: Components/Pages/RecipeSuggestions.razor:55-62
- **Detail**:
  - CLAUDE.md says that when an "alert with action" appears in a second page, it should be extracted into `Components/Ui/`.
  - `context/changes/recipes-ui/research.md:162` already lists "Alert z akcją / ErrorState" with 3 inline copies (RecipeSuggestions.razor:22, :47-50, Products.razor:76). This change adds a 4th copy.
  - Its layout also differs from the error alert right above it: a `<p>` plus a wrapping row with `gap-3`, against `d-flex` with `ms-3`.
- **Fix A**: Extract `ActionAlert` into `Components/Ui/` (parameters: kind, message, action `RenderFragment`), add it to `/dev/ui`, and use it for the 3 alerts on this page (and Products.razor:76).
  - Strength: satisfies the CLAUDE.md shared-component rule, and the two alerts get the same layout.
  - Tradeoff: touches the existing error states outside this slice's scope and needs a new manual pass on `/products` and `/recipes`.
  - Confidence: MED — the markup is simple, but the four copies differ slightly (dismiss, link vs button).
  - Blind spot: whether S-04/S-05 will need variants that would shape the API.
- **Fix B ⭐ Recommended**: Leave it inline and queue the extraction as a follow-up (a small UI change of its own).
  - Strength: keeps S-03 inside its approved scope. The plan's "What We're NOT Doing" already deferred component extraction (RecipeCard) the same way.
  - Tradeoff: a 4th copy stays until the follow-up lands.
  - Confidence: HIGH — the debt is already tracked in research.md §5.
  - Blind spot: the follow-up may slip behind MVP work.
- **Decision**: FIXED via Fix B (queued in `follow-ups/review-fixes.md`)

### F3 — A duplicated owned line inflates the owned-count tiebreak

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (ranking integrity)
- **Location**: Recipes/RecipeModels.cs:40
- **Detail**:
  - `OwnedCount` counts lines, so if the AI repeats an owned product, X rises.
  - X is the third sort key, so the repeat can lift a recipe above an otherwise tied one.
  - This was agreed during planning ("No deduplication of ingredient lines for X/Y") and is pinned by `RecipeScoreTests.Duplicated_owned_line_counts_twice_but_uses_up_one_product`.
- **Fix**: No change now. Revisit only if real responses show duplicate lines; the alternative is to count distinct `ProductId` for X.
- **Decision**: FIXED (X now counts distinct owned ProductIds and Y = X + missing lines; reverses the plan's "no deduplication" decision at the user's request; test renamed `Duplicated_owned_line_counts_once`)

### F4 — Recipe count from the AI is no longer bounded by the classifier

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (performance)
- **Location**: Recipes/RecipeSchema.cs:13
- **Detail**:
  - With the cap moved to the ranker, every recipe the AI returns is classified and scored.
  - The schema has no `maxItems`, so the only bound is the model's `max_tokens`.
  - The work is linear and tiny, so the risk is negligible. The plan deliberately forbids schema changes in this slice.
- **Fix**: None in this slice. Consider `maxItems` together with any future schema change (it would have to stay compatible with S-07's measurement).
- **Decision**: SKIPPED

### F5 — "z więcej niż 2 brakami" reads awkwardly

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence (copy)
- **Location**: Recipes/ScoreLabels.cs:16
- **Detail**: The copy follows the plan exactly, but the phrase is clunky Polish. A more natural wording would be "…, którym brakuje więcej niż 2 składników".
- **Fix**: Optionally reword `ScoreLabels.Hidden` and its tests. This is a product-copy call.
- **Decision**: FIXED (copy is now "Ukryto N propozycję, której / propozycje, którym / propozycji, którym brakuje więcej niż 2 składników"; ScoreLabelsTests updated. Plan row 2.4 still quotes the old text)

### F6 — No combined test that a Y = 0 recipe is excluded from HiddenCount

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria (test coverage)
- **Location**: KitchenAssistant.Tests/Recipes/RecipeServiceTests.cs
- **Detail**:
  - Each half is covered separately: the classifier drops Y = 0, and the service returns `Failed` with `HiddenCount` 0.
  - No service test feeds an always-at-home-only recipe and an over-limit recipe together and asserts `HiddenCount == 1`.
- **Fix**: Add one `RecipeServiceTests` case that mixes the two and asserts `NoneWithinMissingLimit` with `HiddenCount` 1.
- **Decision**: FIXED (added `RecipeServiceTests.Always_at_home_recipe_is_not_counted_as_hidden`; break-check: went red with the Y = 0 drop disabled)

## Triage summary

- Fixed: F1, F2 (Fix B, queued as a follow-up), F3, F5, F6
- Skipped: F4
- After the fixes: `dotnet build` reports 0 warnings, `dotnet test` passes 164/164, and the colour scan is clean.
