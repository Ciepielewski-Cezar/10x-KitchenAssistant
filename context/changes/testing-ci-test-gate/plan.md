# CI Test Gate Implementation Plan

## Overview

Add a GitHub Actions gate that every PR to `main` must pass before it can merge: a Release build, the EF Core pending-model-changes check, and the test suite with a guard that fails when zero tests execute. A `main` ruleset with no bypass makes the gate required. A separate, non-required job reports vulnerable packages. The workflow exposes `workflow_call` so the future `deploy.yml` can depend on it. This is test-plan §3 Phase 1 (Risk #2): no merge reaches production without a green build, green tests and no pending model changes.

## Current State Analysis

- No CI exists: `.github/` is absent and the GitHub API reports zero workflows. Actions are enabled with all actions allowed (research §1).
- No deploy exists. `deploy.yml` is only described in `context/changes/deployment/deployment-plan.md:188-204`, Phase 5, which is not started.
- `main` is unprotected: rulesets `[]`, classic protection 404. The maintainer is `ADMIN` on a public repo, so rulesets are free (research §3).
- Every gate command passes locally on `b1b1241`: Release build with 0 warnings, 224/224 tests, `has-pending-model-changes` exit 0, no vulnerable packages. None needs SQL Server, LocalDB or an Anthropic key (research §2).
- **Zero-test false green, verified on 2026-10-11:** `dotnet test KitchenAssistant.slnx -c Release --no-build --filter "FullyQualifiedName~ZzzNoSuchTest"` on SDK 10.0.401 exits **0** with only "No test matches the given testcase filter". A green `dotnet test` therefore does not prove tests ran.
- `dotnet list package --vulnerable --include-transitive --format json` works on 10.0.401 (verified, clean result, exit 0).
- The test suite has only ever run on Windows. It depends on the Warsaw time zone and a Polish comparer (`Pantry/ProductService.cs:47-51`, also called from `Program.cs:37`), and one assertion uses `Environment.NewLine` (`KitchenAssistant.Tests/Recipes/RecipePromptTests.cs:22`).
- Restore uses only nuget.org (`nuget.config`). `TestResults/` is not git-ignored.

## Desired End State

- `.github/workflows/ci.yml` runs on every PR to `main`, on pushes to `main` and via `workflow_call`. Its `gate` job fails on a build error, a pending model change, a failing test, or zero executed tests. Its `vulnerable-packages` job fails on a High or Critical advisory but is not required.
- An active `main` ruleset, defined in `.github/rulesets/main.json`, requires a PR (0 approvals) and the `gate` check from GitHub Actions with the branch up to date (strict). It blocks force-push and deletion, and nobody can bypass it.
- Three throwaway PRs (failing test, model change without migration, test project removed from the solution) were each shown red and `BLOCKED`, then closed unmerged.
- Test-plan §6.4 and §6.6, `deployment-plan.md` and `CLAUDE.md` describe the gate and how the future deploy depends on it.

Verify: `gh api repos/Ciepielewski-Cezar/10x-KitchenAssistant/rules/branches/main` shows the four rules, and the three proof PRs are closed with a failed `gate` check.

### Key Discoveries:

- Zero-test runs exit 0 on SDK 10.0.401 (local run above). The executed count must be asserted from the TRX output.
- A required check is matched by its check-run name, which for Actions is the job's `name`. Only the `pull_request` run of `ci.yml` satisfies the ruleset. When `deploy.yml` later calls `ci.yml` via `workflow_call`, its check appears as `<caller job> / gate`, which doesn't affect the PR requirement.
- The EF design-time host needs `ConnectionStrings:DefaultConnection` to exist (`Program.cs:29`), not to connect. The committed `appsettings.json` provides it. It's not yet proven on a machine without LocalDB (research Open Question 7). The first Linux CI run proves it.
- `--no-build` requires build and test to use the same configuration (research §2).
- Repo script convention is PowerShell (`scripts/local-prod.ps1`). The local machine has Windows PowerShell 5.1 and no `pwsh`. Ubuntu runners have `pwsh`. Scripts must run on both.

## What We're NOT Doing

- No `deploy.yml`, Azure OIDC, `production` environment, publish/artifact step or post-deploy smoke. These are deferred to the deployment change after test phases 2–4 (change.md scope decision).
- No warnings-as-errors, in CI or in the project. **Decision:** warnings stay warnings, so the build gate fails only on compiler errors. Test-plan §5's row "build (compiler + nullable warnings)" therefore overstates what is enforced. This is recorded in §6.6 and left for `/10x-test-plan --refresh`. §5 itself is not edited.
- No minimum test-count floor. The guard fails only when zero tests execute (decision). Losing one test class while others still run is not detected.
- No admin bypass and no PR-only bypass on the ruleset (decision).
- The vulnerable-package job does not block merges (decision).
- No Dependabot config, no NuGet caching (no lock file; the build takes under a minute), no SHA-pinned actions (the repo does not require them).
- No new tests and no test refactoring, beyond fixing a test that fails only because of a Linux environment assumption (see Critical Implementation Details).
- No hook configuration (the per-edit and end-of-turn hooks are a separate lesson).

## Implementation Approach

One small workflow with two jobs on `ubuntu-latest`: `gate` (required) and `vulnerable-packages` (advisory). Logic that must be provable locally goes into two PowerShell scripts under `scripts/ci/`: the TRX executed-count assertion and the High/Critical advisory check. Then the ruleset is committed as JSON and applied with `gh api` after explicit approval, once `gate` has reported on the change PR. The change PR becomes the first merge through the ruleset. Finally, three deliberately red PRs prove each false-green path is blocked, and the docs PR records the pattern.

Phases 1 and 2 share one branch and one PR (`claude/testing-ci-test-gate`). Phase 3 uses throwaway branches plus a docs branch.

## Critical Implementation Details

- **Ordering.** Apply the ruleset only after `gate` has reported at least once on the change PR. Merge the change PR only after the ruleset is active. Merges to `main` are done by the user (`infrastructure.md`: merges are human-only). Once the ruleset is active, `main` accepts changes only via PR. The uncommitted `.claude/**`, `.claude/prompts/` and 10x-managed `CLAUDE.md` block changes in the working tree are not part of this change. Tell the user before applying the ruleset that, from then on, they also need a PR.
- **Vulnerability exit code.** `dotnet list package --vulnerable` is documented to exit 0 even when it finds vulnerabilities, so the job must decide from the JSON. Fail on `severity` `High` or `Critical` under any project's `frameworks[].topLevelPackages[].vulnerabilities[]` or `frameworks[].transitivePackages[].vulnerabilities[]`. Print Low/Moderate findings without failing. Prove this with a hand-written sample JSON, since the live result is clean.
- **TRX counting.** Use `--logger trx --results-directory TestResults` without a fixed `LogFileName`, so several test projects never overwrite each other's file. The script sums `TestRun/ResultSummary/Counters/@executed` across all `*.trx` files in the directory. It fails with a clear message when the directory has no TRX files or the sum is 0, and prints the executed total when it passes.
- **First Linux run.** If the first `gate` run is red because a test assumes Windows (time zone ID, culture or `\r\n`), fix that test's environment assumption in this change. If the failure shows the app itself misbehaves on Linux, stop and open a separate change via `/10x-new`. Production runs on Linux, so that would be a real bug, not a CI problem.
- **Required-check identity.** The ruleset entry is `{ "context": "gate", "integration_id": 15368 }` (the GitHub Actions app), so a status from another integration cannot satisfy it. Confirm the id from the change PR's check run (`gh api repos/:owner/:repo/commits/<sha>/check-runs --jq '.check_runs[] | {name, app: .app.id}'`) before applying.
- **Proof PRs must not be drafts.** Draft PRs report `mergeStateStatus: DRAFT`, which hides `BLOCKED`. Open them as normal PRs titled `DO NOT MERGE: gate proof – …`.

## Phase 1: Gate Workflow

### Overview

Add the `ci.yml` workflow, the two CI scripts and the `TestResults/` ignore. Prove the scripts locally, then open the change PR so `gate` and `vulnerable-packages` run green on Linux.

### Changes Required:

#### 1. CI workflow

**File**: `.github/workflows/ci.yml`

**Intent**: The single, reusable definition of the merge gate. `deploy.yml` will later call it via `workflow_call` instead of duplicating build and test steps.

**Contract**:
- Workflow `name: CI`.
- Triggers: `pull_request` (branches `main`), `push` (branches `main`), `workflow_call`. No `paths` filters, because a filtered required check stays pending forever on PRs it skips.
- Workflow-level `permissions: contents: read`.
- Concurrency per workflow and ref, cancelling in-progress runs only for `pull_request` events.
- Job id and display name `gate`: `ubuntu-latest`, `timeout-minutes: 15`. Steps:
  - checkout and `setup-dotnet` (`dotnet-version: 10.0.x`), using the current major versions of the official actions (check them at implementation time)
  - `dotnet restore KitchenAssistant.slnx`
  - `dotnet build KitchenAssistant.slnx -c Release --no-restore`
  - `dotnet tool restore`, then `dotnet ef migrations has-pending-model-changes --no-build --configuration Release`
  - `dotnet test KitchenAssistant.slnx -c Release --no-build --logger trx --results-directory TestResults`
  - `pwsh scripts/ci/assert-tests-ran.ps1 -ResultsDirectory TestResults`, under `if: always()` only after the test step actually ran, so a test failure still reports the count without masking the red result
- Job id and display name `vulnerable-packages`: same runner and setup, restore, `dotnet list KitchenAssistant.slnx package --vulnerable --include-transitive --format json` to a file, then `pwsh scripts/ci/assert-no-vulnerable-packages.ps1 -Path <file>`. Independent of `gate` (no `needs`).

#### 2. Test-count guard

**File**: `scripts/ci/assert-tests-ran.ps1`

**Intent**: Turns "tests passed" into "tests passed and more than zero ran", closing the zero-test and missing-from-solution false greens.

**Contract**: Parameter `-ResultsDirectory`. Exit 0 and print `Executed tests: <n>` when the summed `Counters/@executed` across `*.trx` files is greater than 0. Exit non-zero with a message naming the cause (no TRX files, or 0 executed) otherwise. Must run on Windows PowerShell 5.1 and pwsh 7.

#### 3. Vulnerable-package check

**File**: `scripts/ci/assert-no-vulnerable-packages.ps1`

**Intent**: Gives the advisory job a real exit code, since the `dotnet list` command itself always exits 0.

**Contract**: Parameter `-Path` (the JSON report). List every vulnerable package with its severity and advisory URL. Exit non-zero only if any severity is `High` or `Critical`. Must run on Windows PowerShell 5.1 and pwsh 7.

#### 4. Ignore test output

**File**: `.gitignore`

**Intent**: Keep local proof runs from leaving TRX files in the working tree.

**Contract**: Add `TestResults/`.

### Success Criteria:

#### Automated Verification:

- Release build of the solution succeeds: `dotnet build KitchenAssistant.slnx -c Release`
- The test run writes a TRX and the guard passes, printing the executed count: `dotnet test KitchenAssistant.slnx -c Release --no-build --logger trx --results-directory TestResults` then `powershell -File scripts/ci/assert-tests-ran.ps1 -ResultsDirectory TestResults` exits 0
- The guard exits non-zero on an empty results directory
- The guard exits non-zero on a zero-executed run (`--filter "FullyQualifiedName~ZzzNoSuchTest"` into a fresh results directory)
- The vulnerability script exits 0 on the live clean report and non-zero on a sample JSON with one `High` transitive advisory
- On the change PR, both checks succeed: `gh pr checks <PR>` shows `gate` and `vulnerable-packages` as pass

#### Manual Verification:

- The `gate` log on the PR shows the EF message "No changes have been made to the model since the last migration." and an executed count matching the local run
- If the first Linux run was red, the cause was classified as a test-environment assumption (fixed here) or an app bug (separate change opened)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: `main` Ruleset

### Overview

Commit the ruleset definition, apply it after explicit approval, verify the live rules, and merge the change PR through it.

### Changes Required:

#### 1. Ruleset definition

**File**: `.github/rulesets/main.json`

**Intent**: A reviewable, re-appliable record of the branch protection that makes `gate` required. GitHub does not read this path. It is the source for `gh api`.

**Contract**: The REST ruleset body (same shape as the UI's import/export):
- `name: "main"`, `target: "branch"`, `enforcement: "active"`, `bypass_actors: []`
- `conditions.ref_name.include: ["~DEFAULT_BRANCH"]`
- rules: `deletion`, `non_fast_forward`, `pull_request` (`required_approving_review_count: 0`, other review options off), and `required_status_checks` (`strict_required_status_checks_policy: true`, `required_status_checks: [{ "context": "gate", "integration_id": 15368 }]`)

#### 2. Apply and verify

**File**: none (GitHub state)

**Intent**: Make the gate required on GitHub, with nobody able to bypass it.

**Contract**: After the user approves the exact command, create the ruleset with `gh api -X POST repos/Ciepielewski-Cezar/10x-KitchenAssistant/rulesets --input .github/rulesets/main.json`. Later edits use `PUT .../rulesets/<id>` with the same file. Before applying, remind the user that their uncommitted tool updates will need a PR afterwards (see Critical Implementation Details).

### Success Criteria:

#### Automated Verification:

- Exactly one active ruleset named `main` exists: `gh api repos/Ciepielewski-Cezar/10x-KitchenAssistant/rulesets`
- Effective rules on `main` include `deletion`, `non_fast_forward`, `pull_request` with 0 approvals, and `required_status_checks` with `gate`, integration 15368 and strict mode: `gh api repos/Ciepielewski-Cezar/10x-KitchenAssistant/rules/branches/main`
- The live ruleset's bypass list is empty: `gh api repos/Ciepielewski-Cezar/10x-KitchenAssistant/rulesets/<id> --jq '.bypass_actors'` prints `[]`

#### Manual Verification:

- The change PR page lists `gate` as Required, and the user merges the PR after it is green and up to date
- The `push` run of `CI` on the merge commit in `main` is green

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Gate Proof and Docs

### Overview

Prove that each false-green path is blocked by the live ruleset. Then record the pattern and the dependency contract for the future deploy, through a normal gated PR.

### Changes Required:

#### 1. Proof PRs (throwaway, never merged)

**File**: three branches off current `main`

**Intent**: Show end to end that the `gate` check and the ruleset block each failure the test plan names.

**Contract**:
- `proof/gate-failing-test`: add one test that asserts false. Expected red step: tests.
- `proof/gate-pending-model`: add a nullable property to an entity in `Data/` with no migration. Expected red step: pending model changes.
- `proof/gate-no-tests`: remove `KitchenAssistant.Tests` from `KitchenAssistant.slnx`. Expected red step: test-count guard.
- Each is opened as a non-draft PR titled `DO NOT MERGE: gate proof – <case>`. After verification, close it unmerged and delete its remote branch. Record the PR numbers for §6.6.

#### 2. Test plan cookbook

**File**: `context/foundation/test-plan.md`

**Intent**: Fill in the sections this phase owns so future phases know how to add a gate.

**Contract**:
- §6.4 "Adding a gate": a new check becomes a step in `gate` (required) or its own advisory job. The ruleset JSON changes only when a new required check name appears, and is re-applied with `PUT`. No path filters on required checks.
- Append a Phase 1 entry to §6.6: the zero-test exit-0 finding, the proof PR numbers, and the warnings-stay-warnings decision that §5 overstates (for `/10x-test-plan --refresh`).
- Do not edit §1–§5. The §3 Status column is updated by `/10x-test-plan`.

#### 3. Deployment plan alignment

**File**: `context/changes/deployment/deployment-plan.md`

**Intent**: Stop the deploy plan from re-creating a second gate or requiring the wrong check name.

**Contract**:
- Phase 1: mark the ruleset bullet as done by `testing-ci-test-gate`.
- Phase 5: `deploy.yml` calls `./.github/workflows/ci.yml` as a job (`uses:`). Its publish and deploy jobs `needs:` that job, and it keeps its own publish/artifact step. When `deploy.yml` lands, remove the `push` trigger from `ci.yml` so the gate doesn't run twice on `main`. Don't reuse `ci.yml`'s concurrency group. The required check is `gate`, already configured. Remove the stale "passes trivially until tests exist" note.

#### 4. Agent instructions

**File**: `CLAUDE.md` (outside the 10x-managed markers)

**Intent**: Future agents learn that `main` is PR-only and which check guards it.

**Contract**: Extend the **Deployment** bullet in *Architecture* with one or two sentences: `.github/workflows/ci.yml`'s `gate` job (Release build, pending-model check, tests with a zero-executed guard) is the required check in the `main` ruleset (`.github/rulesets/main.json`, no bypass, strict). `main` changes only via PR. `vulnerable-packages` is advisory.

### Success Criteria:

#### Automated Verification:

- Each proof PR has a failed `gate` check: `gh pr checks <n>` for all three
- Each proof PR reports `mergeStateStatus` `BLOCKED`: `gh pr view <n> --json mergeStateStatus`
- All three proof PRs are closed unmerged and their remote branches deleted: `gh pr view <n> --json state,mergedAt` shows `CLOSED` and null `mergedAt`
- The docs PR's `gate` and `vulnerable-packages` checks pass: `gh pr checks <docs PR>`

#### Manual Verification:

- On each proof PR, the failing step is the expected one (tests, pending model changes, test-count guard respectively) and GitHub disables the merge button
- Test-plan §6.4/§6.6, `deployment-plan.md` Phase 1/5 and the `CLAUDE.md` Deployment bullet read correctly, and the user merges the docs PR

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None added. The two CI scripts are proved by running them against real and hand-made inputs (Phase 1).

### Integration Tests:

- The existing 224-case suite runs on Linux for the first time in `gate`.
- The three proof PRs are the end-to-end tests of the gate plus the ruleset.

### Manual Testing Steps:

1. Open the change PR. Confirm both checks are green and `gate`'s log shows the EF "No changes" line and the executed count.
2. After the ruleset is applied, confirm the PR shows `gate` as Required, then merge.
3. For each proof PR, confirm the red step and the disabled merge button, then close it.

## Performance Considerations

Each run is a restore plus about 15 s build plus 2 s tests on a hosted runner, roughly 2 minutes. No caching is needed at this size.

## Migration Notes

None for data. After Phase 2, all work on `main`, including 10x context artifacts and tool updates, goes through a PR.

## References

- Related research: `context/changes/testing-ci-test-gate/research.md`
- Risk and gates: `context/foundation/test-plan.md:66` (Risk #2), `:80` (Phase 1), `:122-130` (Quality Gates)
- Planned deploy workflow: `context/changes/deployment/deployment-plan.md:188-204`; planned ruleset: `:77-84`
- Connection string requirement: `Program.cs:29`; globalization data: `Pantry/ProductService.cs:47-51`
- Script convention: `scripts/local-prod.ps1`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Gate Workflow

#### Automated

- [x] 1.1 Release build of the solution succeeds — d0a2959
- [x] 1.2 The test run writes a TRX and the guard passes, printing the executed count — d0a2959
- [x] 1.3 The guard exits non-zero on an empty results directory — d0a2959
- [x] 1.4 The guard exits non-zero on a zero-executed run — d0a2959
- [x] 1.5 The vulnerability script exits 0 on the live clean report and non-zero on a sample High advisory — d0a2959
- [x] 1.6 On the change PR, both checks succeed — d0a2959

#### Manual

- [x] 1.7 The gate log shows the EF "No changes" message and the expected executed count — d0a2959
- [x] 1.8 Any first-run Linux failure was classified and handled — d0a2959

### Phase 2: `main` Ruleset

#### Automated

- [x] 2.1 Exactly one active ruleset named main exists
- [x] 2.2 Effective rules on main include deletion, non_fast_forward, pull_request and strict required gate check
- [x] 2.3 The live ruleset's bypass list is empty

#### Manual

- [ ] 2.4 The change PR lists gate as Required and the user merges it
- [ ] 2.5 The push run of CI on the merge commit is green

### Phase 3: Gate Proof and Docs

#### Automated

- [ ] 3.1 Each proof PR has a failed gate check
- [ ] 3.2 Each proof PR reports mergeStateStatus BLOCKED
- [ ] 3.3 All three proof PRs are closed unmerged with branches deleted
- [ ] 3.4 The docs PR's checks pass

#### Manual

- [ ] 3.5 Each proof PR fails at the expected step and the merge button is disabled
- [ ] 3.6 Docs read correctly and the user merges the docs PR
