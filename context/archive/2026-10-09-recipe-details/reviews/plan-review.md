<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Recipe details (S-04)

- **Plan**: context/changes/recipe-details/plan.md
- **Mode**: Deep
- **Date**: 2026-10-09
- **Verdict**: SOUND
- **Findings**: 0 critical, 0 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | PASS |
| Plan Completeness | PASS |

## Grounding
6/6 paths ✓, 6/6 symbols ✓, brief↔plan ✓, Progress↔Phase ✓ (6 + 8 items)

Verified against code: the card unmount on regenerate (the page clears `proposals`, then `RecipeService.GenerateAsync` awaits the EF query and the generator; the fake generator waits 1.5 s with `Task.Delay`), no name clashes with `KitchenAssistant.Recipes`, `/dev/ui` is static SSR, `RecipeProposal` needs no change, `IngredientBadge` is used only on the page, `RecipeRanker.MaxMissing = 2`.

## Findings

### F1 — "New batch starts collapsed" depends on an implicit render

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Key Discoveries bullet 1; Phase 2 §1
- **Detail**: Cards unmount today only because `GenerateAsync` yields and Blazor renders the null state in between. If a path completed synchronously (e.g. a future S-07 cache), Blazor would reuse the cards by position and carry the `expanded` flag over to the new recipes.
- **Fix**: Add `@key="proposal"` on `RecipeCard` in the loop; drop the "Neither `@key` nor reset logic is needed" claim.
- **Decision**: FIXED

### F2 — „Do kupienia” label method is unnamed

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 1 §1–§2
- **Detail**: The tests and `RecipeCard` both call a "new public static method" whose name and signature are left open.
- **Fix**: Name it `ScoreLabels.ToBuy(IReadOnlyList<ProposalIngredient> ingredients)` returning `string?`; reference it from the card contract.
- **Decision**: FIXED

### F3 — Phase 1 says the button "stays usable" on a static page

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 1, Manual Verification bullet 2
- **Detail**: `/dev/ui` is static SSR, so the toggle can't be clicked there; "usable" isn't checkable.
- **Fix**: Reword to "stays visible, on one line and inside the card".
- **Decision**: FIXED
