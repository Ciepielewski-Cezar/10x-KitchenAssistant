# Test Plan Refresh (2026-10-13) — Plan Brief

> Full plan: `context/changes/test-plan-refresh-2026-10-13/plan.md`

## What & Why

`context/foundation/test-plan.md` still describes the repo as of 2026-10-11. Since then, Phase 1 shipped, the CI gate and the `main` ruleset went live, agent hooks were merged, and Playwright E2E was set up. The 2026-10-13 interview found no new top risk, so this refresh corrects stale facts, applies the approved risk #3/#4 and Phase 2 edits, and fills the E2E cookbook. It doesn't edit the guide outside a change folder.

## Starting Point

The guide's §3 shows Phase 1 `not started`, although it was archived as `2026-10-11-testing-ai-failure-surfacing`. Risk #2 says "no CI workflow", although `ci.yml` `gate` has been required by the `main` ruleset since PR #25. §4 says "Playwright for .NET, none yet", although `@playwright/test` 1.64 specs exist in open PR #27. §6.4 is TBD.

## Desired End State

The guide matches the repo as of 2026-10-13:

- §3 shows Phase 1 `complete`.
- Risk #2's evidence and the §5 gates describe the live gate, the ruleset, both hooks and local-only E2E.
- Risk #3 carries the F-01 deadline evidence. Risk #4 records which facet the browser spec covers and which stays with Phase 2.
- §6.4 is a copyable E2E recipe.
- §8 is dated 2026-10-13.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Risk set and order | No new risks; §1 and the risk order unchanged; no file:line anchors in §2 | The interview found no new top risk, and the scope matches the hot-spot rescan. | Interview 2026-10-13 |
| Risk #4 guidance | Browser facet covered by `products-cross-user-isolation.spec.ts`; anonymous redirect and prerendered-HTML isolation stay with Phase 2 HTTP tests | Records which part of the risk is already proven so research doesn't redo it. | Interview (user-approved) |
| Phase 2 E2E remainder | generate → open details | The seed already covers sign in → add a product. | Interview (user-approved) |
| Risk #3 rating | Stays High × High, with F-01 unshipped 10 days before 2026-10-22 | Cloud deploy is still untried this close to the deadline. | Interview (user-approved) |
| Risk #2 framing | Gate and ruleset live (PR #25); gate-proof PRs pending; still outside the rollout | Verified with `gh`: the ruleset is already active, not "once merged". | Plan (verified) |
| §5 build row | Split: CI fails on errors only; end-of-turn hook enforces warnings as errors | testing-ci-test-gate deferred this correction to a refresh, and the hook does enforce warnings. | Plan (verified) |
| §6.4 conflict with testing-ci-test-gate | Write the gate pattern as a §5 note and add a redirect note to that plan's Phase 3 | Gates live with gates, §6 stays at six entries, and the overwrite is prevented. | Plan |
| Delivery | Commit to PR #27 (this branch) | The guide shouldn't describe specs that aren't on `main`. | Plan |
| Hot-spot counts | Record the 2026-10-12 rescan in §8 only | Keeps §1 unchanged and §1/§2 consistent. | Plan |
| Test-base profile | Stays `sparse` | 13 of the 17 files cluster in `Recipes/`, which the profile definition counts as sparse. | Plan |

## Scope

**In scope:**

- `test-plan.md`: header, §2 risks #2 and #3 evidence, guidance rows #2 and #4, the §3 Phase 1 row and paragraphs, §4 profile/E2E/hooks/grounding, §5 table plus the "Adding a gate" note, §6.3 pointer, §6.4 cookbook, §8 ledger
- An inline note in testing-ci-test-gate Phase 3

**Out of scope:**

- §1, §7, and the risk numbers and order
- `test-stack.md`, the specs, the hooks and the workflow
- testing-ci-test-gate's Progress drift, and E2E in CI

## Architecture / Approach

In-place section edits in reader-dependency order. Strategy (§2–§5) comes first, then the cookbook and ledger (§6, §8) that refer to it. Each phase is committed on `claude/testing-ci-test-gate` and lands in PR #27 through the required `gate` check. Only planned files are staged, because the working tree holds unrelated `.claude/**` and `CLAUDE.md` tool updates.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Strategy sections (§2–§5) | Corrected evidence, approved risk #3/#4 edits, §3 state, §4 stack, §5 gates | §2 wording drifting from the approved items, or an anchor slipping in |
| 2. Cookbook, ledger, cross-plan note | §6.4 E2E recipe, §6.3 pointer, §8 dates, testing-ci-test-gate redirect, push to PR #27 | The commit sweeping in unrelated working-tree changes |

**Prerequisites:** PR #27 open on `claude/testing-ci-test-gate`; `gh` authenticated.
**Estimated effort:** about 1 short session, 2 phases, docs only.

## Open Risks & Assumptions

- If PR #27 is reworked or dropped, §4 and §6.4 would describe E2E that never reaches `main`. In that case the refresh moves with the specs.
- testing-ci-test-gate Phase 3 may still find something about the gate, such as a proof that fails unexpectedly. That would update the §5 note.

## Success Criteria (Summary)

- A reader of `test-plan.md` sees the true rollout state: Phase 1 complete, Phase 2 next with a narrower E2E remainder, and the CI gate required.
- An agent can add a new E2E spec from §6.4 alone.
- The next `/10x-test-plan` run routes to Phase 2 without manual fixes.
