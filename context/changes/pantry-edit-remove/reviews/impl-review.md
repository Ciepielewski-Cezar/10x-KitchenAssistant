<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Pantry: edit and remove products (S-06)

- **Plan**: context/changes/pantry-edit-remove/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2
- **Date**: 2026-10-08
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 1 warning, 8 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Evidence for the PASS rows:
- **Plan Adherence**: every contract bullet, critical detail and all 14 unit-test cases match the plan. Nothing is missing.
- **Success Criteria**: at HEAD the build has 0 warnings, tests pass 140/140, `has-pending-model-changes` exits 0 and the colour grep is empty. You confirmed manual checks 2.4–2.10.
- **Architecture**: the page talks only to `ProductService`, and every query filters by `UserId`.

## Findings

### F1 — List rows have no `@key`, so a reload can rebuild the open edit form

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Components/Pages/Products.razor:82
- **Detail**: The `@foreach` renders a stateful `EditForm` + `ProductFields` inside `<li>` with no `@key`, so Blazor matches rows by position. When a reload shifts the edited row's index, the edit form is torn down and rebuilt. That happens when an add finishes during an edit, or when another row's save or delete moves a product. The rebuild loses validation messages, focus, and any typed text not yet committed by a change event.
- **Fix**: Add `@key="item.Id"` to the `<li>`.
- **Decision**: FIXED — `@key="item.Id"` on the row `<li>`.

### F2 — Concurrent handlers: a stale `LoadAsync` can overwrite a newer list

- **Severity**: 💬 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: Components/Pages/Products.razor:157-169 (called from AddAsync, SaveEditAsync, DeleteAsync)
- **Detail**: `saving`, `savingEdit` and `deleting` are separate guards, so add, save and delete can be in flight at once. Each one ends with `LoadAsync`, which assigns `products` unconditionally, so an older load that finishes last shows a stale list until the next action. No data is lost. A related case: confirming a delete while that row's save is still running can show „Ten produkt został już usunięty.” for the user's own delete.
- **Fix A ⭐ Recommended**: Add a load counter, so `LoadAsync` applies its result only if it is still the latest request.
  - Strength: Fixes the stale list without changing what the user can click. It is a few lines in one method.
  - Tradeoff: Does not cover the "own delete shows NotFound" case.
  - Confidence: HIGH — standard last-write-wins guard.
  - Blind spot: None significant.
- **Fix B**: Use one shared `busy` flag that disables add, save and delete while any of them is running.
  - Strength: Removes every interleaving, including the NotFound case.
  - Tradeoff: Changes the add form's existing disabled behaviour and blocks unrelated actions for the duration of one call.
  - Confidence: MED — the UX change needs a manual re-check of add.
  - Blind spot: Haven't checked how it interacts with the double-click guards.
- **Decision**: FIXED (Fix A) — `loadVersion` counter in `LoadAsync`; only the latest load replaces the list.

### F3 — `ExpiresOn` and `StorageLocation` are read from the mutable form after the awaits

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Pantry/ProductService.cs:141-142 (same at :92-93)
- **Detail**: `Normalize` copies name, category and quantity before the first await, but the date and location are read from the caller's `ProductForm` after up to three awaits. The edit inputs stay enabled while a save runs, so a date or location changed during the save is written together with the older copy of the other fields.
- **Fix**: Add `ExpiresOn` and `StorageLocation` to `ProductInput` in `Normalize`, and read them from `input` in both add and update.
- **Decision**: FIXED — `ExpiresOn`/`StorageLocation` copied into `ProductInput` by `Normalize`; add and update read them from `input`.

### F4 — The update's NotFound paths during the write have no tests

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: KitchenAssistant.Tests/ProductServiceTests.cs (covers Pantry/ProductService.cs:132-135, :149-152)
- **Detail**: Two paths are untested: a row deleted between the ownership check and the write context, and a row deleted between the load and `SaveChanges` (concurrency exception, then `OwnsAsync`, then NotFound). The plan lists "deleted … during the call → NotFound" as a contract but has no test case for it.
- **Fix**: Add a test where `BeforeCreate` deletes the product at `call == 3` and the update returns `NotFound`.
- **Decision**: FIXED — `Update_of_a_product_deleted_before_the_write_is_not_found` (break-check: red with the null check disabled).

### F5 — `FocusNameAsync` can throw into the circuit

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Components/Pages/ProductFields.razor:54-60
- **Detail**: `ElementReference.FocusAsync()` runs from `OnAfterRenderAsync` with no handling. A `JSException` (element removed by a render in between) or `JSDisconnectedException` would reach the circuit, which the plan rules out. The window is tiny, and the add form behaved this way before the change.
- **Fix**: Catch `JSException` and `JSDisconnectedException` in `FocusNameAsync` and ignore them.
- **Decision**: FIXED — new `Components/Ui/ElementFocus.TryFocusAsync` swallows `JSException`/`JSDisconnectedException`; used by `ProductFields.FocusNameAsync` and the page.

### F6 — Comment gives the wrong reason for the NotFound-before-Duplicate order

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Pantry/ProductService.cs:116
- **Detail**: The comment says the order stops a foreign ID from "revealing which names the user has", but `ExistsAsync` only looks at the caller's own products, so nothing about another user could leak. The real reason is result semantics: the plan says a foreign or deleted ID must always be NotFound.
- **Fix**: Change the comment to "NotFound wins over Duplicate: a foreign or deleted ID is always reported as missing, whatever its new name collides with."
- **Decision**: FIXED — comment reworded to result semantics.

### F7 — Row actions clear the add form's error message

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: Components/Pages/Products.razor:212, :229, :254, :313
- **Detail**: Not in the plan: `StartEdit`, `StartDelete`, `SaveEditAsync` and `DeleteAsync` set `errorMessage = null`. That is the add form's error area, so clicking „Zmień” or „Usuń” also hides a pending add duplicate or failure message. This is benign and arguably what users expect, but it is undocumented.
- **Fix**: Keep the behaviour and note it in the plan's Notes, or in change.md.
- **Decision**: FIXED — behaviour kept and documented in change.md Notes.

### F8 — No CancellationToken on the page, unlike `RecipeSuggestions`

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: Components/Pages/Products.razor (@code) vs Components/Pages/RecipeSuggestions.razor:3,102
- **Detail**: `RecipeSuggestions` implements `IDisposable` and passes a `cts.Token` to service calls. `Products.razor` passes none. The add path already had this gap, and the new update and delete calls widen it even though their service methods accept `ct`. The impact is low because the DB calls are short.
- **Fix**: Adopt the `cts` + `Dispose` pattern in `Products.razor` and pass the token to all four service calls, or leave it as a follow-up.
- **Decision**: FIXED — `Products.razor` implements `IDisposable` with a `cts`; token passed to all four service calls; cancellation swallowed as in RecipeSuggestions.

### F9 — Keyboard focus drops to `<body>` after Usuń, Anuluj, save and delete

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Components/Pages/Products.razor:125-135, :233
- **Detail**: Entering edit mode is the only action that manages focus. Entering delete confirmation, cancelling, or finishing a save or delete removes the button that had focus, and keyboard users land on `<body>`. Also cosmetic: an expiry-due row keeps `list-group-item-warning` while it is in edit mode.
- **Fix**: Focus the confirming „Anuluj” when entering confirmation, and the row's „Zmień” after cancel or save. Leave as a follow-up if it is out of MVP scope.
- **Decision**: FIXED — focus moves to the confirming „Anuluj” on „Usuń”, and back to the row's „Zmień” after cancel or a successful save. After a confirmed delete, focus is still not managed.
