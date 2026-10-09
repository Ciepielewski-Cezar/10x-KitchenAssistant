---
change_id: meal-parameters
title: Meal parameters (S-05)
status: implemented
created: 2026-10-09
updated: 2026-10-09
archived_at: null
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

- Roadmap slice S-05 (FR-004, US-01, Business Logic). Planned 2026-10-09 without a research doc: the backend types (`MealParameters`, `RecipeRequest.Meal`, `MealLabels`, prompt rendering) already landed in S-02.
- "Proposals respect the parameters" was settled in planning: the app hides a proposal whose stated time exceeds the chosen limit, or that states no time while a limit is set. Meal type and servings rest on the prompt.
