# Meal Parameters (S-05) Implementation Plan

## Overview

The user picks the meal type, the maximum preparation time and the number of servings on `/recipes` before asking for recipes (FR-004). The PRD defaults are preselected. The chosen parameters reach the AI through the existing prompt. The app enforces the time limit in code by hiding proposals that exceed it or state no time, and it captions the list with the parameters that produced it.

## Current State Analysis

S-02 already built most of the backend for this slice, but nothing lets the user change the parameters:

- `MealType`, `MealParameters(MealType, int? MaxPrepMinutes, int Servings)` and `MealParameters.Default = (Dinner, 30, 1)` exist in `Recipes/RecipeModels.cs:6-19`. `RecipeRequest` already carries `Meal` (`Recipes/RecipeModels.cs:21`).
- `MealLabels.For(MealType)` and `MealLabels.ForMaxPrepTime(int?)` exist (`Recipes/MealLabels.cs:6-16`). `RecipePrompt.BuildUserMessage` writes the three parameters into the user message in Polish (`Recipes/RecipePrompt.cs:47-51`).
- `RecipeService.GenerateAsync(string userId, CancellationToken ct)` hardcodes `MealParameters.Default` (`Recipes/RecipeService.cs:42`). The test `Only_the_callers_products_reach_the_generator_with_default_meal_parameters` asserts this (`KitchenAssistant.Tests/Recipes/RecipeServiceTests.cs:77-91`).
- `RecipeRanker.Rank` filters on the missing limit, sorts and caps the list, and reports `HiddenCount` (`Recipes/RecipeRanker.cs:11-28`). The service turns "usable, but all hidden" into `NoneWithinMissingLimit` (`Recipes/RecipeService.cs:72-77`, `Recipes/RecipeModels.cs:63-75`).
- `RecipeProposal.PrepTimeMinutes` is `int?`, because the schema allows `null` (`Recipes/RecipeSchema.cs:27-30`). The fake generator returns times 20, 30, `null` and 25 (`Recipes/FakeRecipeGenerator.cs:35-75`).
- The system prompt already says to fit "meal type, maximum preparation time and number of servings". A test freezes it as the measured baseline (`KitchenAssistant.Tests/Recipes/RecipePromptTests.cs:78-101`).
- `/recipes` (`Components/Pages/RecipeSuggestions.razor`) has no form and no `EditContext`. Its generate button and the two retry buttons all call `GenerateAsync()`.
- No segmented control exists in `Components/Ui/`. The `recipes-ui` research proposed `.btn-check` controls for S-05 (`context/changes/recipes-ui/research.md:166`).
- `.btn-outline-primary` isn't re-pointed at the tokens in `wwwroot/app.css`, unlike `.btn-primary`, `.btn-danger` and `.btn-outline-danger` (`wwwroot/app.css:245-290`). It therefore renders Bootstrap's literal blue, which `RecipeCard`'s toggle already shows (`Components/Ui/RecipeCard.razor:23`).

## Desired End State

On `/recipes`, three labelled groups of toggle buttons sit above "Zaproponuj przepisy":

- **Rodzaj posiłku:** Śniadanie, Obiad, Kolacja, Przekąska.
- **Maksymalny czas przygotowania:** do 15 minut, do 30 minut, do 60 minut, bez limitu.
- **Liczba porcji:** 1, 2, 4.

Each page load starts at Obiad, do 30 minut, 1. The controls are disabled while a generation runs.

After a click, the chosen parameters reach the generator. The list shows only proposals that are within the missing limit **and**, when a time limit is set, state a `PrepTimeMinutes` ≤ that limit. A note counts proposals hidden for time, next to the existing missing-limit note. Above the results, "Propozycje dla: Obiad, do 30 minut, 1 porcja" names the parameters the list was generated with. The caption does not follow later control changes.

When every usable proposal is hidden, one alert says "Żadna propozycja nie mieści się w limitach." It shows whichever hidden notes apply, plus a retry button and the products link.

Verify with `dotnet test`, then with the fake generator on `/recipes` using the time limits listed under Manual Testing Steps, then with one real AI run.

### Key Discoveries:

- The parameter types and prompt rendering already exist, so this slice only wires them up (`Recipes/RecipeModels.cs:14-21`, `Recipes/RecipePrompt.cs:47-51`).
- The time limit becomes a second filter in `RecipeRanker`, beside the missing limit. It keeps that pattern: filter before sorting, cap after, and count what was hidden (`Recipes/RecipeRanker.cs:10-24`).
- The page has no `EditContext`, so `InputRadioGroup` can't be used (`Components/Pages/RecipeSuggestions.razor`; compare `Components/Pages/ProductFields.razor:12-21`, which sits inside an `EditForm`).
- The Polish plural rule is private to `ScoreLabels` (`Recipes/ScoreLabels.cs:26-34`). The servings label needs it too.
- Stale worktree copies under `.claude/worktrees/` show up in greps. They aren't part of this change.

## What We're NOT Doing

- No change to the system prompt or the structured-output schema. The frozen `System_prompt_renders_exactly_as_measured` test stays as it is, and `prepTimeMinutes` stays nullable.
- No app-side check of meal type or servings. Neither can be verified from the AI's output, so both rest on the prompt.
- Chosen parameters aren't remembered: no database field, no `localStorage`, no query string. Every page load starts at the PRD defaults.
- No diet parameter (parked in the roadmap) and no extra parameter values beyond the PRD's set.
- No clearing of the list when a control changes. The caption shows which parameters produced it.
- No bUnit or browser tests. The page is verified manually, as in S-04.
- No change to ranking order, the missing limit or ownership matching.
- No `ActionAlert` extraction. The none-within-limits alert stays inline, as the S-03/S-04 alerts do.

## Implementation Approach

The logic goes first, then the UI. Phase 1 widens the service contract and adds the time filter with pure-function tests. The page keeps passing the defaults, so the build and the user experience stay unchanged, apart from the new time-hidden note under defaults. Phase 2 adds a shared `SegmentedControl` (token-clean, shown on `/dev/ui`) and wires three instances into the page.

The allowed values live once, on `MealParameters`. The UI renders its options from them and the service rejects anything else, so the "small, defined set" from FR-004 has a single definition.

## Critical Implementation Details

- **State sequencing.** Capture the parameters at click time into a `generatedFor` snapshot that the caption reads. Never caption from the live `selected` state, because the caption must keep naming the old parameters after the user changes a control. The retry buttons generate with the current `selected` values and refresh the snapshot.
- **No parsing of client values.** Each radio's change handler must call `ValueChanged` with the option value captured on the server, not with the posted string. An `InteractiveServer` event then can never carry a value outside the option list.
- **No loop-variable capture.** Render the options with `foreach` (e.g. over `Options.Select((option, index) => (option, index))`), or copy the index and option into locals before the lambda. A `for (var i…)` loop whose `@onchange` lambda captures `i` sees the value `i` has after the loop when the event fires, which breaks the rule above.

## Phase 1: Parameters through the service, time limit in the ranking

### Overview

`RecipeService` accepts the meal parameters and validates them. `RecipeRanker` also hides proposals over the time limit, and the result reports both hidden counts. This phase adds the Polish labels the UI needs. The page compiles against the new contract, keeps passing `MealParameters.Default`, and gets the reworded none-within-limits alert.

### Changes Required:

#### 1. Allowed parameter values

**File**: `Recipes/RecipeModels.cs`

**Intent**: Define the PRD's small parameter set once, so the UI renders it and the service enforces it.

**Contract**: Add static option lists to `MealParameters`:
- `MealTypeOptions`: all `MealType` values in enum order.
- `MaxPrepMinutesOptions`: `[15, 30, 60, null]`.
- `ServingsOptions`: `[1, 2, 4]`.

Add `bool IsAllowed`, which is true only when all three values are in their lists. `Default` must satisfy `IsAllowed`.

#### 2. Result shape and status

**File**: `Recipes/RecipeModels.cs`

**Intent**: Report time-hidden proposals separately, and name the "all hidden" outcome after both limits.

**Contract**:
- Rename `RecipeGenerationStatus.NoneWithinMissingLimit` to `NoneWithinLimits`, meaning usable proposals exist but the missing limit and the time limit hid them all.
- Change the result to `RecipeGenerationResult(Status, Proposals, int HiddenCount, int HiddenOverTimeCount)`. `HiddenCount` keeps its meaning (over `RecipeRanker.MaxMissing`). `HiddenOverTimeCount` counts the remaining proposals hidden for time. Both are 0 unless the status is `Succeeded` or `NoneWithinLimits`. Update the comments.

#### 3. Time filter in the ranking

**File**: `Recipes/RecipeRanker.cs`

**Intent**: Enforce the chosen time limit in code, using the strict rule agreed in planning.

**Contract**: `Rank(IReadOnlyList<RecipeProposal> proposals, int? maxPrepMinutes = null)` returns `RankedProposals(Proposals, int HiddenCount, int HiddenOverTimeCount)`.

The missing filter runs first, so `HiddenCount` stays exactly as it is today. A proposal over both limits counts only in `HiddenCount`. Of the proposals that pass it, the time filter keeps one when `maxPrepMinutes` is null, or when `PrepTimeMinutes` is non-null and ≤ `maxPrepMinutes`. A proposal at exactly the limit is kept. With a limit set, a proposal with a null time is hidden.

Sorting and capping are unchanged and happen after both filters.

#### 4. Service contract

**File**: `Recipes/RecipeService.cs`

**Intent**: Send the user's chosen parameters to the generator and apply their time limit.

**Contract**: `GenerateAsync(string userId, MealParameters meal, CancellationToken ct = default)`.
- If `!meal.IsAllowed`, throw `ArgumentOutOfRangeException(nameof(meal))` before reading products or calling the generator. Update the "throws only when cancelled" comment.
- Build `RecipeRequest(sent, meal)` and call `RecipeRanker.Rank(usable, meal.MaxPrepMinutes)`.
- Return `NoneWithinLimits` when `usable` is non-empty but nothing survives. Pass both counts.
- The classification log line adds the hidden-over-time count, the number of usable proposals with a null `PrepTimeMinutes` (counted in the service, whatever the limit), and the three meal parameters. The null count lets real runs show whether the AI omits times (see the brief's Open Risks). The "all hidden" information log names both hidden counts.

#### 5. Labels

**Files**: `Recipes/MealLabels.cs`, `Recipes/ScoreLabels.cs`, and a shared plural helper in `Recipes/`

**Intent**: Provide the Polish text for the servings, the caption and the time-hidden note.

**Contract**:
- Move the Polish plural rule out of `ScoreLabels` into one shared internal helper. Don't copy it. `ScoreLabels` output must not change.
- `MealLabels.ForServings(int)` returns "1 porcja", "2 porcje", "4 porcje" (and "5 porcji" by the general rule).
- `MealLabels.Summary(MealParameters)` returns "Obiad, do 30 minut, 1 porcja" for the default, and "Przekąska, bez limitu, 2 porcje" for `(Snack, null, 2)`.
- `ScoreLabels.HiddenOverTime(int count, int maxPrepMinutes)` returns, with a limit of 15:
  - "Ukryto 1 propozycję, która trwa dłużej niż 15 minut lub nie podaje czasu"
  - "Ukryto 2 propozycje, które trwają dłużej niż 15 minut lub nie podają czasu"
  - "Ukryto 5 propozycji, które trwają dłużej niż 15 minut lub nie podają czasu"

#### 6. Page compiles against the new contract

**File**: `Components/Pages/RecipeSuggestions.razor`

**Intent**: Keep Phase 1 shippable without the controls.

**Contract**:
- Call `GenerateAsync(userId!, MealParameters.Default, cts.Token)`.
- Handle `NoneWithinLimits` where `NoneWithinMissingLimit` was handled.
- Store `HiddenOverTimeCount`, and render `ScoreLabels.HiddenOverTime` under the list when it is > 0 (using `MealParameters.Default.MaxPrepMinutes` for now).
- Rewrite the none-within-limits alert now, because the time filter is live from this phase and the old copy blames only the missing limit. It reads:
  - "Żadna propozycja nie mieści się w limitach."
  - then the applicable `ScoreLabels.Hidden` and `ScoreLabels.HiddenOverTime` lines;
  - then "Spróbuj ponownie, zmień parametry albo dodaj produkty.", with the existing retry button and products link.

#### 7. Fake generator comment

**File**: `Recipes/FakeRecipeGenerator.cs`

**Intent**: Document that the recipe without a time, and the time spread 20/30/25, now demonstrate the time filter.

**Contract**: Comment only. The output stays unchanged, so the existing fake tests still hold.

#### 8. Tests

**Files**: `KitchenAssistant.Tests/Recipes/RecipeRankerTests.cs`, `RecipeServiceTests.cs`, `ScoreLabelsTests.cs`, a new `MealLabelsTests.cs`, a new `MealParametersTests.cs` (or a section of an existing models test), `FakeRecipeGeneratorTests.cs`

**Intent**: Pin the boundaries agreed in planning and the new service contract.

**Contract**: The new tests, at minimum:
- **Ranker:**
  - Time equal to the limit is kept, and limit + 1 is hidden and counted in `HiddenOverTimeCount`.
  - A null time is hidden when a limit is set.
  - With `maxPrepMinutes: null`, nothing is hidden for time, null times included.
  - A proposal over both limits counts only in `HiddenCount`.
  - The cap still applies after both filters.
- **Service:**
  - The chosen parameters reach the generator unchanged. Rewrite the default-parameters test to pass a non-default `MealParameters` and assert it arrives.
  - A disallowed value (for example servings 3, or max time 45) throws `ArgumentOutOfRangeException` without calling the generator.
  - All usable proposals over the time limit gives `NoneWithinLimits` with the right `HiddenOverTimeCount`.
  - Rename the two `NoneWithinMissingLimit` tests to the new status.
- **Labels:**
  - `ForServings` plural forms.
  - `Summary` for the default and for `(Snack, null, 2)`.
  - `HiddenOverTime` plural forms 1, 2, 5, 12 and 22.
- **Models:** `Default.IsAllowed` is true. Each out-of-list value is not allowed.
- **Fake:** ranked with the default time limit, the null-time recipe is hidden for time (`HiddenOverTimeCount == 1`, `HiddenCount == 1`).

All existing `GenerateAsync(UserA…)` calls in the tests pass `MealParameters.Default`.

### Success Criteria:

#### Automated Verification:

- Build succeeds with no new warnings: `dotnet build`
- All tests pass, including the new ranker, service, label and model tests: `dotnet test`
- The frozen system-prompt test still passes unchanged: `dotnet test --filter "FullyQualifiedName~RecipePromptTests"`

#### Manual Verification:

- With the fake generator, `/recipes` under the defaults shows two proposals, the note "Ukryto 1 propozycję, której brakuje więcej niż 2 składników", and the note "Ukryto 1 propozycję, która trwa dłużej niż 30 minut lub nie podaje czasu".

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Meal parameter controls on `/recipes`

### Overview

Add a shared, token-clean `SegmentedControl`, re-point `.btn-outline-primary` at the tokens, prove the control on `/dev/ui`, and wire three instances into the page. This phase also adds the caption.

### Changes Required:

#### 1. Token re-pointing for outline primary buttons

**File**: `wwwroot/app.css`

**Intent**: Make outline-primary buttons render from the theme tokens. The control's unchecked, checked (active), hover, focus and disabled states depend on this, and it also fixes `RecipeCard`'s toggle.

**Contract**: Add a `.btn-outline-primary` block beside `.btn-outline-danger` that mirrors it with the primary tokens: `--primary`, `--primary-foreground`, and `--primary-focus-rgb` for `--bs-btn-focus-shadow-rgb`. No literal colours.

#### 2. Shared segmented control

**File**: `Components/Ui/SegmentedControl.razor` (new; namespace `KitchenAssistant.Components.Ui`)

**Intent**: Provide a single-choice group of toggle buttons for small, fixed option sets, accessible without JS.

**Contract**:
- `@typeparam TValue`. Parameters:
  - `Name` (string, required): the radio group name and id prefix.
  - `Legend` (string, required).
  - `Options` (`IReadOnlyList<(TValue Value, string Label)>`, required).
  - `Value`, `ValueChanged` and `Disabled`.
- Markup:
  - A `<fieldset>` with a `<legend class="form-label fs-6">`, wrapped in `mb-3` the way `FormField` wraps its fields. `fs-6` is required: Bootstrap's reboot sets `legend` to float left at up to 1.5rem, and `.form-label` doesn't reset the size.
  - A wrapping row (`d-flex flex-wrap gap-2`, not `.btn-group`, so four options wrap at 375 px without broken corners).
  - For each option, an `<input type="radio" class="btn-check">` with id `{Name}-{index}`, plus a `<label class="btn btn-outline-primary">`.
- `checked` comes from comparing each option to `Value`. The change handler invokes `ValueChanged` with the option's server-side value (see Critical Implementation Details).
- Arrow keys work natively within the group.

#### 3. Kitchen-sink states

**File**: `Components/Pages/DevUi.razor`

**Intent**: Prove the new shared component's states, as CLAUDE.md requires.

**Contract**: Add a "SegmentedControl" section with three states:
- Default first option selected (meal types).
- A non-first option selected (time limits, "bez limitu").
- Disabled (servings).

#### 4. Page wiring, caption and alert

**File**: `Components/Pages/RecipeSuggestions.razor`

**Intent**: Let the user choose the parameters, send them, and show which parameters produced the list.

**Contract**:
- State: `MealParameters selected = MealParameters.Default` and `MealParameters? generatedFor`.
- Three `SegmentedControl`s render above the generate button, with options built from the `MealParameters.*Options` lists and labelled by:
  - `MealLabels.For`;
  - `MealLabels.ForMaxPrepTime`;
  - the servings number.
- Each control updates `selected` with a `with` expression. They are disabled while `generating`.
- `GenerateAsync` captures `generatedFor = selected` when the click starts, then passes it to the service.
- When proposals or the none-within-limits alert are shown, render "Propozycje dla: " + `MealLabels.Summary(generatedFor)` above them as a `text-body-secondary` paragraph.
- The note under the list and the alert's time line (both rewritten in Phase 1) use `generatedFor.MaxPrepMinutes`.

### Success Criteria:

#### Automated Verification:

- Build succeeds with no new warnings: `dotnet build`
- All tests pass: `dotnet test`
- The literal-colour scan reports nothing in the changed views: `grep -nE '#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|oklch\(|:\s*(white|black)\b|navbar-(dark|light)' Components/Ui/SegmentedControl.razor Components/Pages/RecipeSuggestions.razor Components/Pages/DevUi.razor`

#### Manual Verification:

- `/dev/ui` shows the three SegmentedControl states in theme colours (no Bootstrap blue). The checked option is filled with the primary colour, the disabled group is dimmed, and each legend matches a `FormField` label in size.
- On `/recipes`, Obiad / do 30 minut / 1 are preselected on every load. Tab reaches each group, the arrow keys move the selection, and the focus ring is visible.
- With the fake generator and "do 15 minut", the none-within-limits alert appears with both notes and the caption "Propozycje dla: Obiad, do 15 minut, 1 porcja". With "bez limitu", three proposals appear.
- After a successful generation, changing a control leaves the list and its caption unchanged until the next click.
- At 375 px wide, the groups wrap with no horizontal scroll.
- With the Anthropic generator, "Śniadanie, do 15 minut, 2 porcje" returns breakfast-style recipes, each stating ≤ 15 minutes, with amounts for two (eyeball check).

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding.

---

## Testing Strategy

### Unit Tests:

- Ranker time boundaries: equal is kept, +1 is hidden, null is hidden under a limit, and nothing is hidden for time without a limit. A proposal over both limits is counted once, as over-missing.
- Service: parameters pass through, disallowed values are rejected before any I/O, `NoneWithinLimits` comes with both counts, and the existing deadline and cancellation tests run with `MealParameters.Default`.
- Labels: Polish plurals for servings and the time-hidden note, plus the summary line.

### Integration Tests:

- The existing SQLite-backed `RecipeServiceTests` cover the whole service path. There's no UI automation in this slice.

### Manual Testing Steps:

1. Fake generator, defaults: two proposals and both hidden notes, captioned "Propozycje dla: Obiad, do 30 minut, 1 porcja".
2. Fake generator, "do 15 minut": the none-within-limits alert, showing 1 hidden for missing and 3 hidden for time.
3. Fake generator, "bez limitu": three proposals and only the missing note.
4. Change a control after results appear: the list and caption stay. Click retry or generate: the caption updates.
5. Keyboard only: Tab, arrow keys, Space/Enter on the generate button.
6. 375 px viewport: no horizontal scroll.
7. One real AI run with non-default parameters.

## Performance Considerations

None. The prompt gains no tokens beyond the parameter values it already carries, and the filter is an in-memory pass over at most a handful of proposals.

## Migration Notes

No schema change. `RecipeGenerationStatus.NoneWithinMissingLimit` is renamed in code only. Nothing persists it.

## References

- PRD: `context/foundation/prd.md` (FR-004, US-01, Business Logic: the parameter set and defaults)
- Roadmap: `context/foundation/roadmap.md` (S-05)
- Prior slices: `context/archive/2026-10-03-first-recipe-generation/plan.md` (`MealParameters` contract), `context/archive/2026-10-08-recipe-ranking/plan.md` (filter, count and status pattern), `context/archive/2026-10-09-recipe-details/plan.md` (manual UI verification, no bUnit)
- UI research: `context/changes/recipes-ui/research.md` §5 (SegmentedControl via `.btn-check`)
- Similar implementation: `Recipes/RecipeRanker.cs:11-24`, `wwwroot/app.css:278-290` (`.btn-outline-danger` re-pointing)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Parameters through the service, time limit in the ranking

#### Automated

- [x] 1.1 Build succeeds with no new warnings: `dotnet build` — 869caeb
- [x] 1.2 All tests pass, including the new ranker, service, label and model tests: `dotnet test` — 869caeb
- [x] 1.3 The frozen system-prompt test still passes unchanged: `dotnet test --filter "FullyQualifiedName~RecipePromptTests"` — 869caeb

#### Manual

- [x] 1.4 With the fake generator, `/recipes` under the defaults shows two proposals, the note "Ukryto 1 propozycję, której brakuje więcej niż 2 składników", and the note "Ukryto 1 propozycję, która trwa dłużej niż 30 minut lub nie podaje czasu". — 869caeb

### Phase 2: Meal parameter controls on `/recipes`

#### Automated

- [x] 2.1 Build succeeds with no new warnings: `dotnet build` — e7699eb
- [x] 2.2 All tests pass: `dotnet test` — e7699eb
- [x] 2.3 The literal-colour scan reports nothing in the changed views — e7699eb

#### Manual

- [x] 2.4 `/dev/ui` shows the three SegmentedControl states in theme colours, with the checked option filled, the disabled group dimmed and legends at `FormField` label size — e7699eb
- [x] 2.5 On `/recipes`, Obiad / do 30 minut / 1 are preselected on every load; Tab, arrow keys and the focus ring work — e7699eb
- [x] 2.6 Fake generator: "do 15 minut" shows the none-within-limits alert with both notes and the caption; "bez limitu" shows three proposals — e7699eb
- [x] 2.7 Changing a control after a generation leaves the list and caption unchanged until the next click — e7699eb
- [x] 2.8 At 375 px wide, the groups wrap with no horizontal scroll — e7699eb
- [x] 2.9 Anthropic generator, "Śniadanie, do 15 minut, 2 porcje": breakfast-style recipes within 15 minutes, amounts for two — e7699eb
