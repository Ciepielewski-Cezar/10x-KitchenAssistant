---
change_id: test-plan-refresh-2026-10-13
title: Refresh test-plan.md with stale facts and approved risk #3/#4 edits (2026-10-13)
status: implemented
created: 2026-10-13
updated: 2026-10-13
archived_at: null
---

## Notes

Open a change folder to refresh context/foundation/test-plan.md (last updated 2026-10-11) without editing it in place. Interview 2026-10-13: no new top risk; RecipeSuggestions.razor and Recipes/ remain the low-confidence areas (matches hot-spots: Components/Pages 19 commits, Recipes/ 11, 30d to 2026-10-12, scope unchanged); keep §7 as is. Stale facts to correct: (1) §3 Phase 1 -> complete, change folder 2026-10-11-testing-ai-failure-surfacing (archived); (2) §2 risk #2 evidence "no CI workflow" -> CI gate + main ruleset live via testing-ci-test-gate, proof PRs (rows 2.4-3.6) still open, risk #2 stays outside the rollout; (3) §4 e2e row Playwright for .NET -> @playwright/test 1.64 TypeScript (see test-stack.md), test-base line 17 xUnit files (13 in Recipes/) + 2 E2E specs, add the agent hooks; (4) §5 gates: build/tests/pending-model-changes in CI and required by the ruleset once merged, post-edit hook wired (3ba9bcd), e2e local-only; (5) §6.4 e2e cookbook from tests/e2e/seed.spec.ts, waitForInteractive (tests/e2e/support/blazor.ts) and the two-user context pattern in products-cross-user-isolation.spec.ts; (6) §8 freshness dates. User-approved §2/§3 edits: (7) risk #4 response guidance records the split: browser facet (circuit identity) covered by products-cross-user-isolation.spec.ts, anonymous redirect + prerendered-HTML isolation stay with Phase 2 HTTP integration; (8) Phase 2 optional e2e sub-phase remainder is generate -> open details (the seed covers the add-product slice); (9) risk #3 stays High x High with the evidence that F-01 is unshipped 10 days before the 2026-10-22 deadline. No new risks; §1 and the risk order unchanged; never add file:line anchors to §2. After creating the folder, follow the downstream continuation rule.
