# Review follow-ups: recipe-ranking

Source: `context/changes/recipe-ranking/reviews/impl-review.md` (2026-10-08)

## F2: Extract a shared "alert with action" component

- **What**: Four inline copies of an alert with a retry or link action exist:
  - `Components/Pages/RecipeSuggestions.razor:22` (load error)
  - `Components/Pages/RecipeSuggestions.razor:47-50` (generation failure)
  - `Components/Pages/RecipeSuggestions.razor:55-62` (all proposals over the missing limit, added in S-03)
  - `Components/Pages/Products.razor:76`
  
  Their layouts differ: `d-flex` + `ms-3` vs. a `<p>` + wrapping row with `gap-3`.
- **Do**: Extract `ActionAlert` (or `ErrorState`) into `Components/Ui/`. Give it a kind parameter (danger/warning), a message and an action `RenderFragment`. Add its states to `/dev/ui` and replace all copies.
- **Why deferred**: S-03's scope excluded component extraction. The item is already tracked in `context/changes/recipes-ui/research.md` §5 (line 162).
- **Suggested path**: its own small change via `/10x-new`, or fold it into S-04 when the details view adds more alerts.
