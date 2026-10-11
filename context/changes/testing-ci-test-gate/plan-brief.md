# CI Test Gate — Plan Brief

> Full plan: `context/changes/testing-ci-test-gate/plan.md`
> Research: `context/changes/testing-ci-test-gate/research.md`

## What & Why

Test-plan Risk #2: a merge to `main` can ship a regression (failing test, broken build, forgotten migration) because nothing checks it first. This change adds a required GitHub Actions gate and a no-bypass `main` ruleset, so a red PR cannot merge. It ships before the first Azure deploy, so the existing 224-case suite protects production from day one.

## Starting Point

The repo has no `.github/`, no workflows and no branch protection. Every gate command already passes locally. On SDK 10.0.401, though, `dotnet test` exits 0 when zero tests run (verified), so a plain green run doesn't prove anything was tested.

## Desired End State

Every PR to `main` runs `CI`. Its `gate` job (Release build, EF pending-model check, tests plus an "executed > 0" guard) is required by an active ruleset with no bypass and strict up-to-date mode. A separate `vulnerable-packages` job flags High/Critical advisories without blocking. Three throwaway PRs proved each false-green path is blocked. The future `deploy.yml` will call `ci.yml` via `workflow_call` instead of duplicating it.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Scope | Gate + ruleset only; deploy deferred | Deploy is blocked on Azure prerequisites, and the gate can protect `main` now | Change notes |
| Workflow shape | Standalone `ci.yml` with `workflow_call` | One definition: required on PRs now, a `needs:` target for deploy later | Change notes |
| Zero-test guard | Fail if the TRX is missing or executed = 0 | Closes the verified exit-0 false green with no number to maintain | Plan |
| Warnings | Stay warnings (no `-warnaserror`) | User choice; §5's "nullable warnings" wording is flagged for refresh | Plan |
| Ruleset | PR + `gate` required, strict, no bypass, no force-push/deletion | "Cannot reach main without green gate" becomes literally true | Plan |
| Vulnerable packages | Separate non-required job, fails on High/Critical | Visible signal without an overnight CVE blocking every merge | Plan |
| Proof | Three red PRs: failing test, pending model, tests removed from the solution | Proves each false-green path end to end through the real ruleset | Plan |
| Ruleset ownership | JSON in `.github/rulesets/main.json`, applied by agent via `gh api` after approval | Reproducible and diffable | Plan |
| Required check identity | `gate` pinned to the GitHub Actions app (15368) | Another integration cannot satisfy it | Plan |

## Scope

**In scope:**
- `.github/workflows/ci.yml` (`gate`, `vulnerable-packages`)
- `scripts/ci/assert-tests-ran.ps1`, `scripts/ci/assert-no-vulnerable-packages.ps1`
- `.gitignore` (`TestResults/`)
- `.github/rulesets/main.json`, applied live
- Three proof PRs, closed unmerged
- Test-plan §6.4/§6.6, deployment-plan Phase 1/5 alignment, `CLAUDE.md` Deployment bullet

**Out of scope:**
- `deploy.yml`, OIDC, `production` environment, post-deploy smoke
- Warnings-as-errors, a test-count floor, Dependabot, NuGet caching, SHA-pinned actions
- New tests (except fixing a Linux-only environment assumption), hooks, edits to test-plan §1–§5

## Architecture / Approach

`pull_request`/`push`/`workflow_call` → `ci.yml`. Job `gate`: restore → Release build → `ef migrations has-pending-model-changes` → `dotnet test --logger trx` → assert executed > 0. Job `vulnerable-packages`: `dotnet list package --vulnerable --format json` → fail on High/Critical. The ruleset requires check `gate` (Actions app), so only the PR run counts. Later, `deploy.yml` will add `uses: ./.github/workflows/ci.yml` and make deploy `needs:` it.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Gate workflow | `ci.yml` + two scripts, proved locally, green on the change PR | First Linux run exposes a time zone, culture or newline assumption in tests |
| 2. `main` ruleset | Active no-bypass ruleset from committed JSON; change PR merged through it | After it applies, every commit to `main` (incl. tool updates) needs a PR |
| 3. Proof and docs | Three red PRs shown blocked; cookbook, deploy plan and CLAUDE.md updated | Proof PR merged by mistake (mitigated: the gate blocks it, and titles say DO NOT MERGE) |

**Prerequisites:** `gh` authenticated as the repo admin; the user approves the ruleset `gh api` call and performs the merges.
**Estimated effort:** ~1 session across 3 phases (plus CI wait time).

## Open Risks & Assumptions

- The EF check is assumed to pass without LocalDB (it needs only the committed connection string). The first CI run proves it.
- `dotnet list package --vulnerable` is assumed to exit 0 even when it finds vulnerabilities, so the script decides from the JSON. This is proved against a sample report.
- With no bypass, a GitHub Actions outage blocks merges until the ruleset is edited by hand.
- The gate doesn't detect a whole test class silently disappearing while others still run (no floor, by decision).

## Success Criteria (Summary)

- A PR with a failing test, a pending model change, or no executed tests shows a red `gate` and cannot be merged by anyone.
- A clean PR goes green in about 2 minutes, and `main` accepts changes only through such PRs.
- The deployment plan and test-plan cookbook describe how the future deploy depends on `gate`.
