# Meal Parameters (S-05) — Plan Brief

> Full plan: `context/changes/meal-parameters/plan.md`

## What & Why

Before asking for recipes, the user picks the meal type, the maximum preparation time and the number of servings from the PRD's small set (FR-004). The proposals respect those choices. This is the last unbuilt part of the US-01 flow ("prosi o przepisy z wprowadzonymi parametrami").

## Starting Point

S-02 already defined `MealParameters` with the PRD defaults and renders them into the AI prompt. `RecipeService` hardcodes those defaults, and `/recipes` has no controls. `RecipeRanker` filters by the missing limit and counts what it hides. Nothing checks the time limit.

## Desired End State

`/recipes` shows three groups of toggle buttons. Each page load starts at Obiad, do 30 minut, 1 porcja. The chosen values go to the AI. The app hides any proposal whose stated time is over the limit, or that states no time while a limit is set, and a note counts the hidden ones. The caption "Propozycje dla: Obiad, do 30 minut, 1 porcja" names the parameters the list came from, and it stays put when the user changes a control afterwards.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| What "respects" means for time | App hides proposals with time > limit **and** with no time while a limit is set; a time equal to the limit is kept; "bez limitu" hides nothing for time | The user gets a hard guarantee checked in code, following the `RecipeRanker` filter-and-count pattern. |
| Meal type and servings | Prompt only, no app check | Neither can be verified from the AI's output. |
| System prompt | Unchanged, so the frozen "as measured" test stays | The parameters already travel in the user message, and the app-side filter enforces the time limit. |
| Controls | Shared `SegmentedControl` (radio `.btn-check` toggle buttons) in `Components/Ui/`, shown on `/dev/ui` | All options are visible, each takes one click, and no JS is needed; this matches the `recipes-ui` research. |
| Remembering choices | None; every page load starts at the PRD defaults | No storage or migration; fits the PRD defaults and the "no history" non-goal. |
| List after a control change | List kept, captioned with the parameters that produced it | No paid generation is lost to a misclick, and the user can see which settings applied. |
| Allowed values | Defined once on `MealParameters`; the UI renders from them and the service rejects anything else | One definition of the "small, defined set". |
| Status naming | `NoneWithinMissingLimit` → `NoneWithinLimits`, plus `HiddenOverTimeCount` | "All hidden" can now have either cause, or both. |
| Over both limits | Counted once, as over-missing | Existing `HiddenCount` semantics and tests stay intact. |

## Scope

**In scope:**
- Allowed option lists and validation on `MealParameters`.
- `GenerateAsync(userId, meal, ct)`.
- Time filter and count in `RecipeRanker`.
- Polish labels: servings, summary, time-hidden note.
- `SegmentedControl` and its `/dev/ui` states.
- `.btn-outline-primary` token re-pointing.
- Page wiring, caption and the reworded none-within-limits alert.
- Unit tests.

**Out of scope:**
- System prompt or schema changes.
- App checks of meal type or servings.
- Remembering parameters.
- A diet parameter.
- Clearing the list on control change.
- bUnit or browser tests.
- `ActionAlert` extraction.
- Ranking order changes.

## Architecture / Approach

The page holds `selected` (bound to the three controls) and snapshots it into `generatedFor` on click. `RecipeService.GenerateAsync(userId, generatedFor)` validates it, sends it in `RecipeRequest.Meal` to the unchanged prompt, and calls `RecipeRanker.Rank(usable, meal.MaxPrepMinutes)`. The ranker filters by missing first, then by time, sorts, caps, and returns both hidden counts. The page renders the proposals, both notes and the caption, built from `MealLabels.Summary(generatedFor)`.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Parameters through the service, time limit in the ranking | Validated service contract, time filter with boundary tests, labels; the page passes the defaults and shows the reworded none-within-limits alert | The strict null rule hides recipes when the AI omits a time, even under the defaults |
| 2. Meal parameter controls on `/recipes` | `SegmentedControl` and tokens, `/dev/ui` states, page wiring, caption | Wrapping and focus states at 375 px; `.btn-outline-primary` re-pointing changes `RecipeCard`'s toggle colour |

**Prerequisites:** S-02 (done). S-03 and S-04 are merged, so there is no merge-order conflict on `/recipes`. A local AI key is needed only for the one real-run check.
**Estimated effort:** ~1–2 sessions across 2 phases.

## Open Risks & Assumptions

- The strict rule hides null-time proposals, and the schema still lets the AI return no time. If real runs often omit it, empty results may become common. The fix would be a schema or prompt change, which this slice deliberately excludes because the prompt is frozen as the measured baseline.
- Servings and meal type are trusted to the prompt; a wrong-sized recipe is not caught.
- The AI's stated time is an estimate; the filter checks what the AI states, not the real cooking time.

## Success Criteria (Summary)

- On `/recipes`, a user can change any of the three parameters and get proposals generated for exactly those values, with no shown proposal exceeding the chosen time limit.
- The list always says which parameters produced it, and the hidden-proposal notes explain any shortfall.
- `dotnet test` covers the time boundaries, the validation and the Polish labels.
