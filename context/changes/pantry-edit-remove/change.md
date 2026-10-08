---
change_id: pantry-edit-remove
title: Pantry edit remove
status: impl_reviewed
created: 2026-10-08
updated: 2026-10-08
archived_at: null
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

- Starting or finishing a row action („Zmień”, „Usuń”, save, delete) clears the page-level message under the add form, so a stale add duplicate/failure message disappears with it. „Anuluj” leaves it. Kept on purpose (impl-review F7).
- An unexpected delete failure is shown as a row-level alert under the confirming row, not in the page-level area.
