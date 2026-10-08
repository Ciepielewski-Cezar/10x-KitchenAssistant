<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: First recipe generation (S-02)

- **Plan**: context/changes/first-recipe-generation/plan.md
- **Scope**: Phase 3 of 3 (partial: only 3.6 is done; 3.1–3.5 are deferred)
- **Reviewed phases**: 3
- **Date**: 2026-10-05
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 1 warning, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

Commits reviewed: ed3eae8 (spike prep, classification stats) and 3c24ae6 (review fixes, smoke test).

- **3.1**: `dotnet test KitchenAssistant.slnx` passes 111/111. Tests ran with `--artifacts-path`, because a running KitchenAssistant process (PID 36048) locks `bin/`.
- **3.6** has evidence: `spike.md` has the smoke-test log lines (14.5 s, `end_turn`, 5 proposals, 20/20 valid IDs).
- **Secrets**: `.env` is git-ignored and was never committed, and there is no `sk-ant` in history.
- **Scope**: the Development `Recipes:Generator` switch was not committed, as the plan asks.

## Findings

### F1 — Spike deferred while it still gates S-04's shape

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Plan Adherence
- **Location**: plan.md Progress 3.1–3.5; spike.md "Status"; roadmap.md S-02 Unknowns
- **Detail**: Phase 3 exists to measure at least 10 real calls and to "decide the defaults and S-04's shape". Only the smoke test ran (one call). The plan's „przed MVP” deferral is recorded in spike.md, the 3c24ae6 commit message and a preliminary roadmap note. But the roadmap gives no gate that stops S-04 (recipe details: one AI call or a split) from being planned without the verdict. The S-02 unknown still reads "Block: no". The change cannot be archived while 3.1–3.5 are open. The cost of the spike is small: about $0.025 per call (smoke test), so about $0.28 for 11 calls. The runbook and the pantry are ready.
- **Fix A ⭐ Recommended**: Run the 10-call spike now, following the spike.md runbook, then close 3.1–3.5.
  - Strength: It costs about $0.30 and roughly 15 minutes. It closes S-02, answers the roadmap unknown with evidence, and gives S-04 a real input.
  - Tradeoff: It needs your time and the dev key now, plus a VPN check.
  - Confidence: HIGH — the smoke test already showed the path works end to end, at 14.5 s.
  - Blind spot: Latency varies from call to call; one data point says little about the tail.
- **Fix B**: Keep the deferral but make it a gate. In roadmap S-02 Unknowns, state that `/10x-plan` for S-04 must read the spike verdict first. Add a dated „deferred until” note above Phase 3's Progress block.
  - Strength: Keeps your schedule and stops S-04 from being planned blind.
  - Tradeoff: S-02 stays `implementing` and is not archivable, and the risk the roadmap put first stays partly open.
  - Confidence: MED — it relies on the note being read at S-04 planning time.
  - Blind spot: S-03/S-05 might also assume the one-call approach.
- **Decision**: SKIPPED

### F2 — Progress 3.6 has no commit SHA

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: context/changes/first-recipe-generation/plan.md:432
- **Detail**: The Progress convention is "append ` — <commit sha>` when a step lands". Row 3.6 is `[x]` but has no SHA. The smoke test result landed in 3c24ae6.
- **Fix**: Append ` — 3c24ae6` to row 3.6.
- **Decision**: FIXED

### F3 — MaxProducts comment overstates what the cap protects

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Recipes/RecipeService.cs:29; plan.md Implementation Notes ("Prompt size cap")
- **Detail**: The comment says "only stored products drop off a large pantry". `all.Take(MaxProducts)` also cuts „Do zużycia” products when there are more of them than the cap. Those are sorted by name, not by expiry, so the products closest to expiring can be among those left out. There is no test for the case where the UseFirst count exceeds `MaxProducts`. There is also no test showing that a product past the cap is still matched by name.
- **Fix**: Reword the comment and the addendum to "UseFirst is sent first; anything past the cap is left out". Add both missing tests.
- **Decision**: FIXED — comment and addendum reworded; tests `More_use_first_products_than_MaxProducts_cuts_use_first_too` and `A_product_left_out_by_MaxProducts_is_still_owned_by_name`

### F4 — Log and comment wording inaccurate on edge paths

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Recipes/AnthropicRecipeGenerator.cs:33,35; Recipes/RecipeService.cs:58
- **Detail**: Three strings say slightly more than is true:
  1. When the user leaves the page, the only trace is "Recipe generation call failed: … TaskCanceledException". RecipeService and the page log nothing in that case.
  2. "RecipeService logs the exception itself" holds only for the three handled exception types. Anything else is logged by the page's generic catch.
  3. The unknown-ID warning says "they were not treated as owned", but since F3 of the first review it also counts IDs from dropped and over-cap recipes, which were never classified.
- **Fix**:
  1. Reword the generator line to "Recipe generation call ended without a response: …".
  2. Change the comment to "the caller logs the exception".
  3. Shorten the warning to "… product IDs not on the user's list."
- **Decision**: FIXED — all three reworded; the spike.md runbook line now matches the new log text

### F5 — `|` → `/` normalization can defeat the exact-name fallback

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Recipes/RecipePrompt.cs:55 vs Recipes/RecipeClassifier.cs:128
- **Detail**: For a product named `a|b`, the prompt shows `a/b`. If the AI returns that name with a null ID, the fallback compares it to the stored `a|b`, finds no match, and the ingredient becomes Missing. This needs three things at once: a `|` in the name, the AI omitting an ID it was given, and the AI echoing the changed name. The smoke test had 20/20 valid IDs. The same mismatch already existed for line breaks, which `OneLine` also changes.
- **Fix**: Accept it, and record it in plan.md's Implementation Notes as a known limitation.
- **Decision**: FIXED — recorded as an accepted known limitation in plan.md Implementation Notes

## Triage summary

- Fixed: F2, F3, F4, F5 (4)
- Skipped: F1 (1). The 10-call spike stays deferred; Progress 3.1–3.5 stay open.
- After the fixes: `dotnet test KitchenAssistant.slnx` passes 113 of 113 tests.
