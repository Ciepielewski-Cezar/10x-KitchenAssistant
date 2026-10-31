# Production Composition and Access — Plan Brief

> Full plan: `context/changes/testing-production-composition-access/plan.md`
> Research: `context/changes/testing-production-composition-access/research.md`

## What & Why

Test-plan Phase 2 covers two High-impact risks.

- **Risk #3:** the app works locally but breaks on Azure in Production (late key failure, migration failure, Fake
  generator active, no interactive connection) while `/healthz` stays green.
- **Risk #4:** an anonymous visitor reaches the data pages, or a user sees another user's products in the page flow.

Nothing exercises the real `Program.cs` today. F-01 (the first deploy) is about to land, eight days before the
deadline.

## Starting Point

- Composition is tested only at the DI-extension level.
- Access relies on per-page `[Authorize]`, and there is no fallback policy. The anonymous redirect was checked once
  by curl.
- The only planned post-deploy check is a `/healthz` curl, and there is no `deploy.yml`.

## Desired End State

- **In the required CI `gate`:** a Production-environment `WebApplicationFactory` suite on SQLite. It proves the
  generator choice, fail-fast config, the AI client settings and the missing-key warning. It signs in two real users
  through the login form and proves anonymous redirects and per-user prerendered HTML.
- **Private by default:** every page is private unless it opts out, and a guard test forces a decision for each new
  page.
- **Post-deploy smoke:** a Playwright smoke signs in a dedicated user, adds a product over the live circuit,
  generates recipes with the real AI and opens one recipe's details. It is proven on the local production-like stack
  and ready for `deploy.yml`.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Missing AI key in Production | Keep the startup warning; the test pins it and the smoke goes red when generation fails | Login keeps working without AI (deployment-plan:117), and only a real call catches an *invalid* key too | Plan |
| Test-host database | In-memory SQLite in `gate`; the migration path is proven by the smoke on `local-prod -Reset` | No CI change or skip mechanism, while real SQL Server is still exercised | Research → Plan |
| Test sign-in | Real login form (antiforgery token + POST), users seeded with `UserManager` | Proves the Production cookie pipeline, and avoids the "mocked identity" anti-pattern | Plan |
| Fallback authorization | Add `FallbackPolicy = RequireAuthenticatedUser`; public pages opt out one by one with `[AllowAnonymous]` | Secure by default, so a forgotten `[Authorize]` can't publish a page | Plan |
| Smoke form | A separate Playwright config driven by `SMOKE_BASE_URL`, with no `webServer` | Reuses `waitForInteractive`; the same artifact runs locally now and after deploy later | Plan |
| Smoke account | A dedicated user from `SMOKE_*`, registered on first run if absent | No account sprawl in the production DB | Plan |
| Optional E2E sub-phase | Dropped | The smoke covers generate → open details over the live circuit | Plan (test-plan §3 rule) |
| Unknown URL, anonymous | Redirect to login (an accepted fallback-policy consequence) | The middleware challenges unmatched requests; signed-in users still get `/not-found` | Plan |

## Scope

**In scope:**

- `Mvc.Testing` and a Production factory.
- Composition, access and route-policy tests.
- The fallback policy, with `[AllowAnonymous]` on 21 public pages, 2 Identity POSTs, static assets and `/healthz`.
- A CLAUDE.md rule.
- The smoke config, setup and spec, plus `.env.example` and an npm script.
- Doc updates: test-plan, test-stack and the deployment plan.

**Out of scope:**

- A startup throw on a missing key.
- SQL Server in CI.
- A test auth handler.
- `deploy.yml`, OIDC and Azure work (F-01).
- A fresh account per smoke run.
- Re-proving the browser cross-user facet.
- Identity page logic.
- A custom 404 for anonymous visitors.

## Architecture / Approach

- `ProductionAppFactory` boots the real `Program` with:
  - `UseEnvironment("Production")`;
  - host settings via `UseSetting` (connection string read during builder creation, dummy AI key,
    `MigrateOnStartup=false`);
  - an in-memory SQLite `DbContextFactory`.
- The real `AnthropicRecipeGenerator` stays registered. Plain HTTP requests can't open a circuit, so they can't reach
  it.
- Tests use literal oracles: status codes, `Location` fragments, Polish page strings and hand-written route lists.
- The smoke is a separate Playwright config that targets any URL.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Production test host + composition | Factory; generator, fail-fast, client settings, warning, `/dev/ui`, `/healthz` tests | Override timing (`UseSetting` vs `Program.cs:29`); faking a missing connection string needs an empty content root |
| 2. Access tests | Anonymous 302 + `ReturnUrl`; two-user prerendered isolation via real sign-in | Antiforgery/cookie handling in `HttpClient`; exact `Location` encoding pinned on the first run |
| 3. Fallback policy | Secure by default + public/protected lists + coverage guard + CLAUDE.md rule | `[AllowAnonymous]` in `Account/Pages/_Imports` would open Manage; static assets must stay public |
| 4. Smoke + docs | `playwright.smoke.config.ts`, setup, spec; test-plan/test-stack/deployment-plan updates | Needs a real dev AI key and Docker for the local-prod proof; one paid call per run |

**Prerequisites:**

- .NET 10 SDK, Node with Playwright installed, and Docker for `local-prod`.
- A dev `ANTHROPIC_API_KEY` in `.env` for the green smoke run.

**Estimated effort:** about 3–4 sessions across 4 phases (Phases 1–2 together are about one session).

## Open Risks & Assumptions

- Several first-run pins were inferred, not observed:
  - `Program` is public through the .NET 10 generator;
  - `AnthropicClient` getters are readable;
  - `MapStaticAssets` serves from the test bin;
  - the `ReturnUrl=%2Fproducts` literal.

  The plan pins each one on the first run.
- A machine-level `ANTHROPIC_API_KEY` environment variable could outrank `UseSetting`. The `ApiKey ==` test value
  assertion would expose it.
- The smoke's generate step depends on the real model returning at least one recipe. A "none within limits" outcome
  would read as red.
- The §5 smoke gate stays "pending F-01" until `deploy.yml` exists. Until then it is a manual pre-deploy run.

## Success Criteria (Summary)

- A regression in Production wiring or page access turns the required `gate` red. Deliberate breaks in each phase
  prove this.
- A new page can't become public by accident.
- One command tells whether a deployed (or local-prod) app really works for a signed-in user, AI included. It goes
  red when the key is missing or invalid.
