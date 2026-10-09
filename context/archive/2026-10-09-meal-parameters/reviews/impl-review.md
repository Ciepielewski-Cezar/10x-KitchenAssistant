<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Meal Parameters (S-05)

- **Plan**: context/changes/meal-parameters/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2
- **Date**: 2026-10-09
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Success criteria were re-run during the review:
- `dotnet build`: 0 warnings.
- `dotnet test`: 224/224 passed.
- `RecipePromptTests`: 8/8 passed, and the file is unchanged.
- The literal-colour scan is clean.

Manual items 1.4 and 2.4–2.9 were confirmed by the user in session.

The drift sweep found no DRIFT or MISSING items. The tests that go beyond the plan's minimum, such as the option lists matching the PRD, the out-of-enum meal type and the 15/16 boundary through the service, are benign. The plan says "at minimum".

## Findings

### F1 — Option lists duplicated between /recipes and /dev/ui

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: Components/Pages/DevUi.razor:118-125, Components/Pages/RecipeSuggestions.razor:131-138
- **Detail**: Both pages hold the same three `(Value, Label)` lists, built from `MealParameters.*Options` and `MealLabels`. CLAUDE.md asks to extract shared code rather than copy it. If the labels change, the /dev/ui kitchen sink can drift from the real page.
- **Fix**: Expose the three lists once, for example as static properties on `MealLabels` (`MealTypeChoices`, `MaxPrepTimeChoices`, `ServingsChoices`), and use them from both pages.
- **Decision**: FIXED

### F2 — MealTypeOptions is a mutable array behind IReadOnlyList

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Recipes/RecipeModels.cs:21
- **Detail**: `Enum.GetValues<MealType>()` returns a `MealType[]`. Code that casts it back to an array could change the process-wide allow-list that `IsAllowed` relies on. The other two lists are collection expressions, which compile to read-only wrappers.
- **Fix**: Use `[.. Enum.GetValues<MealType>()]` or `Array.AsReadOnly(Enum.GetValues<MealType>())`.
- **Decision**: FIXED

### F3 — Log lines read "max (null) min" when there is no time limit

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Recipes/RecipeService.cs:57, Recipes/RecipeService.cs:87
- **Detail**: With "bez limitu", `MaxPrepMinutes` is null, and the templates render "max (null) min" and "over the time limit of (null) min". The structured property is still correct, so this is cosmetic.
- **Fix**: Drop the "min" unit from the templates (for example "max prep {MaxPrepMinutes}"), so a null value reads naturally.
- **Decision**: FIXED

### F4 — SegmentedControl.Value is not EditorRequired

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: Components/Ui/SegmentedControl.razor:30-31
- **Detail**: If a caller omits `Value` with `TValue=int`, it defaults to 0, which isn't an option, so nothing renders as checked and nothing warns. All current callers pass `Value`.
- **Fix**: Mark `Value` as `[Parameter, EditorRequired]`.
- **Decision**: FIXED
