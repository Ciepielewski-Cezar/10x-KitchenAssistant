---
date: 2026-10-11T18:15:28+02:00
researcher: Cezar Ciepielewski
git_commit: b1b1241bbe2369d3adc7ec076514cb6c27360ac6
branch: main
repository: 10x-KitchenAssistant
topic: "CI test gate: what exists and what must be true so no merge to main reaches production without green build, green tests and no pending model changes (test-plan Phase 1, Risk #2)"
tags: [research, ci, github-actions, quality-gates, deployment, ef-core]
status: complete
last_updated: 2026-10-11
last_updated_by: Cezar Ciepielewski
---

# Research: CI test gate (test-plan §3 Phase 1, Risk #2)

**Date**: 2026-10-11T18:15:28+02:00
**Researcher**: Cezar Ciepielewski
**Git Commit**: b1b1241bbe2369d3adc7ec076514cb6c27360ac6
**Branch**: main (working tree has uncommitted changes under `.claude/`, `CLAUDE.md`, `context/foundation/test-plan.md`; none touch app or test code)
**Repository**: Ciepielewski-Cezar/10x-KitchenAssistant

## Research Question

The four questions the test plan assigns to research for Risk #2 (`context/foundation/test-plan.md:66`):

1. Does a workflow exist yet?
2. How is deploy triggered?
3. What branch ruleset and required checks exist?
4. Does the test project run from the solution file?

Plus: what do the gates in test-plan §5 (build, pending model changes, tests, vulnerable packages) actually do when run locally as CI would run them?

## Summary

- **No CI exists.** `.github/` is absent from the working tree, and the GitHub API reports `total_count: 0` workflows for the repo. Actions are enabled with all actions allowed.
- **No deploy exists either.** The deployment plan (`deployment-plan.md`) has all Azure/OIDC phases not started. The only workflow it describes (`deploy.yml`, Phase 5) is unwritten, so "deploy is triggered by merge to main" is currently a plan, not behaviour.
- **No protection on `main`.** The rulesets API returned `[]` and the classic branch-protection API returned 404 "Branch not protected". The `production` environment query returned no names. The authenticated user has `ADMIN` on a `PUBLIC` repo, so rulesets and environments are available without a paid plan.
- **Every gate command already passes locally on this commit** (Release build 0 warnings/0 errors; 224/224 tests; `has-pending-model-changes` exit 0; no vulnerable packages), so a first CI run should start green. None of them needs SQL Server, LocalDB or an Anthropic key.
- **The test project runs from the solution file**, but see the zero-test and test-discovery cautions below.
- **Main design tension for the plan:** the deployment plan puts the gate inside `deploy.yml` as a `build` job. The test plan wants an independent required check that deploy depends on. These need one coherent shape (see Open Questions).

## Detailed Findings

### 1. Workflow and deploy trigger: neither exists

- The repo root listing has no `.github` directory (verified with `ls .github` → "No such file"). `git log -- .github` printed nothing, so it was never committed on the inspected history.
- `gh api repos/Ciepielewski-Cezar/10x-KitchenAssistant/actions/workflows` → `{"total_count":0,"workflows":[]}`; `actions/permissions` → `enabled: true, allowed_actions: all, sha_pinning_required: false`.
- Roadmap F-01 status is `ready`, and its snapshot line says "brak pipeline'u CI/CD" (`context/foundation/roadmap.md:73`, `:44`).
- The only specified pipeline is the planned `deploy.yml` (`deployment-plan.md:188-204`): triggers `pull_request`→main (build only), `push`→main (build + deploy), `workflow_dispatch`; job `build` (restore, `build -c Release`, `tool restore` + `ef migrations has-pending-model-changes`, `test -c Release --no-build`, publish, upload artifact); job `deploy` with `needs: build`, `environment: production`, OIDC login, `webapps-deploy`, curl smoke on `/healthz`.
- That plan's step 4 note says `dotnet test` "passes trivially until `KitchenAssistant.Tests` exists" (`deployment-plan.md:197`). **Stale:** the test project exists and is in the solution (see §4).
- Stale also: `deployment-plan.md:9` says the GitHub remote "does not exist yet". It exists, is public, has default branch `main`, and PR #24 was merged at 2026-10-09T21:36:37Z (`gh pr list`). The Phase 1 ruleset and `production` environment steps (`:77-84`) are still not done (see §3).

### 2. Gate commands, run locally on this commit

All run from the repo root with SDK 10.0.401 (`global.json` pins 10.0.100 with `rollForward: latestFeature`, `global.json:3-4`).

| Gate (test-plan §5) | Command run | Observed result |
|---|---|---|
| build | `dotnet build -c Release --no-restore` | Succeeded, 0 warnings, 0 errors, 13.7 s (both projects built) |
| tests | `dotnet test -c Release --no-build` and `dotnet test KitchenAssistant.slnx -c Release --no-build` | Each: 1 test assembly, Passed 224, Failed 0, Skipped 0, ~1–2 s |
| pending model changes | `dotnet tool restore` then `dotnet ef migrations has-pending-model-changes --no-build` | "No changes have been made to the model since the last migration."; exit 0 |
| vulnerable packages | `dotnet list package --vulnerable --include-transitive` | No vulnerable packages in either project |

Findings that affect the workflow:

- **No database or secret is needed.** The tests use in-memory SQLite (`KitchenAssistant.Tests/ProductServiceTests.cs:9` comment; csproj references `Microsoft.EntityFrameworkCore.Sqlite`, `KitchenAssistant.Tests.csproj:12`). A grep of the test project for LocalDB, SqlServer, `ANTHROPIC` and `Environment.GetEnvironmentVariable` found none besides that comment.
- **The EF check needs a connection string to exist, not to connect.** `Program.cs:29` throws if `ConnectionStrings:DefaultConnection` is missing; the committed `appsettings.json` supplies the LocalDB string, so the check works on a clean runner. This run did not prove it runs with no SQL Server present on a clean machine, only on this one (which has LocalDB). The EF design-time path stops before the startup-migration block at `Program.cs:79-84` per the deployment plan's own note (`deployment-plan.md:135`); not independently verified here.
- **`dotnet tool restore`** works from `dotnet-tools.json` at the repo root (dotnet-ef 10.0.12, per `CLAUDE.md`).
- **`--no-build` with `-c Release`** works after a Release build; a CI `dotnet test` without a prior matching build would rebuild or fail to find the Debug output, so the build and test steps must use the same configuration.
- **Warnings are not errors.** Neither csproj sets `TreatWarningsAsErrors` or `WarningsAsErrors` (`KitchenAssistant.csproj:3-13`, `KitchenAssistant.Tests.csproj:3-8`), no `Directory.Build.props`/`.editorconfig` exists at the root. The §5 gate says "compiler + nullable warnings", but today a nullable warning would not fail the build. Currently 0 warnings, so enabling `-warnaserror` (or the property) on the CI command line would pass.

### 3. Branch rules and required checks: none

- `gh api repos/.../rulesets` → `[]`.
- `gh api repos/.../branches/main/protection` → HTTP 404 "Branch not protected".
- `gh api repos/.../environments` returned no environment names.
- Repo settings from `gh api repos/...`: `allow_merge_commit: true`, `allow_squash_merge: true`, `allow_auto_merge: false`, `delete_branch_on_merge: false`; secret scanning and push protection enabled; Dependabot security updates disabled.
- Practical consequence: the maintainer can push directly to `main` today. A required status check is only effective if the ruleset also applies to admins (no bypass) or the maintainer accepts bypass.
- Merge history shows PRs are in use (PRs #20–#24 merged on 2026-10-08/09), so a "require PR" rule matches the existing habit.
- A required check in a ruleset is matched by its job/check name. The deployment plan notes to "add `build` as a required status check after the first PR run" (`deployment-plan.md:204`); the check name must be settled before the ruleset is created.

### 4. Test project and solution

- `KitchenAssistant.slnx` lists both `KitchenAssistant.Tests/KitchenAssistant.Tests.csproj` and `KitchenAssistant.csproj` (lines 2–3). `dotnet sln list` shows both.
- `dotnet test` with no path at the repo root (which contains both a `.csproj` and a `.slnx`) ran successfully against the test assembly; `dotnet test KitchenAssistant.slnx` gave the same result. For CI, pass the solution path explicitly rather than relying on this.
- 146 `[Fact]`/`[Theory]` attributes in test sources; 224 executed cases (theories expand). `--list-tests` enumerates tests from `KitchenAssistant.Tests.dll`.
- **Zero-test false green (the "must challenge" in the test plan):** whether `dotnet test` exits non-zero when it discovers no tests was not exercised here, so a zero-test run may be green. The plan should either assert a minimum executed count (for example parse the TRX or console total) or rely on the test project being present in the solution plus a deliberate failing-test proof. Not verified which behaviour .NET 10 SDK 10.0.401 gives; this is an Open Question.
- The app project excludes the test folder from its default globs (`KitchenAssistant.csproj:12`), so building the solution does not double-compile test sources.

### 5. Relationship to the deployment change

- Both changes want a workflow that builds and tests. The deployment plan (`:188-204`) owns `deploy.yml`; the test plan Phase 1 says the "deploy depends on the gate" (`test-plan.md:66`) and lists the anti-pattern "a deploy job that does not depend on it".
- Deployment Phase 5 is sequenced after Azure provisioning (Phases 0–4, all ⬜). Phase 1 of the test plan therefore can (and per `test-plan.md:85-87` should) land first, ahead of any deploy job.
- `.github/dependabot.yml` is in the deployment plan's Phase 2 file list (`:106`) but is absent from the tree, so it is not yet present.

## Code References

- `context/foundation/test-plan.md:66` – Risk #2 response row: what proves protection, what research must ground, anti-patterns.
- `context/foundation/test-plan.md:80` – Phase 1 "CI test gate" row, status not started.
- `context/foundation/test-plan.md:122-130` – Quality Gates table (build, pending model changes, tests required after Phase 1; vulnerable check recommended).
- `context/changes/deployment/deployment-plan.md:77-84` – planned ruleset and `production` environment (not done).
- `context/changes/deployment/deployment-plan.md:188-204` – planned `deploy.yml` with `build` and `deploy` jobs.
- `context/changes/deployment/deployment-plan.md:134` – forgotten-migration failure mode and the CI gate that is meant to catch it.
- `KitchenAssistant.slnx:2-3` – both projects registered.
- `KitchenAssistant.csproj:3-13` – net10.0, Nullable enabled, no warnings-as-errors; test-folder exclusion at line 12.
- `KitchenAssistant.Tests/KitchenAssistant.Tests.csproj:10-17` – xunit 2.9.3, SQLite, TimeProvider.Testing, coverlet.collector.
- `Program.cs:29` – connection string required at build of the host; `Program.cs:79-84` – startup migration behind `Database:MigrateOnStartup`.
- `global.json:3-4` – SDK pin.

## Architecture Insights

- The repo is a single app project plus one test project, so one `build` + `test` job on one runner covers the full gate; the whole local sequence takes under a minute of compute (13.7 s build, 1–2 s tests) plus restore.
- Gates that need no external resource (build, tests, EF model check, vulnerability scan) can run on every PR. The only gate that needs Azure is the post-deploy smoke, which test-plan §5 already assigns to Phase 3.
- Today deploy is assumed to trigger from `main` pushes. A gate that is a *separate* required check on PRs protects against merging a red PR; a gate that is also a `needs:` dependency of the deploy job protects against deploying a red commit pushed or merged around the check. The plan needs both to satisfy "cannot reach production".

## Historical Context (from prior changes)

- `context/changes/deployment/deployment-plan.md` – defines the intended pipeline; claims "no tests yet" (contradicted: 224 tests exist, supported: no `.github/` exists) and "remote does not exist on GitHub" (contradicted: repo exists and is public). Claim that `has-pending-model-changes` needs no database is partially supported by this run (passed here with LocalDB installed; not proven without it).
- `context/foundation/roadmap.md:73,78-92` – F-01 owns the deploy pipeline and its blockers (PAYG upgrade, quota, prod API key); those are human-only per the deployment plan, so a deploy-dependent gate cannot be proven end to end yet. The CI-only part can ship now.
- `context/foundation/infrastructure.md:90` – "PRs run build + tests in GitHub Actions only; first preview is production after merge" – consistent with a PR-time check being the only pre-prod protection.
- `context/archive/2026-10-03-first-recipe-generation/` – source of the "Fake generator stays Development-only" rule; not relevant to this phase except that CI tests must not need an Anthropic key (confirmed none do).

## Related Research

None for CI. `context/changes/deployment/deployment-plan.md` is the closest prior material; there is no `research.md` in that change.

## Open Questions

1. **Workflow shape.** One workflow shared with deploy (reusable `workflow_call` job, or a `needs:` dependency inside `deploy.yml`) versus a standalone `ci.yml` that runs on `pull_request` and `push` and is also required. Decide before writing, because the required check name and the later deploy dependency both depend on it.
2. **Zero-test detection.** Does `dotnet test` on SDK 10.0.401 fail when no tests are discovered? Not exercised. Decide between a minimum-count assertion and relying on the proof run with a deliberately failing test.
3. **Warnings as errors.** Enable `-warnaserror` (or `TreatWarningsAsErrors`) in CI to make "compiler + nullable warnings" true? Currently 0 warnings, so it passes now, but it changes behaviour for future work.
4. **Ruleset enforcement level.** Require a PR with 0 approvals and the status check, with or without admin bypass? The deployment plan chose 0 approvals for a solo developer (`deployment-plan.md:78`); bypass policy is undecided. Rulesets are applied by the owner (human-only per the deployment plan).
5. **Trigger scope.** Run on every PR and on pushes to `main`, with no path filters? A path-filtered workflow leaves a required check pending forever on PRs that skip it, so the plan should avoid path filters on the required check.
6. **Vulnerable-package gate.** Test-plan marks it recommended, not required. Should it block the PR, or run as a non-blocking job? It depends on live NuGet advisories, so it can turn red without any code change.
7. **EF check on a clean runner.** Confirm on the first CI run (or a clean container) that `has-pending-model-changes` passes without LocalDB present.
8. **Proof of the gate.** How the phase proves a red PR is blocked: a throwaway branch with a failing test, and one with a forgotten migration, each opened as a PR to confirm the merge button is disabled. Needs the ruleset to exist first.
