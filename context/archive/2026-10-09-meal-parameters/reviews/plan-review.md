<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Meal Parameters (S-05)

- **Plan**: context/changes/meal-parameters/plan.md
- **Mode**: Deep
- **Date**: 2026-10-09
- **Verdict**: SOUND
- **Findings**: 0 critical, 2 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | WARNING |
| Blind Spots | WARNING |
| Plan Completeness | WARNING |

## Grounding
10/10 paths ✓, 7/7 symbols ✓, brief↔plan ✓, PRD parameter set ✓. The fake generator's expectations hold: under the defaults, 2 are shown, 1 is hidden for missing and 1 for time; "do 15 minut" hides 1 for missing and 3 for time; "bez limitu" shows 3. `.btn-outline-primary` is used only by `RecipeCard`, and `--primary-focus-rgb` exists.

## Findings

### F1 — `<legend class="form-label">` renders as a heading

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architectural Fitness
- **Location**: Phase 2 §2 — Shared segmented control
- **Detail**: Bootstrap's reboot styles every `legend` with float:left, width:100% and font-size calc(1.275rem + .3vw), which is 1.5rem on wide screens. `.form-label` only sets margin-bottom, and app.css has no legend override, so the group titles would render at heading size, unlike FormField's 1rem labels.
- **Fix**: Use `<legend class="form-label fs-6">`, and add the legend size to manual check 2.4.
- **Decision**: FIXED

### F2 — Phase 1 leaves the "all hidden" alert blaming only the missing limit

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 1 §6
- **Detail**: Phase 1 turns on the 30-minute filter and is called shippable, but the alert kept "…w limicie 2 brakujących składników". When the real AI returns a batch that is all over 30 minutes or has no times, the alert names the wrong cause, and the fake generator never hits this under the defaults.
- **Fix**: Move the alert rewrite into Phase 1 §6. Phase 2 adds only the caption.
- **Decision**: FIXED

### F3 — The null-time risk can't be measured from the logs

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 1 §4 — classification log line
- **Detail**: The brief's main open risk is the AI omitting `prepTimeMinutes`. The planned log merges "over the limit" with "no time", so real runs can't show whether the deferred schema or prompt change is needed.
- **Fix**: Also log the number of usable proposals with a null `PrepTimeMinutes` in the classification line.
- **Decision**: FIXED

### F4 — The index-based ids invite the Razor for-loop closure bug

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 2 §2 + Critical Implementation Details
- **Detail**: Ids `{Name}-{index}` invite a `for (var i…)` loop. An `@onchange` lambda that captures `i` sees the value `i` has after the loop, which breaks the "server-captured value" guarantee.
- **Fix**: Add "iterate with `foreach`, or copy to a local before the lambda" to Critical Implementation Details.
- **Decision**: FIXED
