# Recipe details (S-04) — Plan Brief

> Full plan: `context/changes/recipe-details/plan.md`

## What & Why

FR-006 says the user can open the details of a chosen recipe. Today `/recipes` prints every proposal fully expanded, which makes the ranked list long and hard to compare. S-04 turns each proposal into a compact card that opens in place, so the user chooses from the list first and then reads one full recipe in a consistent structure.

## Starting Point

Each proposal is an inline `article.card` in `RecipeSuggestions.razor` that shows the title, score, summary, time, all ingredients with badges, and all steps. The page already holds the full `RecipeProposal` and its code-computed score, and proposals live only in page memory.

## Desired End State

After generating, the user sees up to 5 ranked cards, all collapsed. Each shows the title, score, summary, „ok. N min” and „Do kupienia: …” (only when something is missing). „Pokaż przepis” expands that card's ingredients (in the AI's order, with badges) and its steps instantly, without an AI call, and several cards can be open at once. A new generation starts collapsed, and a refresh clears the list, as today.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Persistence | Current page only (as today) | No entity, migration or stale-score problem; matches the PRD's no-history non-goal and the deadline |
| How details open | Expand in place with a „Pokaż przepis” toggle (Blazor state) | Keeps the ranked list visible and needs no route, persistence or Bootstrap JS |
| Collapsed card | Title, score, summary, time + „Do kupienia: …” | The user can judge the shopping without opening every card |
| „Do kupienia” names | Every missing line in order, no dedupe; line hidden when 0 | Always matches the „Brakuje: N” count (S-03 counts lines) |
| Ingredient order in details | AI order with Masz / Zawsze w domu / Brakuje badges | Reads like a recipe and matches the steps |
| Extraction | `RecipeCard` + `IngredientStatusBadge` in `Components/Ui/`, on `/dev/ui`; `ActionAlert` (F2) deferred | Proves both card states; S-04 adds no alerts |
| Default state | All cards collapsed; `InitiallyExpanded` parameter only for `/dev/ui` | `/dev/ui` is static SSR and can't click the toggle |

## Scope

**In scope:** `ScoreLabels` „Do kupienia” label and its unit tests; `IngredientStatusBadge` and `RecipeCard` components; `/dev/ui` states; `/recipes` switched to `RecipeCard`.

**Out of scope:**
- Persistence (database, session storage), a `/recipes/{id}` route or a same-page detail view
- A new AI call or a prompt or schema change
- Grouping ingredients by status
- `ActionAlert` (F2), bUnit, S-05 meal parameters, ingredient dedupe

## Architecture / Approach

`RecipeSuggestions` (InteractiveServer) keeps generating and ranking exactly as now, and renders one `RecipeCard` per ranked `RecipeProposal`. `RecipeCard` owns a private `expanded` flag (seeded from `InitiallyExpanded`). It always renders the summary block, and renders the ingredients (`IngredientStatusBadge`) and steps only while expanded. The „Do kupienia” text comes from `ScoreLabels`, built from the classifier's `Missing` statuses, never from the AI. Because the page clears `proposals` before each generation, every new batch mounts fresh, collapsed cards.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Shared recipe card | Label + tests, `IngredientStatusBadge`, `RecipeCard`, `/dev/ui` states | The toggle can't be clicked on static `/dev/ui`; states are shown through a parameter |
| 2. Details on /recipes | Page renders collapsed `RecipeCard`s; inline card and badge removed | Regressing the page's other states (failure, all over limit, hidden note) |

**Prerequisites:** S-02 and S-03 done (they are). Everything runs on the fake generator, with no API key or spend.
**Estimated effort:** ~1 after-hours session across 2 phases.

## Open Risks & Assumptions

- If the S-07 spike later moves generation to „list first, full recipe on open”, the expanded section would need its own AI call and the ≤ 1 min check. Reading the existing `RecipeProposal` keeps that rework contained in `RecipeCard`.
- An accidental refresh still loses the batch and needs a new (paid) generation. This was accepted to keep S-04 small.
- S-05 edits the same page. Whichever change lands second resolves the merge.

## Success Criteria (Summary)

- The user scans compact, ranked cards and sees at a glance what each needs from the shop.
- Opening a card shows the full recipe (ingredients with status, numbered steps) instantly, in the same structure for every proposal.
- Existing page states and the code-computed score are unchanged.
