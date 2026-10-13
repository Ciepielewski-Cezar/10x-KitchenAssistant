# Test Plan Refresh (2026-10-13) Implementation Plan

## Overview

Refresh `context/foundation/test-plan.md` (last updated 2026-10-11). The 2026-10-13 interview raised no new top risk. Four kinds of edit are needed: correct stale facts in §2–§5, apply the user-approved §2/§3 edits for risks #3 and #4 and the Phase 2 E2E remainder, fill the §6.4 E2E cookbook from the shipped specs, and bump §8. This is a docs-only change in one file, plus one inline note in another change's plan. It lands in open PR #27 next to the E2E specs it describes. §1, §7, the risk order and the risk numbers stay as they are, and §2 never gets a file:line anchor.

## Current State Analysis

`test-plan.md` still describes the repo as of the morning of 2026-10-11. Since then:

- **Rollout Phase 1 shipped and was archived.** Commits `33eca9d`…`b416631` landed it, and `0306290` archived it as `context/archive/2026-10-11-testing-ai-failure-surfacing/` (`status: archived`). The §6.2 and §6.6 cookbook entries already describe it, but the §3 row still reads `not started` / `—` (`test-plan.md:80`).
- **The CI gate is live and required.** PR #25 (merged 2026-10-11) added `.github/workflows/ci.yml` (`gate`: Release build → `has-pending-model-changes` → tests → zero-executed guard; plus an advisory `vulnerable-packages` job) and `.github/rulesets/main.json`. `gh api …/rules/branches/main` lists `deletion`, `non_fast_forward`, `pull_request`, `required_status_checks`. The gate-proof PRs from testing-ci-test-gate Phase 3 were never opened, and Progress rows 2.4–3.6 are unchecked. That change folder is still `implementing`. §2 risk #2 still says "no CI workflow in repo" (`:51`), and §5 says CI is "outside this rollout" (`:137-145`).
- **The CI build doesn't enforce warnings.** testing-ci-test-gate decided warnings stay warnings and left the overstated §5 build row "for `/10x-test-plan --refresh`" (`context/changes/testing-ci-test-gate/plan.md:38`).
- **Agent hooks were merged in PR #26 (`3ba9bcd`).** `.claude/settings.json` wires `PostToolUse` (Write|Edit) → `.claude/hooks/check-edited-file.mjs`, which builds the solution and reports the edited file's diagnostics and its related tests. It also wires `Stop` → `.claude/hooks/end-of-turn.mjs`, which builds with warnings as errors, runs the whole unit suite, and runs the pending-model check when `Data/` changed. `KitchenAssistant.Tests/Tooling/AgentHooksTests.cs` covers the hooks. §4 has no hooks row, and §5 lists the post-edit hook only as "recommended".
- **E2E exists, but only in open PR #27** (`670cdb6`, `83fd4fb`). It uses `@playwright/test` 1.64.0 in TypeScript (`context/foundation/test-stack.md`). Its pieces:
  - `playwright.config.ts`: `webServer` runs the Development build (Fake generator, LocalDB) on port 5180, with a `setup` project and `storageState`.
  - `tests/e2e/auth.setup.ts` and `auth-b.setup.ts` set up two users.
  - `tests/e2e/seed.spec.ts` covers risk #3's interactive-connection slice: a product added over the circuit survives a reload.
  - `tests/e2e/support/blazor.ts` provides `waitForInteractive`.
  - `tests/e2e/products-cross-user-isolation.spec.ts` covers risk #4's browser facet.

  CI doesn't run E2E. §4 still says "Playwright for .NET … none yet" (`:120`), and §6.4 is TBD (`:223-227`).
- **The test base is 17 xUnit test files, 13 under `KitchenAssistant.Tests/Recipes/`.** The others are in `Pages/`, `Account/`, `Tooling/` and the root, and `Support/` is excluded from the count. There are also 2 E2E specs and 2 setup files. §4 says 15 files with no component or E2E tests (`:109-111`).
- **Two changes plan to write §6.4.** testing-ci-test-gate Phase 3 plans to write test-plan §6.4 "Adding a gate" plus a §6.6 entry headed "Phase 1" (`context/changes/testing-ci-test-gate/plan.md:197-206`). In this file §6.4 is the E2E slot, and rollout Phase 1 is AI failure surfacing.

## Desired End State

`test-plan.md` matches the repo as of 2026-10-13:

- §3 shows Phase 1 `complete`.
- Risk #2's evidence and the §5 gates describe the live CI gate, the ruleset, the hooks and local-only E2E.
- Risk #3 carries the F-01 deadline evidence. Risk #4's guidance records the browser/HTTP split.
- §6.4 teaches how to add an E2E test.
- §8 shows the refresh date.
- testing-ci-test-gate Phase 3 points its test-plan edit at the §5 note, so it no longer targets §6.4.

Verify with the Phase 1–2 checks below, then with PR #27's green `gate` check.

### Key Discoveries:

- `ci.yml`'s `gate` job and the active `main` ruleset are on `main` (PR #25, merged 2026-10-11T19:05Z). The gate is already required. It doesn't wait for "once merged".
- The end-of-turn hook enforces warnings as errors (`.claude/hooks/end-of-turn.mjs:2`). CI doesn't (`context/changes/testing-ci-test-gate/plan.md:38`). The §5 build row therefore needs two rows, or one row with two `Where` values.
- The test-base profile stays **`sparse`**. 17 files clear the ~15 threshold, but 13 of them cluster in `Recipes/`, and the profile definition counts clustering as sparse (`.claude/skills/10x-test-plan/SKILL.md:154`).
- The schema calls §6 "three to six entries" the sweet spot (`.claude/skills/10x-test-plan/references/test-plan-schema.md`, §6). That's why the gate pattern goes under §5 and not into a new §6.7.
- The status vocabulary in the §3 Status cell is load-bearing: `complete` is the parser literal.

## What We're NOT Doing

- No new risks, no renumbering, no reordering. §1 is left unchanged, including the scan-window line and the 31-commit count. The §2 hot-spot counts stay at 18/10. The 2026-10-12 rescan goes into §8 only (decision).
- No file:line anchors, function names or module names in §2. Spec file names appear only where the user approved them (risk #4 guidance).
- No changes to §7 (interview: keep as is).
- No §3 row for the CI gate: risk #2 stays outside the rollout (decision 2026-10-11, reaffirmed 2026-10-13).
- No edits to testing-ci-test-gate's Progress rows or its other contracts. Its drift (2.4–2.5 unchecked although #25 merged) belongs to that change.
- No changes to `test-stack.md` (owned by `/10x-e2e-setup`), the specs, the hooks or the workflow.
- No E2E in CI. §5 records it as local-only.

## Implementation Approach

Edit sections in place, in the order a reader depends on them. Strategy (§2–§5) comes first so the cookbook (§6.4) and ledger (§8) can refer to it. Both phases commit to `claude/testing-ci-test-gate`, so they ride PR #27 (decision) and pass through the required `gate` check. Keep the existing tone and table shapes. Cite evidence (PR numbers, change folders, dates, the interview) in §2, and concrete paths in §4–§6.

## Critical Implementation Details

- **Before committing, check the working tree.** It holds unrelated uncommitted `.claude/**` and `CLAUDE.md` tool updates. Stage only `context/foundation/test-plan.md`, `context/changes/testing-ci-test-gate/plan.md` and this change folder. Don't sweep in the 10x-cli changes.
- **The §1 baseline is `83fd4fb`.** The "§1 unchanged" check diffs against that commit, not `HEAD`, because Phase 1's commit moves `HEAD`.

## Phase 1: Strategy Sections (§2–§5)

### Overview

Correct the stale facts and apply the approved edits in §2, §3, §4 and §5, plus the "Last updated" header.

### Changes Required:

#### 1. Risk Map evidence (§2 table)

**File**: `context/foundation/test-plan.md`

**Intent**: Make risks #2 and #3 cite current evidence without changing their wording, impact, likelihood or order.

**Contract**:
- Risk #2 Source: replace "no CI workflow in repo" with evidence that a gate now exists. `ci.yml` `gate` and the `main` ruleset have been live since PR #25 (2026-10-11), testing-ci-test-gate's gate-proof PRs are not yet run, and there is still no deploy workflow. Impact and Likelihood stay High × High: the gate's red paths are unproven and F-01 auto-deploy isn't wired.
- Risk #3 Source: append that F-01 was still unshipped on 2026-10-12, 10 days before the 2026-10-22 deadline. It stays High × High.
- Risk #1 Source counts stay at 18/10.

#### 2. Risk Response Guidance (§2 guidance table)

**File**: `context/foundation/test-plan.md`

**Intent**: Record what the risk #2 and #4 rows prove now, so Phase 2 research doesn't redo covered ground.

**Contract**:
- Row #2: the "Context `/10x-research` must ground" cell no longer asks "whether a workflow exists yet". Instead it points at the gate-proof PRs (failing test, pending model change, test project removed from the solution) as the outstanding proof. The row keeps the "(outside rollout …)" label.
- Row #4: record the split.
  - The browser facet is covered by `products-cross-user-isolation.spec.ts`: user B's live (circuit) `/products` page never lists user A's product.
  - Anonymous redirect and isolation in the prerendered HTML stay with Phase 2's HTTP integration test.
  - "Likely cheapest layer" stays HTTP integration for the remaining facets.

#### 3. Phased Rollout (§3)

**File**: `context/foundation/test-plan.md`

**Intent**: Show the real rollout state and narrow Phase 2's optional E2E sub-phase to what the seed doesn't already cover.

**Contract**:
- Phase 1 row: Status `complete`, Change folder `archive/2026-10-11-testing-ai-failure-surfacing`. Phases 2 and 3 stay `not started` / `—`.
- Phase 2 E2E paragraph (`:88-95`): the seed already covers the sign in → add a product slice over the live connection, and `products-cross-user-isolation.spec.ts` covers risk #4's browser facet. What remains for the sub-phase is generate recipes → open details with the Fake generator. Keep the drop-if-redundant and one-flow-only rules.
- Risk #2 paragraph (`:97-100`): the CI gate and ruleset are live through change `testing-ci-test-gate`, whose gate-proof PRs are pending. Tests added by this rollout now run in the required `gate` check. E2E stays local-only. Risk #2 stays outside the rollout.

#### 4. Stack (§4)

**File**: `context/foundation/test-plan.md`

**Intent**: Describe the test base and tools that exist today.

**Contract**:
- Test-base profile stays **sparse**, now described as:
  - 17 xUnit test files (13 in `Recipes/`), bUnit page tests since Phase 1, and hook tests;
  - 2 Playwright specs plus 2 auth setup files;
  - CI gate on every PR;
  - still no HTTP-pipeline tests.
- E2E row: `@playwright/test` (TypeScript), version 1.64.0, checked: 2026-10-13. Note: config, commands and auth are in `context/foundation/test-stack.md`. It runs locally against the Development build with the Fake generator and isn't in CI.
- New row, layer "agent hooks (Claude Code)": `.claude/settings.json` → `check-edited-file.mjs` (per edit) and `end-of-turn.mjs` (Stop), version n/a, checked: 2026-10-13, covered by `KitchenAssistant.Tests/Tooling/AgentHooksTests.cs`.
- Grounding tools: bump the dates to 2026-10-13 where re-checked. Provider/platform: GitHub has the `CI` workflow and the `main` ruleset (no longer "no workflows yet"). Runtime/browser: add `playwright-cli` (`.claude/skills/playwright-cli/SKILL.md`) for E2E exploration.

#### 5. Quality Gates (§5)

**File**: `context/foundation/test-plan.md`

**Intent**: List the gates that are actually wired and where, and record how to add one. This settles the §6.4 conflict.

**Contract**:
- Table rows:
  - build in CI: `gate`, required by the ruleset, fails on compiler errors only (warnings stay warnings);
  - build in the end-of-turn hook: warnings as errors, local agent loop;
  - pending model changes: CI `gate`, required; also end-of-turn when `Data/` changed, and local before commit;
  - unit + component tests: CI `gate`, required, with the zero-executed guard;
  - HTTP integration: not yet, "required after §3 Phase 2";
  - E2E: local only, optional (`npx playwright test`), not in CI;
  - post-deploy smoke: required after §3 Phase 2, unchanged;
  - post-edit hook: wired in `3ba9bcd`, local agent loop, edited file's diagnostics and related tests;
  - vulnerable packages: CI advisory job, not required.
- Replace the closing sentence "CI wiring … belongs to the deployment work" with a short **Adding a gate** note:
  - a new check becomes a step in `gate` (required) or its own advisory job;
  - `.github/rulesets/main.json` changes only when a new required check name appears, and is re-applied with `PUT …/rulesets/<id>`;
  - no `paths` filters on required checks;
  - the future `deploy.yml` calls `ci.yml` via `workflow_call`.

#### 6. Header

**File**: `context/foundation/test-plan.md`

**Intent**: Bump the date.

**Contract**: `Last updated: 2026-10-13`.

### Success Criteria:

#### Automated Verification:

- §1 is byte-identical to the baseline: `diff <(git show 83fd4fb:context/foundation/test-plan.md | sed -n '/^## 1\./,/^## 2\./p') <(sed -n '/^## 1\./,/^## 2\./p' context/foundation/test-plan.md)` prints nothing
- §2 has no file:line anchors: `sed -n '/^## 2\./,/^## 3\./p' context/foundation/test-plan.md | grep -nE '[A-Za-z0-9_/.-]+\.(cs|razor|ts|mjs|json|md|yml):[0-9]'` prints nothing
- Risk numbers and order are unchanged: `sed -n '/^## 2\./,/^### Risk Response/p' context/foundation/test-plan.md | grep -oE '^\| [0-9]+ \|'` prints `| 1 |` … `| 6 |` in order, and the Impact/Likelihood cells match the baseline
- §3 Phase 1 row reads `complete` and `archive/2026-10-11-testing-ai-failure-surfacing`: `grep -n '^| 1 | AI failure surfacing' context/foundation/test-plan.md`
- Stale phrases are gone: `grep -nE 'no CI workflow in repo|Playwright for \.NET|15 test files|CI outside this rollout|no workflows yet' context/foundation/test-plan.md` prints nothing

#### Manual Verification:

- Risk #2/#3/#4 edits and the §3 paragraphs read correctly and match the approved wording (items 2, 7, 8, 9 of the change brief)
- The §5 table matches what `ci.yml`, the ruleset and the two hooks actually do

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Cookbook, Ledger and Cross-Plan Note

### Overview

Fill §6.4, point §6.3 at the risk #4 split, bump §8, redirect testing-ci-test-gate Phase 3, and ship through PR #27.

### Changes Required:

#### 1. E2E cookbook (§6.4)

**File**: `context/foundation/test-plan.md`

**Intent**: Give the next agent a copyable recipe for an E2E test, written in the §6.2 style with building blocks, steps and a caveat.

**Contract**: Replace the TBD with:
- **When**: only for a flow that needs the live interactive connection or several pages, and that no bUnit or HTTP test can cover.
- **Reference**: `tests/e2e/seed.spec.ts`, the exemplar to copy. Run setup and commands as described in `context/foundation/test-stack.md` (link to it, don't duplicate it).
- **Building blocks**:
  - `waitForInteractive(page)` (`tests/e2e/support/blazor.ts`): call it before any non-idempotent action on an `InteractiveServer` page. It's the one DOM query the suite allows.
  - The `setup` project with `storageState` (user A).
  - The two-user pattern from `products-cross-user-isolation.spec.ts`: a `pageB` fixture opens its own browser context with `playwright/.auth/user-b.json` (written by `auth-b.setup.ts`, credentials `E2E_USERNAME_B`/`E2E_PASSWORD_B`).
- **Rules**:
  - role/label/text locators only;
  - no `waitForTimeout`;
  - unique names with a `Date.now()` suffix (plus `workerIndex` in fixtures);
  - cleanup through the UI, asserted, in `afterEach` for multi-user specs;
  - a header comment naming the risk and facet.
- **Caveat**: E2E runs against the Development build with the Fake generator and LocalDB, and isn't in CI.

#### 2. HTTP cookbook pointer (§6.3)

**File**: `context/foundation/test-plan.md`

**Intent**: Keep the Phase 2 TBD accurate after the risk #4 split.

**Contract**: The TBD bullet names the facets that remain: anonymous redirect, prerendered-HTML isolation, and Production composition fails fast. It notes that the browser facet is already covered (§6.4).

#### 3. Freshness ledger (§8)

**File**: `context/foundation/test-plan.md`

**Intent**: Date the refresh and record the hot-spot recheck without touching §1 or §2 counts (decision).

**Contract**:
- Strategy last reviewed: 2026-10-13.
- Stack versions last verified: 2026-10-13.
- AI-native tool references last verified: 2026-10-13.
- Add one line: "Hot-spots rechecked 2026-10-12 (30 days): `Components/Pages` 19, `Recipes/` 11 commits; scope and §2 order unchanged."
- Keep the refresh-trigger list.

#### 4. testing-ci-test-gate Phase 3 redirect

**File**: `context/changes/testing-ci-test-gate/plan.md`

**Intent**: Stop that change's Phase 3 from overwriting the E2E §6.4 slot or adding a mislabelled "Phase 1" entry.

**Contract**: Add an inline note in Phase 3 "#### 2. Test plan cookbook". Test-plan refresh 2026-10-13 already wrote the "Adding a gate" pattern under test-plan §5. Phase 3 should only update that note if the proof shows something new. Its §6.6 entry goes under a heading naming the change (`testing-ci-test-gate`), not "Phase 1", and the warnings-stay-warnings point is already in §5. Leave Progress rows, step titles and the other contracts unchanged.

#### 5. Ship

**File**: none (git and GitHub)

**Intent**: Land the refresh in PR #27 through the required gate (decision).

**Contract**: Commit only the files named in this plan on `claude/testing-ci-test-gate` and push. PR #27 picks up the commit.

### Success Criteria:

#### Automated Verification:

- §6.4 is no longer TBD and names the seed, `waitForInteractive` and the two-user pattern: `sed -n '/^### 6.4/,/^### 6.5/p' context/foundation/test-plan.md | grep -cE 'seed\.spec\.ts|waitForInteractive|products-cross-user-isolation\.spec\.ts'` prints 3 or more, and the section contains no `TBD`
- Every path the cookbook cites exists: `ls tests/e2e/seed.spec.ts tests/e2e/support/blazor.ts tests/e2e/products-cross-user-isolation.spec.ts tests/e2e/auth-b.setup.ts context/foundation/test-stack.md`
- §6 still has exactly six sub-sections: `grep -c '^### 6\.' context/foundation/test-plan.md` prints 6
- §8 dates read 2026-10-13: `sed -n '/^## 8\./,$p' context/foundation/test-plan.md | grep -c '2026-10-13'` prints 3 or more
- The commit touches only the planned files: `git show --stat HEAD` lists `context/foundation/test-plan.md`, `context/changes/testing-ci-test-gate/plan.md` and `context/changes/test-plan-refresh-2026-10-13/**`
- PR #27's checks pass: `gh pr checks 27` shows `gate` and `vulnerable-packages` as pass

#### Manual Verification:

- §6.4 reads as a usable recipe and an agent could write a new spec from it alone
- The testing-ci-test-gate Phase 3 note is clear and its Progress section is untouched

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None. This is a docs-only change.

### Integration Tests:

- PR #27's required `gate` runs the full suite on the commit. It catches nothing in Markdown, but it proves the docs commit didn't ride in with anything else.

### Manual Testing Steps:

1. Read §2 risk #2–#4 and their guidance rows against the change brief's items 2, 7 and 9.
2. Read §3 against item 1 and item 8.
3. Compare §5 with `ci.yml`, `.github/rulesets/main.json` and `.claude/settings.json`.
4. Follow §6.4 mentally to draft a new spec, and check that nothing is missing.

## Performance Considerations

None.

## Migration Notes

None. Once PR #27 merges, the next `/10x-test-plan` run reads the new §3 state: Phase 1 `complete`, next pending Phase 2.

## References

- Change brief: `context/changes/test-plan-refresh-2026-10-13/change.md`
- Guide being refreshed: `context/foundation/test-plan.md`; schema: `.claude/skills/10x-test-plan/references/test-plan-schema.md`; refresh rules: `.claude/skills/10x-test-plan/SKILL.md` (`--refresh` mode)
- CI gate change: `context/changes/testing-ci-test-gate/plan.md` (warnings decision `:38`, Phase 3 test-plan contract `:197-206`)
- E2E stack: `context/foundation/test-stack.md`; specs `tests/e2e/seed.spec.ts`, `tests/e2e/products-cross-user-isolation.spec.ts`, `tests/e2e/support/blazor.ts`
- Hooks: `.claude/settings.json`, `.claude/hooks/check-edited-file.mjs`, `.claude/hooks/end-of-turn.mjs`
- Archived Phase 1: `context/archive/2026-10-11-testing-ai-failure-surfacing/`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Strategy Sections (§2–§5)

#### Automated

- [x] 1.1 §1 is byte-identical to the 83fd4fb baseline — eb6cf18
- [x] 1.2 §2 has no file:line anchors — eb6cf18
- [x] 1.3 Risk numbers, order, impact and likelihood are unchanged — eb6cf18
- [x] 1.4 §3 Phase 1 row reads complete with the archive folder — eb6cf18
- [x] 1.5 Stale phrases are gone — eb6cf18

#### Manual

- [x] 1.6 Risk #2/#3/#4 edits and §3 paragraphs match the approved wording — eb6cf18
- [x] 1.7 §5 table matches ci.yml, the ruleset and the hooks — eb6cf18

### Phase 2: Cookbook, Ledger and Cross-Plan Note

#### Automated

- [x] 2.1 §6.4 names the seed, waitForInteractive and the two-user pattern with no TBD — 72b54b9
- [x] 2.2 Every path the cookbook cites exists — 72b54b9
- [x] 2.3 §6 still has exactly six sub-sections — 72b54b9
- [x] 2.4 §8 dates read 2026-10-13 — 72b54b9
- [x] 2.5 The commit touches only the planned files — 72b54b9
- [x] 2.6 PR #27 checks pass — 736e704

#### Manual

- [x] 2.7 §6.4 reads as a usable recipe — 72b54b9
- [x] 2.8 testing-ci-test-gate Phase 3 note is clear and its Progress is untouched — 72b54b9
