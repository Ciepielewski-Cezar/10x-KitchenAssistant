<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Production Composition and Access (Test-Plan Phase 2)

- **Plan**: context/changes/testing-production-composition-access/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-10-14
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

Automated criteria re-run on 2026-10-14: `dotnet build` OK; `dotnet test` 344/344; ProductionCompositionTests 9/9, AccessTests 5/5, AuthorizationPolicyTests 46/46; `dotnet ef migrations has-pending-model-changes` reports no changes; the smoke config lists setup + 2 tests; the E2E config lists only `tests/e2e`.

## Findings

### F1 — Manual smoke runs ticked done, but test-plan says pending

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Success Criteria
- **Location**: context/foundation/test-plan.md:81; plan.md Progress 4.4–4.6
- **Detail**: Progress marks 4.4 (red with an empty key on `local-prod -Reset`), 4.5 (green with a real key) and 4.6 (re-run reuses the account, no leftover product) as `[x]` — 75d4220. test-plan §3 row 2 still says "implemented (manual smoke runs pending)". The p4 commit and the epilogue share one timestamp (21:09:41), and neither commit message records a smoke run. These are the only proofs of the startup migration on SQL Server and of the real AI path, so they look rubber-stamped.
- **Fix A ⭐ Recommended**: Run the three smoke scenarios against local-prod now, then set test-plan §3 row 2 to "complete" and note the runs.
  - Strength: Produces the evidence the plan requires; the Progress ticks become true.
  - Tradeoff: About 10 minutes plus one or two real Anthropic calls; needs Docker and a valid key.
  - Confidence: HIGH — the scripts and config already exist (`--list` passes).
  - Blind spot: The smoke has never been seen running green in this review.
- **Fix B**: Untick 4.4–4.6 and keep the test-plan wording "pending".
  - Strength: Progress and docs agree immediately, at no cost.
  - Tradeoff: The change can't be called complete or archived until someone runs the smoke.
  - Confidence: HIGH — a documentation-only edit.
  - Blind spot: None significant.
- **Decision**: SKIPPED

### F2 — Minimal-API Identity endpoints have no authorization tests

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:42,90; KitchenAssistant.Tests/Http/AuthorizationPolicyTests.cs:113-125
- **Detail**: The coverage guard and route lists cover only `@page` components. Nothing pins that `PerformExternalLogin` and `PasskeyRequestOptions` stay anonymous, or that `Logout` and `PasskeyCreationOptions` stay protected. If someone drops `.AllowAnonymous()` from `PasskeyRequestOptions`, passkey sign-in on the login page breaks: it gets a 302 instead of JSON, and no test fails.
- **Fix**: Add a theory to `AuthorizationPolicyTests` that POSTs anonymously to each `/Account/*` minimal endpoint and asserts challenged or not challenged, using the same `ReturnUrl=` check as the page tests.
- **Decision**: FIXED — Added `Public_endpoint_does_not_challenge_anonymous_visitors` (PerformExternalLogin, PasskeyRequestOptions; sent with a real antiforgery token through the new `HttpSignIn.GetAntiforgeryTokenAsync`) and `Protected_endpoint_challenges_anonymous_visitors` (Logout, PasskeyCreationOptions, Manage/LinkExternalLogin, Manage/DownloadPersonalData). Deliberate break: removing `.AllowAnonymous()` from PasskeyRequestOptions turned it red. Suite: 350/350.

### F3 — Smoke traces would capture the production password and cookie

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: playwright.smoke.config.ts:30; tests/smoke/smoke.setup.ts:13-14,24-26
- **Detail**: With `trace: 'retain-on-failure'`, a failed setup writes `SMOKE_PASSWORD` (the `fill()` values) and the session cookie into `test-results/**/trace.zip`. That's harmless locally, but once F-01 runs the smoke in `deploy.yml` and uploads artifacts, the production smoke credentials leak into CI artifacts.
- **Fix**: Set `use: { trace: 'off' }` on the `smoke-setup` project, and add a line in deployment-plan's smoke step saying not to upload `test-results/` from the setup.
- **Decision**: FIXED — `smoke-setup` project now has `use: { trace: 'off' }` (with a comment). The deployment-plan smoke-step note now says to keep any uploaded `test-results/`/`playwright-report/` artifact short-lived, because the `smoke` project's traces still carry the session cookie. `--list` still passes.

### F4 — Weak oracles in three tests

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: KitchenAssistant.Tests/Http/ProductionCompositionTests.cs:32-38,50-67; KitchenAssistant.Tests/Http/AuthorizationPolicyTests.cs:96-98
- **Detail**:
  - `Unknown_effort_stops_startup` uses `Assert.ThrowsAny<Exception>` with no message check, so any startup failure passes it. The sibling `MaxProducts` test pins `InvalidOperationException` and the message.
  - The missing-connection-string test checks only `ex.ToString()` contains "DefaultConnection", not the exception type.
  - `Public_page_does_not_challenge_anonymous_visitors` also passes on a 500.
- **Fix**: Use `Assert.Throws<InvalidOperationException>` and assert the message contains `Effort` / `DefaultConnection`. In the public-page theory, also assert the status is below 500.
- **Decision**: FIXED —
  - Effort and missing-connection-string tests now pin `InvalidOperationException` and check the message (`Recipes:Effort 'extreme'`, `DefaultConnection`).
  - The public-page theory now asserts status < 500.
  - That exposed a pre-existing template bug: anonymous `/Account/LoginWith2fa` and `/Account/LoginWithRecoveryCode` throw "Unable to load two-factor authentication user." → 500. On the user's decision, both are exempted from the < 500 check (`ThrowWithoutTwoFactorUser`, with a comment), and fixing the pages is a follow-up.
  - Suite: 350/350.

### F5 — Blazor hub endpoints sit behind the fallback policy

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Program.cs:109; CLAUDE.md (Hard rules, "Pages are private by default")
- **Detail**: The `_blazor` hub and negotiate endpoints are not anonymous. This is fine today because only `[Authorize]` pages are interactive. A future public `@rendermode InteractiveServer` page would render but never connect for anonymous visitors, and the CLAUDE.md rule doesn't warn about it.
- **Fix**: Add one clause to the CLAUDE.md bullet: "a public page that needs interactivity also needs the Blazor hub opened to anonymous users; all public pages are static SSR today".
- **Decision**: FIXED — The CLAUDE.md "Pages are private by default" bullet now says every public page is static SSR and the `_blazor` hub sits behind the fallback policy.

### F6 — Logout with an expired session ends on the not-found page

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:44
- **Detail**: The plan kept `Logout` under the fallback policy on purpose. As a result, a user whose cookie expired who clicks Logout goes to `/Account/Login?ReturnUrl=%2FAccount%2FLogout`, and after signing in is redirected (GET) to a POST-only route. That gives a 405, re-executed as `/not-found`. This is cosmetic only.
- **Fix**: Add `.AllowAnonymous()` to `Logout` (signing out an anonymous user is a no-op). Move it to the public side of the F2 endpoint theory.
- **Decision**: FIXED —
  - `Logout` now has `.AllowAnonymous()`, which departs from the plan's Phase 3 §3 "Logout stays under the fallback policy".
  - `/Account/Logout` moved to `PublicEndpoints`.
  - The endpoint theories now post `returnUrl=products`.
  - Suite: 350/350.

### F7 — Documented extras: UseStaticWebAssets and pre-confirmed seeded users

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: KitchenAssistant.Tests/Support/ProductionAppFactory.cs:61-63; KitchenAssistant.Tests/Support/HttpSignIn.cs:15-26
- **Detail**: Both changes are benign and recorded in commit messages and test-plan §6.6:
  - **UseStaticWebAssets.** The factory adds `UseStaticWebAssets()` (not in the plan: "only the database swapped"), so the static-asset test proves the test-host layout, not the published one. The smoke covers the published case.
  - **Pre-confirmed users.** `SeedUserAsync` creates users with `EmailConfirmed = true`, so the Production `RequireConfirmedAccount=false` setting, which the plan named as a Key Discovery, is no longer pinned by any HTTP test. Only the smoke's registration path relies on it.
- **Fix**: Add a short "Deviations" addendum to plan.md listing both, so later reviews treat them as decided.
- **Decision**: FIXED — plan.md has a new "## Deviations (addendum, 2026-10-14)" section, just before Progress. It lists these two items, the ReturnUrl-based public check, and the review-time changes (F2, F3, F4 exemption, F6).

### F8 — Smoke setup can silently register a stray production account

- **Severity**: 💡 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: tests/smoke/smoke.setup.ts:22-28
- **Detail**: On "Invalid login attempt." the setup registers `SMOKE_USERNAME`. If the secret is mistyped, or has a stale case or whitespace, each deploy run creates a new production account and passes green, which hides the misconfiguration. The plan chose this auto-register on purpose ("register it on a fresh environment").
- **Fix**: Register only when an explicit `SMOKE_ALLOW_REGISTER=1` is set (local-prod `-Reset` runs). Otherwise fail with "smoke account cannot sign in".
  - Strength: A deploy smoke can't create accounts, and a bad secret turns the run red.
  - Tradeoff: One more variable in `.env.example`/test-stack, and the first deploy needs the account registered once by hand (or with the flag).
  - Confidence: MED — a small change, but it alters the plan's "fresh environment" ergonomics.
  - Blind spot: Whether F-01 will deploy to an empty production database that needs the auto-register.
- **Decision**: FIXED — `smoke.setup.ts` now registers only when `SMOKE_ALLOW_REGISTER=1`, and otherwise throws "The smoke account cannot sign in…". Updated `.env.example`, test-stack `## Smoke` (variables, projects, local run) and the deployment plan's Phase 4 secrets note (register once by hand, never set the flag in `deploy.yml`). `--list` still passes. Not run against a live stack.

## Triage summary

- Fixed: F2, F3, F4, F5, F6, F7, F8 (7)
- Skipped: F1 (1)
- Follow-up: the scaffold pages `LoginWith2fa`/`LoginWithRecoveryCode` throw → 500 for anonymous visitors (exempted in the test).
