# Pantry: edit and remove products (S-06) Implementation Plan

## Overview

A signed-in user can change any field of a product on their list (name, category, quantity, expiry date, storage location) or delete it, directly in the product's row on `/products`. This is roadmap slice S-06 (FR-003). The PRD keeps it in the MVP because the product list goes stale quickly: today the only fix for a wrong quantity or a used-up product is nothing at all.

## Current State Analysis

- `ProductService` (`Pantry/ProductService.cs:25`) is the only place products are read or written. It has `GetProductsAsync` and `AddProductAsync` and nothing else. Every method takes `userId` explicitly and filters by it.
- The add path (`Pantry/ProductService.cs:59-112`) validates and normalises input (trimmed name 1–100 chars, defined category, whitespace-only quantity → `null`, quantity ≤ 50 chars, `NormalizedName = name.ToUpperInvariant()`), pre-checks duplicates with `ExistsAsync`, and on `DbUpdateException` re-checks so a lost race into the unique index returns `Duplicate`.
- The duplicate rule is enforced by the unique index `(UserId, Category, NormalizedName)` (`Data/ApplicationDbContext.cs:34`). An edit that renames a product or moves it to the other category can hit that index.
- `Products.razor` (`Components/Pages/Products.razor`) is `InteractiveServer` + `[Authorize]`. It has one add form whose five fields are inline markup (`:21-56`) with fixed element IDs (`product-name`, `category-use-first`, …) and a fixed radio-group name. List rows (`:104-127`) are display-only `list-group-item`s.
- Bootstrap JS is not loaded (`Components/App.razor`), so no `data-bs-*` modal or collapse.
- `app.css` re-points `.btn-primary`, `.btn-danger`, `.btn-outline-danger` and `.btn-link` to tokens (`wwwroot/app.css:245-292`); `.btn-secondary` / `.btn-outline-secondary` are not re-pointed and would render Bootstrap's literal grey.
- Recipe generation (`Recipes/RecipeService.cs:22`) re-reads the product list on each request and links ingredients to products by ID. Deleting a product only makes proposals already on screen stale; no schema or cascade work is needed.
- Tests (`KitchenAssistant.Tests/ProductServiceTests.cs`) run `ProductService` against in-memory SQLite with `FakeTimeProvider`, and the `ConnectionDbContextFactory.BeforeCreate` hook injects a rival row before the nth context to simulate a race (`:178-197`).

## Desired End State

On `/products`, every row has „Zmień” and „Usuń” buttons. „Zmień” turns that row into a form with all five fields pre-filled; „Zapisz” saves and the product shows in its (possibly new) section, re-sorted and with the expiry flag recomputed; „Anuluj” discards. „Usuń” replaces the row's buttons with an inline confirmation „Usunąć „{name}”?” with „Usuń” / „Anuluj”; confirming removes the product. Only one row is in edit or confirm state at a time. An edit that would collide with another product in the target category is rejected with the add form's duplicate message. A product deleted elsewhere (another tab) yields a short message and a refreshed list. Nobody can edit or delete another user's product.

Verify with `dotnet test KitchenAssistant.slnx` (service rules) and the Phase 2 manual walkthrough (UI).

### Key Discoveries:

- Normalisation and validation in `AddProductAsync` (`Pantry/ProductService.cs:62-79`) must be shared with update, not copied.
- `ExistsAsync` (`Pantry/ProductService.cs:116-121`) needs to be able to exclude the product being edited, so that a case-only rename („mleko” → „Mleko”) of the same product is not a duplicate of itself.
- The race-fallback pattern (`Pantry/ProductService.cs:102-111`) and its test (`ProductServiceTests.cs:178-197`) are the templates for update.
- Fixed element IDs in the add form (`Products.razor:22,29,33,41,45,49`) would collide with a second instance of the fields in an edit row.
- `ProductListItem` (`Pantry/ProductService.cs:13-20`) already carries every editable field plus `Id`, so the page can pre-fill an edit form without a new read.

## What We're NOT Doing

- Merging duplicates: a colliding edit is rejected, never merged.
- Optimistic concurrency (row version): two tabs editing the same product → last save wins.
- A separate edit page, a modal `<dialog>`, or any JS interop.
- Undo after delete, bulk delete, or „clear section”.
- Invalidating or refreshing recipe proposals already shown on `/recipes`.
- A database migration: the entity is unchanged.
- A new component in `Components/Ui/` (and so no `/dev/ui` entry): the extracted fields component is product-specific.

## Implementation Approach

Service first, then page — the same split as S-01. Phase 1 adds `UpdateProductAsync` and `DeleteProductAsync` to `ProductService`, extracting the add path's validation/normalisation into one private helper and extending `ExistsAsync` with an optional excluded ID. Both new methods look up the product by `Id` **and** `UserId`, so another user's ID is indistinguishable from a missing one (`NotFound`). Phase 2 extracts the five form fields into a `ProductFields` component (used by the add form and the edit row), then adds per-row edit and delete-confirm states to `Products.razor`, held as page fields (`editingId`, `confirmingDeleteId`), all in Blazor state.

## Critical Implementation Details

- **NotFound wins over Duplicate.** Update must establish that the product exists for this user before reporting a duplicate, so a foreign or deleted ID always yields `NotFound`. The write itself must also re-filter by `Id` and `UserId`: a product deleted between the check and the save yields `NotFound`, not an exception.
- **Unique IDs per form instance.** `ProductFields` takes an ID prefix; every `id`/`for` pair and the `InputRadioGroup` `Name` derive from it (add form keeps `product-…`, edit row uses e.g. `edit-{id}-…`), otherwise labels in the edit row would focus the add form's inputs.
- **Button classes.** Use only token-backed variants: `btn-link` for „Zmień”/„Anuluj”, `btn-outline-danger` for the row's „Usuń”, `btn-danger` for the confirming „Usuń”, `btn-primary` for „Zapisz” (all with `btn-sm`). Do not use `btn-secondary`/`btn-outline-secondary`, which `app.css` does not re-point.
- **Focus.** Entering edit mode focuses the edit form's name input after render (same `OnAfterRenderAsync` pattern as the add form, `Products.razor:189-197`).

## Phase 1: Service and tests

### Overview

`ProductService` gains update and delete with the same validation, normalisation, duplicate rule and privacy filter as add, covered by xUnit tests.

### Changes Required:

#### 1. Product service

**File**: `Pantry/ProductService.cs`

**Intent**: Add update and delete operations that only ever touch the caller's own product, and share input handling with add so the three paths can't drift apart.

**Contract**:
- New enums next to `AddProductResult`: `UpdateProductResult { Updated, Duplicate, NotFound }` and `DeleteProductResult { Deleted, NotFound }`.
- `Task<UpdateProductResult> UpdateProductAsync(string userId, int productId, ProductForm form, CancellationToken ct = default)`:
  - Throws `ArgumentException` for an empty `userId` or invalid form, exactly as `AddProductAsync` does (same rules, same messages).
  - Returns `NotFound` when no product with `productId` belongs to `userId` (including another user's product, and a product deleted before or during the call).
  - Returns `Duplicate` when another product of the same user (`Id != productId`) has the same `NormalizedName` in the target category; a lost race into the unique index also returns `Duplicate` (re-check after `DbUpdateException`, as in add).
  - Otherwise overwrites `Name`, `NormalizedName`, `Category`, `Quantity`, `ExpiresOn`, `StorageLocation` (clearing optional fields to `null` is allowed) and returns `Updated`.
- `Task<DeleteProductResult> DeleteProductAsync(string userId, int productId, CancellationToken ct = default)`: throws `ArgumentException` for an empty `userId`; deletes the product only when it belongs to `userId`; returns `NotFound` otherwise.
- Validation/normalisation extracted into one private helper used by add and update; `ExistsAsync` gains an optional product ID to exclude.

#### 2. Service tests

**File**: `KitchenAssistant.Tests/ProductServiceTests.cs`

**Intent**: Prove the update/delete rules against real relational behaviour (unique index, FK), reusing the existing fixture and race hook.

**Contract**: New `[Fact]`/`[Theory]` cases listed under Testing Strategy → Unit Tests; existing tests unchanged. A small helper to fetch a product's ID by name may be added to the test class.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build KitchenAssistant.slnx`
- Service tests pass: `dotnet test KitchenAssistant.slnx` (all cases listed under Testing Strategy → Unit Tests)
- No pending model changes: `dotnet ef migrations has-pending-model-changes` exits 0

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human before proceeding to the next phase.

---

## Phase 2: Inline edit and delete on `/products`

### Overview

The products page gets per-row „Zmień” and „Usuń” actions with an inline edit form and an inline delete confirmation, reusing one fields component for add and edit.

### Changes Required:

#### 1. Shared product fields

**File**: `Components/Pages/ProductFields.razor` (new; namespace `KitchenAssistant.Components.Pages`, no `@page`)

**Intent**: One definition of the five product inputs (name, category radios, quantity, expiry date, storage location) with their labels and validation messages, so the add form and the edit row render identically.

**Contract**: Parameters `ProductForm Form` (editor-required), `string IdPrefix` (editor-required). Must be rendered inside an `EditForm` whose model is that `Form`. Uses `FormField` for name/quantity/expiry/location and the existing `fieldset` + `InputRadioGroup` for category. Exposes the name input's `ElementReference` (or a `FocusNameAsync()` method) so the page can move focus. Element IDs and the radio-group name derive from `IdPrefix`. Keeps the Polish labels and `ParsingErrorMessage` texts from `Products.razor:21-56`. Not placed in `Components/Pages/Products/` (would clash with the `Products` page class) nor in `Components/Ui/` (product-specific).

#### 2. Products page

**File**: `Components/Pages/Products.razor`

**Intent**: Replace the inline add-form fields with `ProductFields`, and add row-level edit and delete flows.

**Contract**:
- **Add form**: renders `<ProductFields Form="form" IdPrefix="product" />`; add behaviour, messages and focus-after-add unchanged.
- **Row (display)**: existing content plus, aligned to the end of the row, „Zmień” (`btn btn-sm btn-link`) and „Usuń” (`btn btn-sm btn-outline-danger`), each with `aria-label` including the product name (e.g. „Zmień mleko”, „Usuń mleko”).
- **Row (editing)**: when `editingId == item.Id`, the row shows an `EditForm` with `DataAnnotationsValidator`, `<ProductFields IdPrefix="edit-{id}" …>`, „Zapisz” (`btn btn-sm btn-primary`, disabled while saving) and „Anuluj” (`btn btn-sm btn-link`), plus a row-level error line (`text-danger`, `role="alert"`). The edit `ProductForm` is pre-filled from the `ProductListItem`. Name input is focused after the row renders.
- **Row (confirm delete)**: when `confirmingDeleteId == item.Id`, the row's buttons are replaced by „Usunąć „{name}”?” and „Usuń” (`btn btn-sm btn-danger`, disabled while deleting) / „Anuluj” (`btn btn-sm btn-link`).
- **One row at a time**: starting edit or confirm on a row cancels any other row's edit/confirm state (unsaved edits in the other row are discarded).
- **Results**:
  - `Updated` → leave edit mode, reload the list (product may change section/order/expiry flag).
  - `Duplicate` → stay in edit mode, show „Produkt „{name}” jest już w kategorii „{category label}”.” (same text as add).
  - `Deleted` → reload the list.
  - `NotFound` (update or delete) → leave edit/confirm mode, reload the list, show the page-level message „Ten produkt został już usunięty.” in the existing error area.
  - Unexpected exception → log through `ILogger<Products>`, stay in the current mode, show „Nie udało się zapisać zmian. Spróbuj ponownie.” (update) or „Nie udało się usunąć produktu. Spróbuj ponownie.” (delete). An exception must not reach the circuit.
- Double-click guard on save/delete, as in `AddAsync` (`Products.razor:154-158`).
- No literal colours; run the CLAUDE.md literal-colour `grep` on the touched `.razor` files.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build KitchenAssistant.slnx`
- Tests still pass: `dotnet test KitchenAssistant.slnx`
- No literal colours in touched views: the CLAUDE.md `grep -nE` colour scan over `Components/Pages/Products.razor` and `Components/Pages/ProductFields.razor` prints nothing

#### Manual Verification:

- Adding a product still works exactly as before (fields, validation, duplicate message, form reset keeping the category, focus back on name), and clicking a field label in the add form focuses the add form's input
- „Zmień” on a row shows its five fields pre-filled with focus on the name; changing quantity, expiry date and location and saving shows the new values; clearing them shows none; „Anuluj” discards changes
- Changing the category moves the product to the other section in A→Z position; setting the expiry date to today shows „Sprawdź termin”
- Renaming „mleko” to „Mleko” saves; renaming to, or moving onto, a name already in the target category shows the duplicate message and keeps the edit form open; invalid input shows the Polish validation messages and saves nothing
- „Usuń” shows the inline confirmation; „Anuluj” restores the row; confirming removes the product; opening edit on one row while another row is confirming (or editing) closes the other
- With `/products` open in two tabs, deleting a product in one tab and then editing or deleting it in the other shows „Ten produkt został już usunięty.” and a refreshed list
- Layout is usable at phone width (375 px): row buttons wrap without horizontal scroll

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful.

---

## Testing Strategy

### Unit Tests:

- **Update all fields:** a product updated with new name, category, quantity, expiry date and location shows every new value in the right section.
- **Clearing optional fields:** updating with `Quantity = "   "`, `ExpiresOn = null`, `StorageLocation = null` stores `null` for all three; the name is trimmed.
- **Case-only rename:** `"mleko"` → `" Mleko "` in the same category returns `Updated` and the item's name is `"Mleko"`.
- **Rename onto an existing name:** with `"mleko"` and `"ser"` in `UseFirst`, renaming `"ser"` to `"MLEKO"` returns `Duplicate` and both products are unchanged.
- **Move between categories:** with `"mleko"` in both `UseFirst` and `Stored`, moving the `UseFirst` one to `Stored` returns `Duplicate`; with `"mleko"` only in `UseFirst`, the same move returns `Updated` and it now appears only in `Stored`.
- **Expiry recomputed:** after updating `ExpiresOn` to today (Warsaw), the item has `IsExpiryDue = true`.
- **Privacy on update:** user B updating user A's product ID returns `NotFound` and A's product is unchanged — also when B already has a product whose name would collide (NotFound wins).
- **Missing ID on update:** an unknown ID returns `NotFound`.
- **Update race:** a rival `"mleko"` inserted (via `ConnectionDbContextFactory.BeforeCreate`) right before the write context makes `UpdateProductAsync` renaming `"ser"` → `"mleko"` return `Duplicate`, with one `"mleko"` in the list.
- **Update validation:** empty/whitespace name and missing category throw `ArgumentException`.
- **Delete:** deleting an own product returns `Deleted` and it disappears; the other products stay.
- **Privacy on delete:** user B deleting user A's product returns `NotFound` and A still has it.
- **Missing ID on delete:** an unknown or already deleted ID returns `NotFound`.
- **Missing user ID:** `null`/`""` throws for both `UpdateProductAsync` and `DeleteProductAsync` (extend the existing `Missing_user_id_is_rejected` theory).

### Integration Tests:

- None automated in this change; the page is covered by the Phase 2 manual walkthrough (no bUnit in the test project).

### Manual Testing Steps:

1. `dotnet run --launch-profile https`, sign in, open `/products` with a few products in both sections.
2. Walk through every Phase 2 manual criterion in order.
3. Repeat the edit + delete happy path on the production-like stack (`./scripts/local-prod.ps1`, http://localhost:8090/products).

## Performance Considerations

None: one small query per action on a per-user list of at most a few dozen rows; the page reloads the list after each change as it already does after add.

## Migration Notes

No schema change. `dotnet ef migrations has-pending-model-changes` must still exit 0.

## References

- Roadmap slice: `context/foundation/roadmap.md` (S-06)
- PRD: `context/foundation/prd.md` (FR-003, Access Control)
- Previous slice plan: `context/archive/2026-10-03-pantry-add-products/plan.md`
- Add path and race fallback: `Pantry/ProductService.cs:59-121`
- Race test pattern: `KitchenAssistant.Tests/ProductServiceTests.cs:178-197`
- Add form fields to extract: `Components/Pages/Products.razor:21-56`
- Token-backed button variants: `wwwroot/app.css:245-292`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Service and tests

#### Automated

- [x] 1.1 Solution builds: `dotnet build KitchenAssistant.slnx` — 0d272e9
- [x] 1.2 Service tests pass: `dotnet test KitchenAssistant.slnx` — 0d272e9
- [x] 1.3 No pending model changes: `dotnet ef migrations has-pending-model-changes` exits 0 — 0d272e9

### Phase 2: Inline edit and delete on `/products`

#### Automated

- [x] 2.1 Solution builds: `dotnet build KitchenAssistant.slnx` — f4f0318
- [x] 2.2 Tests still pass: `dotnet test KitchenAssistant.slnx` — f4f0318
- [x] 2.3 No literal colours in `Products.razor` and `ProductFields.razor` — f4f0318

#### Manual

- [x] 2.4 Add flow unchanged; add-form labels focus add-form inputs — f4f0318
- [x] 2.5 „Zmień” pre-fills and focuses; field changes and clearing save; „Anuluj” discards — f4f0318
- [x] 2.6 Category change moves the product A→Z; expiry today shows „Sprawdź termin” — f4f0318
- [x] 2.7 Case-only rename saves; colliding rename/move shows duplicate message; invalid input saves nothing — f4f0318
- [x] 2.8 Inline delete confirm works; only one row in edit/confirm state at a time — f4f0318
- [x] 2.9 Product deleted in another tab shows „Ten produkt został już usunięty.” and refreshed list — f4f0318
- [x] 2.10 Row buttons wrap at 375 px without horizontal scroll — f4f0318
