---
change_id: recipe-generation-spike
title: Measure live recipe generation and lock the model defaults
status: planned
created: 2026-10-08
updated: 2026-10-08
archived_at: null
---

## Notes

Split out of S-02 (`first-recipe-generation`) on 2026-10-08. S-02's Phase 3 rows 3.1–3.5 (the 10-call spike, recording quality and ID validity, and locking the `Recipes` defaults) moved here as steps 1.1–1.5, together with `spike.md`. The smoke test (S-02 row 3.6) stayed in S-02 and passed on 2026-10-05.

The contract below is unchanged from the S-02 plan (`context/changes/first-recipe-generation/plan.md`, Phase 3, changes 1–3), which has the background: SDK retries, deadline, schema caveats.
- 2026-10-08: moved out of milestone M-1 (it was roadmap slice S-07) and parked as the first candidate for the next milestone. It no longer blocks S-04 (`recipe-details`), which assumes the current single-call shape. Until the spike runs, the `Recipes` defaults rest on the 2026-10-05 smoke test.
