# Test Plan

> Phased test rollout for this project. Strategy is frozen at the top
> (§1–§5); cookbook patterns at the bottom (§6) fill in as phases ship.
> Read before writing any new test.
>
> Refresh: re-run `/10x-test-plan --refresh` when stale (see §8).
>
> Last updated: 2026-10-11

## 1. Strategy

Tests follow three non-negotiable principles for this project:

1. **Cost × signal.** The cheapest test that gives a real signal for the
   risk wins. Do not promote to e2e because e2e "feels safer." Do not put a
   vision model on top of a deterministic visual diff that already catches
   the regression.
2. **User concerns are first-class evidence.** Risks anchored in "the
   team is worried about X, and the failure would surface somewhere in
   <area>" carry the same weight as PRD lines or hot-spot data.
3. **Risks are scenarios, not code locations.** This plan documents *what
   could fail* and *why we believe it's likely* — drawn from documents,
   interview, and codebase *signal* (churn, structure, test base). It does
   NOT claim to know which line owns the failure. That knowledge is
   produced by `/10x-research` during each rollout phase. If the plan and
   research disagree about where the failure lives, research is the
   ground truth.

Hot-spot scope used for likelihood weighting: `Recipes/`, `Pantry/`,
`Components/` (Pages, Ui, Account, Layout), `Data/` (excluding
`Data/Migrations/`), `Program.cs`, `wwwroot/app.css`. Scan window: 30 days to
2026-10-11, 31 commits in scope.

Oracles come from the PRD (*Business Logic*, *Access Control*), the
interview, or a hand-written expectation table — never from the code under
test. Real Anthropic calls never run in automated tests; the generator is
replaced at the boundary.

## 2. Risk Map

The top failure scenarios this project must protect against, ordered by
risk = impact × likelihood. Risks are failure scenarios in user / business
terms, not test names. The Source column cites the *evidence that surfaced
this risk* — never a specific file as "where the failure lives" (that is
research's job, see §1 principle #3).

| # | Risk (failure scenario) | Impact | Likelihood | Source (evidence — not anchor) |
|---|---|---|---|---|
| 1 | The AI returns malformed, truncated, refused, over-limit or unusable output, or the call times out, and the user gets an error page, a frozen interactive page or a silent empty list instead of a clear, retryable state. | High | High | interview Q1; PRD *Guardrails* (structured recipe); archive/2026-10-03-first-recipe-generation (parser, deadline, cancellation), archive/2026-10-08-recipe-ranking and archive/2026-10-09-meal-parameters (outcome ordering, "none within limits"); hot-spot dirs `Components/Pages` (18 commits/30d), `Recipes/` (10 commits/30d) |
| 2 | A merge to `main` deploys a regression (failing test, broken build, pending model change) because no automated gate runs before deploy. | High | High | interview Q4; roadmap F-01 (auto-deploy on merge); deployment plan risk table (pending-migration gate planned, not wired); no CI workflow in repo |
| 3 | The app works locally but breaks on Azure in Production (missing or invalid AI key found at first click, startup migration failure, Fake generator active, interactive page cannot connect), while `/healthz` stays green. | High | High | interview Q2 (no prior cloud deployment experience); roadmap F-01 not shipped, deadline 2026-10-22; archive/2026-10-03-first-recipe-generation (late key failure, Fake must stay Development-only); hot-spot `Program.cs` (5 commits/30d) |
| 4 | Abuse (access): an anonymous visitor reaches the products or recipes page, or a signed-in user reads or changes another user's products through the page flow rather than the service. | High | Medium | PRD *Access Control*; archive/2026-10-03-pantry-add-products and archive/2026-10-08-pantry-edit-remove (privacy proved at service level, pages verified manually, anonymous redirect checked by curl only) |
| 5 | The visible score ("Masz X z Y", missing count, use-first count), order or hidden-proposal set is wrong, so the user picks a "no shopping" recipe that actually needs shopping. | Medium | Medium | PRD *Business Logic*; archive/2026-10-08-recipe-ranking (review found duplicated owned lines inflating X); archive/2026-10-09-meal-parameters (time-limit boundaries); hot-spot dir `Recipes/` |
| 6 | Abuse (cost and input): one generation request costs more than designed because caps are bypassed through configuration or long or prompt-shaped product names distort the prompt; repeated clicks multiply it. | Medium | Medium | archive/2026-10-03-first-recipe-generation reviews (unbounded product count, unvalidated limits, delimiter injection, no per-user throttle); tech-stack `has_ai: true` |

High-impact items that belong to observability, not tests: total spend
across many requests (no throttle exists to test; covered by the F-01
budget alert) and the ≤ 1 min latency NFR (see §7).

### Risk Response Guidance

| Risk | What would prove protection | Must challenge | Context `/10x-research` must ground | Likely cheapest layer | Anti-pattern to avoid |
|------|-----------------------------|----------------|--------------------------------------|-----------------------|-----------------------|
| #1 | Every generation outcome (failed, timed out, cancelled, no usable proposals, none within limits, success) reaches the user as a distinct, recoverable state; no outcome surfaces as an unhandled exception, a dead circuit or a blank list. | "Parser unit tests pass, so the user sees a clean state." "An empty list means the AI found no recipes" — it may be a swallowed failure. | How each service outcome maps to page state; what an unhandled exception does to an interactive server page; which odd model outputs (truncation, refusal, extra recipes, missing fields) are already covered. | Component test of the recipes page with a scripted fake generator; unit tests only for uncovered parser/outcome gaps. | Asserting the page renders whatever the page code produces instead of the PRD-expected state; happy-path-only. |
| #2 (outside rollout — owned by deployment/CI work, see §3) | A PR with a failing test, a broken build or a pending model change cannot reach production; the deploy depends on the gate. | "The workflow is green, so tests ran" — a zero-test run or a test project missing from the solution is also green. | Whether a workflow exists yet; how deploy is triggered; branch ruleset and required checks; whether the test project runs from the solution file. | CI required status check. | A gate that runs but is not required, or a deploy job that does not depend on it. |
| #3 | The Production composition fails loudly at startup on bad or missing config, never selects the Fake generator, and a post-deploy check exercises more than `/healthz` (a signed-in page and the interactive connection). | "`/healthz` 200 means the app works." "Development wiring tested means Production wiring works." | How the Production environment is assembled; config validation and fail-fast behaviour; startup migration path; what the deployed app needs for the interactive connection. | Integration test with a Production-environment test host; post-deploy smoke. | Testing only the Development composition; a smoke that hits only liveness. |
| #4 | Anonymous requests to the products and recipes pages redirect to login; with two seeded users, user B's products never appear in, and are never changed by, user A's session. | "The service filters by user, so the page is safe" — the page may pass the wrong identity. | Where pages obtain the current user; authorization attributes and fallback policy; how a test host can authenticate as a seeded user. | HTTP-level integration test with two seeded users. | Mocking the identity lookup so the test proves nothing about the real pipeline. |
| #5 | Scores, order and hidden set match a hand-written oracle table derived from PRD rules: "always at home" exclusions, max 2 missing, distinct owned products, unknown IDs never owned, time-limit edges. | "The existing ranker and classifier tests are correct" — their expected values may have been copied from the implementation. | Existing ranking, classification and score tests compared rule by rule against the PRD. | Unit. | Oracle problem: expected values computed by the code under test. |
| #6 | Per-request caps (product count, token budget, retries, deadline) hold under misconfiguration and hostile product names; a name cannot add or alter prompt fields. | "Review fixes landed, so caps hold." "Names are just data." | Where caps are enforced and validated; how names are normalised before reaching the prompt. | Unit. | Asserting that a sanitizer was called instead of asserting what the prompt contains. |

## 3. Phased Rollout

Each row is a discrete rollout phase that will open its own change folder
via `/10x-new`. Status moves left-to-right through the values below; the
orchestrator updates Status as artifacts appear on disk.

| # | Phase name | Goal (one line) | Risks covered | Test types | Status | Change folder |
|---|---|---|---|---|---|---|
| 1 | AI failure surfacing | Prove every AI outcome lands on the recipes page as a clear, recoverable state and per-request caps hold | #1, #6 | unit + component | not started | — |
| 2 | Production composition and access | Prove Production wiring fails fast, access is enforced end-to-end, and the deployed app works beyond liveness | #3, #4 | integration + post-deploy smoke (optional agent-driven browser smoke) + optional e2e sub-phase | not started | — |
| 3 | Scoring oracle audit | Prove score, order and hidden-set tests use PRD-derived expectations rather than implementation-copied values | #5 | unit | not started | — |

Order rationale: Phase 1 addresses the top interview concern in the
highest-churn area. Phase 2 lands alongside F-01. Phase 3 has the lowest
marginal risk because unit coverage is already broad.

Phase 2 optional e2e sub-phase (decision 2026-10-11): one Playwright test of
the critical flow — sign in → add a product → generate recipes → open
details — against the local app with the fake generator (no AI cost). It
proves what component and HTTP tests cannot: the flow works across pages
over the live interactive connection in a real browser. Plan it as the last
test sub-phase of Phase 2 and drop it if the earlier sub-phases already give
that signal. Keep it to this one flow; edge and error states stay in
component and integration tests.

Risk #2 (no gate before deploy) is deliberately outside this rollout: the
owner will wire CI in a separate phase of the application work (F-01 /
deployment plan, CI/CD pipeline phase). Decision 2026-10-11. Until then,
the tests added here are run locally only.

## 4. Stack

The classic test base for this project. AI-native tools (if any) carry a
`checked:` date so future readers can see which lines need re-verification.
Recommendations in this section must be grounded in local manifests/configs
plus the MCP/tools actually exposed in the current session.

Test-base profile: **sparse** — xUnit project with 15 test files, 13 of them
under `Recipes/`; services covered on SQLite in-memory with
`FakeTimeProvider`; no page/component, HTTP-pipeline or e2e tests; no CI.

| Layer | Tool | Version | Notes |
|---|---|---|---|
| unit + integration (service) | xUnit + EF Core SQLite in-memory + `Microsoft.Extensions.TimeProvider.Testing` | xunit 2.9.3, EF Core Sqlite 10.0.12, TimeProvider.Testing 10.10.0 | Existing; `KitchenAssistant.Tests`, registered in `KitchenAssistant.slnx`. |
| AI boundary | Fake recipe generator (in-app, Development-only) | n/a | Replaces Anthropic at the boundary; no real-API tests in automation. |
| component | bUnit — checked: 2026-10-11 | bunit 2.11.3 | Interactive page states without a browser. Shipped in Phase 1; support code in `KitchenAssistant.Tests/Support/`. |
| mutation (selective gate) | Stryker.NET (`dotnet-stryker`) — checked: 2026-10-11 | 5.0.0 (local tool) | Ad hoc after a risk phase, scoped with `--mutate`; not a per-commit gate. |
| HTTP integration | `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) with a test auth scheme — checked: 2026-10-11 | none yet — see Phase 2 | Real middleware, authorization and DI composition. |
| e2e | Playwright for .NET — checked: 2026-10-11 | none yet — see Phase 2 (optional sub-phase) | One critical-flow test against the local app with the fake generator; not a suite. |
| (optional) AI-native | Agent-driven browser smoke (built-in browser / Playwright MCP) — checked: 2026-10-11 | n/a | Post-deploy only: sign in, generate recipes over the live interactive connection. When NOT to use: per-PR CI, or any state bUnit already proves. |

**Stack grounding tools (current session):**
- Docs: Context7 — confirmed `WebApplicationFactory` + test authentication handler pattern (aspnetcore.docs) and bUnit docs availability; checked: 2026-10-11
- Search: web search available, not used — official docs via Context7 were sufficient; checked: 2026-10-11
- Runtime/browser: built-in browser pane and Claude in Chrome available — candidate for the optional Phase 2 post-deploy smoke; checked: 2026-10-11
- Provider/platform: GitHub via `gh` CLI (repo and PRs exist; no workflows yet) — relevant to the CI work owned by deployment (outside this rollout); Linear needs authorization, not used; checked: 2026-10-11

## 5. Quality Gates

The full set of gates that must pass before a change reaches production.
"Required after §3 Phase <N>" means the gate is enforced once that rollout
phase lands; before that, the gate is `planned`.

| Gate | Where | Required? | Catches |
|---|---|---|---|
| build (compiler + nullable warnings) | local; CI owned by deployment work | required locally now; CI outside this rollout | syntactic / type drift |
| pending model changes check | local; CI owned by deployment work | required locally before commit (CLAUDE.md); CI outside this rollout | forgotten migration that would stop startup |
| unit + component + integration tests | local; CI owned by deployment work | required locally after §3 Phase 1 (grows with Phases 2–3); CI outside this rollout | logic, page-state and access regressions |
| e2e on the critical flow (local, fake generator) | local | optional after §3 Phase 2 | cross-page flow breaking over the live interactive connection |
| post-deploy smoke (signed-in page + interactive connection) | after deploy | required after §3 Phase 2 | environment-specific failures liveness misses |
| post-edit hook (affected tests) | local (agent loop) | recommended after §3 Phase 1 | regressions at edit time |

CI wiring for build, tests and the pending-model-changes check belongs to
the deployment work (risk #2), not to this rollout.

## 6. Cookbook Patterns

How to add new tests in this project. Each sub-section is filled in once
the relevant rollout phase ships; before that, the sub-section reads
"TBD — see §3 Phase <N>."

### 6.1 Adding a service unit test

- TBD — see §3 Phase 3 for the PRD-oracle table pattern (score, order,
  hidden set). Existing service tests live in `KitchenAssistant.Tests/`.

### 6.2 Adding a component test for a page state

Pattern: "AI outcome -> visible recoverable state". Real services, scripted AI,
literal oracle strings. Reference: `KitchenAssistant.Tests/Pages/RecipeSuggestionsPageTests.cs`.

Building blocks in `KitchenAssistant.Tests/Support/`:

- `RecipePageContext` (a `BunitContext`): registers the real `ProductService` and
  `RecipeService`, `RecipeOptions` defaults, one shared `FakeTimeProvider`
  (`ctx.Time`, used by both services, so deadlines can be advanced), and
  `NullLogger`. Auth comes from `AddAuthorization()` with `SetAuthorized(..)` and a
  `NameIdentifier` claim (`RecipePageContext.UserId`), which is what the page reads.
  `[Authorize]` is enforced by the router, not the component, so access control is
  not testable here (HTTP-level, see 6.3).
- `ScriptedRecipeGenerator`: an `IRecipeGenerator` driven by a
  `Func<RecipeRequest, CancellationToken, Task<string>>`. It returns raw JSON, so the
  real parse, classify and rank path runs; only the AI is replaced. Records
  `Requests`, `LastToken` and a `Called` signal.
- `SqliteTestDatabase`: in-memory SQLite kept open for the test.
  `ctx.Database.FailNext(n)` makes the next n `CreateDbContext` calls throw, which is
  the seam for "products load fails" without mocking `ProductService`.
- `RecipePageContext.RecipesJson(("Title", ["ingredient", ..]), ..)` builds the
  generator's answer; `ctx.AddProductAsync("jajka")` seeds the user's pantry.

Recipe for a new test:

1. Script the AI outcome (a returned JSON string, a thrown exception, or a never-ending
   task) and seed products.
2. `Render<T>()`, then wait for the loaded state before acting.
3. Click, then `WaitForAssertion` on the user-visible state: alert role class and text,
   buttons, cards, `disabled`. Assert literal Polish strings copied from the archived
   plans (the oracle), never values produced by app helpers.
4. In-flight states: block the script on a `TaskCompletionSource`, assert the
   in-flight state, then `SetResult`. Never use delays. Deadlines: script
   `await Task.Delay(Timeout.Infinite, ct)`, `await ctx.Generator.Called.Task`, then
   `ctx.Time.Advance(TimeSpan.FromSeconds(61))`.
5. Cancellation: dispose the component and assert `ctx.Generator.LastToken.IsCancellationRequested`.

```csharp
[Fact]
public async Task A_failed_generation_shows_the_error_with_retry_and_no_cards()
{
    using var ctx = new RecipePageContext((_, _) =>
        Task.FromException<string>(new RecipeGenerationException("refusal")));
    await ctx.AddProductAsync("jajka");
    var page = ctx.Render<RecipeSuggestions>();
    page.WaitForAssertion(() => Assert.Single(page.FindAll("button"), b => b.TextContent.Trim() == "Zaproponuj przepisy"));

    page.FindAll("button").Single(b => b.TextContent.Trim() == "Zaproponuj przepisy").Click();

    page.WaitForAssertion(() => Assert.Contains("Nie udało się wygenerować przepisów.",
        Assert.Single(page.FindAll(".alert-danger")).TextContent));
    Assert.Contains(page.FindAll("button"), b => b.TextContent.Trim() == "Spróbuj ponownie");
    Assert.Empty(page.FindAll("article.card"));
}
```

Caveat: bUnit may not dispatch events to a disabled element, so a double-click test
proves the `disabled` attributes, not the server-side re-entry guard.

### 6.3 Adding an HTTP integration test (auth, ownership, composition)

- TBD — see §3 Phase 2 for the "anonymous redirect / cross-user isolation /
  Production composition fails fast" pattern.

### 6.4 Adding an e2e test

- TBD — see §3 Phase 2 (optional sub-phase) for the "sign in → add product
  → generate → open details" critical-flow pattern with the fake generator.
  Add an e2e test only for a cross-page flow no cheaper layer covers.

### 6.5 Post-deploy smoke

- TBD — see §3 Phase 2.

### 6.6 Per-rollout-phase notes

(Appended by each phase after it lands.)

#### Phase 1: AI failure surfacing (change `testing-ai-failure-surfacing`)

- **Shipped:** bUnit harness (`KitchenAssistant.Tests/Support/`); page outcome tests for
  `/recipes` (generating, succeeded, failed x6 causes, deadline, retry, none within
  limits, no products, products removed, user leaves); a products-load retry on the page
  (added test-first); unit gaps (generator network and HTTP errors, stop reasons, more
  than 5 usable recipes, null ingredient, line-break and tab sanitising, 100/101-character
  names); one Stryker run, triaged in
  `context/changes/testing-ai-failure-surfacing/mutation-report.md`.
- **Mutation gate:** run ad hoc after a risk phase, scoped to the changed files, from
  `KitchenAssistant.Tests/`:
  `dotnet dotnet-stryker --mutate "**/Recipes/RecipeService.cs" --mutate "**/Recipes/RecipeResponseParser.cs"`.
  Open the report, and for each survivor ask whether it would hurt a user; kill it with
  an assertion or record why it is ignored. Log-only survivors are expected (NullLogger).
  Output goes to the git-ignored `StrykerOutput/`. The first run scored 21.43 % raw, with
  every survivor log-only or cosmetic.
- **Owned by Phase 2, not pinned here:** `MaxRetries = 1` and the 60 s per-attempt timeout
  (`Program.cs:65,68`). They are Production composition, so the Production-composition
  test in Phase 2 owns them.

## 7. What We Deliberately Don't Test

Exclusions agreed during the rollout (Phase 2 interview, Q5). Future
contributors should respect these unless the underlying assumption changes.

- **Identity account pages** (register, login, manage) — generated by the
  ASP.NET Core template and trusted as framework code. Re-evaluate if they
  gain custom logic beyond styling. (Source: Phase 2 interview Q5.)
- **≤ 1 min recipe latency** — measured by the parked
  `recipe-generation-spike` and F-01 request telemetry, not by tests.
  Re-evaluate if the spike splits generation into multiple calls. (Source:
  roadmap *Parked*.)
- **Real Anthropic output quality** — non-deterministic and paid; the
  contract is tested through the fake generator. Re-evaluate if structured
  output is replaced. (Source: archive/2026-10-03-first-recipe-generation.)

## 8. Freshness Ledger

- Strategy (§1–§5) last reviewed: 2026-10-11
- Stack versions last verified: 2026-10-11
- AI-native tool references last verified: 2026-10-11

Refresh (`/10x-test-plan --refresh`) when:

- a new top-3 risk surfaces from the roadmap or archive,
- a recommended tool's `checked:` date is older than three months,
- the project's tech stack changes (new framework, new test runner),
- §7 negative-space no longer matches what the team believes.
