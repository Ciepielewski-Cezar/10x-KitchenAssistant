# Pantry: edit and remove products (S-06) — Plan Brief

> Full plan: `context/changes/pantry-edit-remove/plan.md`

## What & Why

A signed-in user can change any field of a product on their list or delete it, right in the product's row on `/products`. This is roadmap slice S-06 (FR-003): the PRD keeps it in the MVP because the product list goes stale quickly, and recipes are only as good as that list.

## Starting Point

S-01 delivered `/products` with an add form and two read-only sections, backed by `ProductService` (list + add, per-user filter, duplicate rule on `(UserId, Category, NormalizedName)` with a race fallback) and in-memory SQLite service tests. Nothing can be changed or removed after adding.

## Desired End State

Each row has „Zmień” and „Usuń”. „Zmień” turns the row into a pre-filled form with all five fields; saving re-sorts the product into its (possibly new) section with the expiry flag recomputed. „Usuń” asks „Usunąć „{name}”?” inline before deleting. Only one row is in edit or confirm state at a time. Colliding edits are rejected with the add form's duplicate message, a product already deleted in another tab yields „Ten produkt został już usunięty.”, and nobody can touch another user's product.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Edit UX | Inline in the row, one row at a time, „Zapisz” / „Anuluj” | Edits happen where the product is, so it works on a phone without scrolling to a distant form. |
| Delete confirmation | Inline in the row: „Usunąć „{name}”?” „Usuń” / „Anuluj” | Pure Blazor state, no JS (Bootstrap JS isn't loaded), and it can't target the wrong row. |
| Editable fields | All five: name, category, quantity, expiry date, storage location | Quantity and expiry go stale fastest; optional fields can be cleared. |
| Duplicate on edit | Reject with the add message; a case-only rename of the same product is allowed | One rule and one message for add and edit, with the unique index as the guarantee. |
| Missing / foreign product | `NotFound` for another user's ID and for a deleted one; NotFound wins over Duplicate | Privacy: a foreign ID is indistinguishable from a missing one. |
| Concurrency | Last save wins; no row version | Per-user data, a handful of users; a stale delete is handled by `NotFound`. |
| Shared fields | `ProductFields` component in `Components/Pages/`, keyed by an ID prefix | Add and edit render identically without colliding element IDs; product-specific, so not in `Components/Ui/`. |
| Buttons | `btn-link`, `btn-outline-danger`, `btn-danger`, `btn-primary` (all `btn-sm`) | Only these variants are re-pointed to tokens in `app.css`. |
| Tests | Extend `ProductServiceTests` (SQLite, fake clock, race hook); UI checked manually | Same proven setup as S-01; no bUnit in the project. |

## Scope

**In scope:**
- `UpdateProductAsync` and `DeleteProductAsync` in `ProductService`, sharing validation/normalisation with add
- Service tests for update, delete, duplicates, privacy, races and validation
- `ProductFields` component used by the add form and the edit row
- Row actions, inline edit form and inline delete confirmation on `/products`

**Out of scope:**
- Merging duplicates, optimistic concurrency, undo, bulk delete
- A separate edit page, a modal, or any JS interop
- Refreshing recipe proposals already on screen
- Any migration (the entity is unchanged)

## Architecture / Approach

`Products.razor` holds `editingId` / `confirmingDeleteId` as page state and calls `ProductService`, never the DbContext. `UpdateProductAsync(userId, productId, form)` returns `Updated | Duplicate | NotFound` and `DeleteProductAsync(userId, productId)` returns `Deleted | NotFound`; both match the product by `Id` and `UserId`. Update reuses add's validation helper, checks duplicates excluding the product itself, and re-checks after a unique-index `DbUpdateException` exactly like add.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Service and tests | Update/delete methods with duplicate, privacy and race handling; xUnit coverage | Ordering so that `NotFound` beats `Duplicate`, and a product deleted mid-update doesn't throw |
| 2. Inline edit and delete on `/products` | `ProductFields`, row actions, inline edit and delete confirm, Polish messages | Duplicate element IDs between the add form and an edit row; regressing the add flow |

**Prerequisites:** S-01 merged (done); LocalDB and `dotnet tool restore` for local runs.
**Estimated effort:** about 2 sessions across 2 phases.

## Open Risks & Assumptions

- Assumes the per-user list stays small (dozens of rows), so reloading the whole list after each change is fine.
- Recipe proposals shown before a delete may still list the deleted product until the next generation; accepted.

## Success Criteria (Summary)

- A user fixes a product's name, category, quantity, expiry date or location in place, and deletes products after a one-click confirmation.
- Edits that would create a duplicate are rejected with a clear Polish message; nothing ever touches another user's products.
- `dotnet test` proves update/delete rules, privacy and the race fallback; the add flow behaves as before.
