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
