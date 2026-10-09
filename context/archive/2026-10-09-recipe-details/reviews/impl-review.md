<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Recipe details (S-04)

- **Plan**: context/changes/recipe-details/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2
- **Date**: 2026-10-09
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Evidence: every planned item is a MATCH (commits 130e7f2, 7d7b09a, fe4bf80 touch only the planned files and context/). Re-run on 2026-10-09: `dotnet build` 0 warnings / 0 errors, `ScoreLabelsTests` 25/25, `dotnet test` 187/187, CLAUDE.md literal-colour grep over the four touched views prints nothing. Phase 1's break-check (dedup in `ToBuy`) turned the repeated-name test red. All manual rows were confirmed by the user in chat. AI strings are Razor-encoded, `/recipes` keeps `[Authorize]`, and `ToBuy` reads classifier statuses, not LLM output.

## Findings

### F1 — Every card's toggle has the same accessible name

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (accessibility)
- **Location**: Components/Ui/RecipeCard.razor:24
- **Detail**: With up to 5 cards, a screen-reader user listing buttons hears „Pokaż przepis” five times with no recipe named. The `<article>` + `<h2>` makes the context recoverable, but not from a buttons list.
- **Fix**: Give the `h2` an id derived from the component instance and point the button's `aria-describedby` at it (or add a `visually-hidden` span with the title inside the button).
- **Decision**: FIXED — aria-describedby on the toggle points at a per-card h2 id (Guid-based)

### F2 — `@key` uniqueness relies on record equality of reference-compared lists

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (reliability)
- **Location**: Components/Pages/RecipeSuggestions.razor:68
- **Detail**: `@key="proposal"` uses `RecipeProposal`'s value equality. It is unique today only because the record's `IReadOnlyList` members compare by reference (plan, Key Discoveries). If the record later gains value-equal collections or a custom `Equals`, two identical AI proposals in one batch would produce duplicate sibling keys and Blazor would throw. The reasoning lives only in the plan, not next to the code.
- **Fix**: Add a one-line Razor comment above the loop recording why `proposal` is a safe key (unique per batch, never equal across batches), so a future change to `RecipeProposal` sees the dependency.
- **Decision**: FIXED — Razor comment above the loop records why `proposal` is a safe key

### F3 — The shared-component gap list still marks RecipeCard / StatusBadge / ScoreSummary as missing

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: context/changes/recipes-ui/research.md:159-162 (§5 table)
- **Detail**: CLAUDE.md sends agents to this table for "remaining candidates". After S-03 and S-04, `RecipeScoreSummary`, `IngredientStatusBadge` (recipes side; Products.razor still has its own inline badge) and `RecipeCard` + the disclosure exist in `Components/Ui/`, but the table still says „Brak” / inline. A future agent could rebuild one of them.
- **Fix**: Update the „Stan dziś” cells for StatusBadge, RecipeCard, ScoreSummary and Disclosure to point at the `Components/Ui/` components (StatusBadge: note that Products.razor's badge is still inline).
- **Decision**: FIXED — §5 rows StatusBadge, RecipeCard, ScoreSummary and Disclosure now point at Components/Ui/ (Products.razor badge noted as still inline)
