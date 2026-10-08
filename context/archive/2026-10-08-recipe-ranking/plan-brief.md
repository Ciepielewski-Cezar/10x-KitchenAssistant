# Recipe ranking (S-03) — Plan Brief

> Full plan: `context/changes/recipe-ranking/plan.md`

## What & Why

Users should see the AI's recipe proposals ordered by the app's own score: no-shopping recipes that use up „zużyj w pierwszej kolejności” products first, then those with 1–2 missing ingredients. Each proposal shows „Masz X z Y składników” and its missing count, and proposals with more than 2 missing ingredients are hidden. The PRD and CLAUDE.md require these values to be computed in C#, never taken from the AI.

## Starting Point

S-02 already classifies every ingredient in C# as owned (with product ID and category), always-at-home or missing. Proposals are shown in the AI's order, capped at 5, with no score and no ≤ 2-missing filter. Zero usable proposals shows the generic failure.

## Desired End State

`/recipes` lists up to 5 proposals ranked by missing ↑, use-first products ↓, owned ↓, AI order. Each card carries a `RecipeScoreSummary` („Masz X z Y składników”, „Bez zakupów” / „Brakuje: N”, „Zużywa N produktów do szybkiego zużycia”). A note says how many proposals were hidden, and a dedicated message with retry replaces the generic error when all of them were.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Sort key | Missing ↑ → distinct use-first products ↓ → owned (X) ↓ → AI order | Same key for both PRD groups, follows the PRD's two criteria literally, deterministic | Plan |
| Counting | X/Y and missing count ingredient lines as shown; use-first counts distinct product IDs; Y excludes always-at-home | Numbers match the card; the PRD says „ile produktów” for use-first | Plan |
| Y = 0 recipe | Dropped as unusable in classification, not counted as hidden | A water-and-salt recipe must not top the list with „Masz 0 z 0” | Plan |
| Filter and cap | Filter > 2 missing, then sort, then cap at 5 (cap moves out of the classifier) | Capping first would throw away valid proposals | Plan |
| Empty result | New `NoneWithinMissingLimit` status with its own message + retry; „Ukryto N propozycji…” note when some are hidden | The user knows generation worked and why the list is short | Plan |
| AI request size | Keep up to 5, no prompt change; limit 2 interpolated from the shared constant with identical text | Keeps S-07's latency measurement valid and costs nothing | Plan |
| Score UI | Shared `Components/Ui/RecipeScoreSummary.razor` with badges, proved on `/dev/ui` | Reusable by S-04; follows the CLAUDE.md shared-component rule | Plan |
| Component name | `RecipeScoreSummary`, not `RecipeScore` | Avoids a type-name clash with the `RecipeScore` record under the global `Components.Ui` import | Plan |

## Scope

**In scope:** `RecipeScore` record and factory, classifier change (score, Y = 0 drop, no cap), new `RecipeRanker`, new result status and hidden count, prompt constant, `ScoreLabels` with Polish plurals, fake 4th recipe, unit tests, `RecipeScoreSummary` component, `/dev/ui` states, page wiring.

**Out of scope:** prompt/schema changes or over-asking the AI, a progress-bar meter, a full `RecipeCard` extraction, deduplicating ingredient lines, persistence and details (S-04), meal-parameter UI (S-05), ownership-matching changes.

## Architecture / Approach

`RecipeService` → generator → parser → `RecipeClassifier` (status per ingredient + `RecipeScore`, all usable recipes) → `RecipeRanker.Rank` (filter > `MaxMissing`, stable sort, cap `MaxProposals`) → `RecipeGenerationResult(Status, Proposals, HiddenCount)`. The page renders `RecipeScoreSummary` per card, plus the hidden note and the all-hidden state. Text and plurals come from `ScoreLabels`.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Score, filter and ranking in C# | Score model, ranker, service statuses, labels, fake 4th recipe, unit tests | Moving the cap changes existing classifier tests; ordering of the two empty checks |
| 2. Score on the page | `RecipeScoreSummary`, `/dev/ui` states, ranked cards, hidden note, all-hidden message | Polish plural text and the narrow-width layout |

**Prerequisites:** S-02 done (it is). No API key or spend needed; everything runs on the fake generator.
**Estimated effort:** ~2 after-hours sessions (one per phase).

## Open Risks & Assumptions

- With the 5-recipe request unchanged, a user may see fewer than 5 proposals when the AI ignores the 2-missing limit. S-07's real calls show how often this happens.
- An AI-named spice outside the always-at-home list still counts as missing (S-02 behaviour), which can push a recipe over the limit.
- S-05 touches the same page and service; merge order is set in whichever change lands second.

## Success Criteria (Summary)

- Proposals appear in the agreed order, and none has more than 2 missing ingredients.
- Every card's „Masz X z Y” and missing count match its ingredient badges, all computed in C#.
- The user is told when proposals were hidden and gets a clear retry path when all of them were.
