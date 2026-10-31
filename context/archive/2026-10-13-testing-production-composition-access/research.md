---
date: 2026-10-13T23:02:15+02:00
researcher: Claude (Opus 5.5) for Cezar Ciepielewski
git_commit: cd7ee05fe2bb6f94be2299e0fa2fc3d41fe3653a
branch: main
repository: Ciepielewski-Cezar/10x-KitchenAssistant
topic: "Test-plan Phase 2: Production composition (risk #3) and access enforcement (risk #4)"
tags: [research, testing, integration-tests, webapplicationfactory, program-cs, authorization, deployment, smoke]
status: complete
last_updated: 2026-10-13
last_updated_by: Claude (Opus 5.5)
---

# Research: Production composition and access (test-plan Phase 2)

**Date**: 2026-10-13T23:02:15+02:00
**Researcher**: Claude (Opus 5.5) for Cezar Ciepielewski
**Git Commit**: cd7ee05fe2bb6f94be2299e0fa2fc3d41fe3653a (working tree clean)
**Branch**: main
**Repository**: Ciepielewski-Cezar/10x-KitchenAssistant

## Research Question

From `context/foundation/test-plan.md` §2 *Risk Response Guidance* rows #3 and #4
(`test-plan.md:67-68`) and §3 Phase 2 (`test-plan.md:81,88-99`), ground the following:

1. **Risk #3.** How is the Production environment assembled? What fails fast today on bad or
   missing config? How does the startup migration run? Is the Fake generator kept out of
   Production? What does the deployed app need for the interactive connection? What would a
   check beyond `/healthz` look like?
2. **Risk #4.** Where do the pages get the current user? Which authorization attributes and
   fallback policy apply? How can a test host authenticate as a seeded user? What would an
   HTTP-level test see for anonymous requests and for the prerendered HTML?
3. **Feasibility.** What does a Production-environment `WebApplicationFactory` test need inside
   `KitchenAssistant.Tests`, running in the CI `gate` job?
4. **History.** Which earlier decisions (deployment plan, CI gate, archived features) Phase 2
   must build on, and which ones conflict.

## Summary

- **Production composition fails fast in four cases today:**
  - `ConnectionStrings:DefaultConnection` is missing (`Program.cs:29`).
  - `Recipes:Generator=Fake` is set outside Development (`Recipes/RecipeServiceCollectionExtensions.cs:12-16`).
  - `Recipes:Effort` is invalid, or `MaxTokens`, `DeadlineSeconds` or `MaxProducts` is ≤ 0 (`:19-22`).
  - The startup migration throws: `MigrateAsync` runs before `app.Run()` (`Program.cs:79-83`).
- **The missing AI key does not fail fast.** It only logs a warning (`Program.cs:74-77`) and
  surfaces at the first click as `RecipeGenerationException("missing key…")`
  (`Recipes/AnthropicRecipeGenerator.cs:18-21`). This was a deliberate choice so that login keeps
  working (`context/changes/deployment/deployment-plan.md:117`). An *invalid* key is not detected
  until Anthropic returns 401 on the first call. Nothing checks the key earlier.
- **No test exercises the real `Program.cs` composition.** The only composition tests call
  `AddRecipes` on a hand-built `ServiceCollection`
  (`KitchenAssistant.Tests/Recipes/RecipeServiceCollectionExtensionsTests.cs:26-85`). The package
  `Microsoft.AspNetCore.Mvc.Testing` is absent from `KitchenAssistant.Tests.csproj:10-18`.
- **Access to `/products` and `/recipes` is enforced only by each page's `[Authorize]`**
  (`Components/Pages/Products.razor:9`, `Components/Pages/RecipeSuggestions.razor:10`). No
  fallback policy exists: a grep over `*.cs`, `*.razor` and `*.json` found no `AddAuthorization`,
  `FallbackPolicy` or `RequireAuthorization` on `MapRazorComponents`.
  - An anonymous GET is challenged by the endpoint authorization middleware before the component
    renders. The expected result is a 302 to `/Account/Login?ReturnUrl=%2Fproducts`. Docs and an
    archived curl check support this (`context/archive/2026-10-03-pantry-add-products/plan.md:227,311`);
    no automated test covers it.
  - Both pages read the user id from the cascading `AuthenticationState`
    (`ClaimTypes.NameIdentifier`, `Products.razor:160-165`, `RecipeSuggestions.razor:136-141`).
    Every `ProductService` method filters by that id (`Pantry/ProductService.cs:53-60,72-108,117-149,163-171`).
- **A Production `WebApplicationFactory` host is feasible without changing `Program.cs`**, with
  one open choice about the database:
  - SQLite with `EnsureCreated` and `Database:MigrateOnStartup=false` works on today's `gate` runner.
  - Proving the migration path needs a real SQL Server, for example a `services:` container in
    `ci.yml`, because the migrations are SQL Server–specific.
- **The post-deploy smoke has nothing to attach to yet.** `.github/workflows/deploy.yml` does not
  exist. The deploy plan's only smoke step curls `/healthz` (`deployment-plan.md:203`), which is
  exactly the anti-pattern `test-plan.md:67` names. The interactive connection depends on App
  Service settings that are planned but not applied (WebSockets on, ARR affinity on;
  `deployment-plan.md:164-165`). No code in the repo configures it.

## Detailed Findings

### 1. How the Production composition is assembled (risk #3)

**Configuration layering.**
- `appsettings.json:12-19` sets `Recipes:Generator=Anthropic`, `Model`, `Effort=low`,
  `MaxTokens=16000`, `DeadlineSeconds=60` and `MaxProducts=60`, plus a LocalDB connection string
  (`:2-4`).
- `appsettings.Production.json:1-8` adds only `Identity:RequireConfirmedAccount=false` and
  `Database:MigrateOnStartup=true`.
- `appsettings.Development.json:8-10` sets `Recipes:Generator=Fake`.
- Production therefore resolves to the Anthropic generator. Two things produce that: the
  `appsettings.json` value and the `RecipeOptions.Generator` default (`Recipes/RecipeOptions.cs:14`).
  No deployment doc plans a `Recipes__Generator` app setting.

**Fail-fast behaviour in `Program.cs`, in startup order.**

| Config fault | Where checked | Behaviour today | Automated proof today |
|---|---|---|---|
| Connection string missing | `Program.cs:29` | throws during builder phase | none |
| `Recipes:Generator=Fake` outside Development | `RecipeServiceCollectionExtensions.cs:12-16` (called from `Program.cs:70`) | throws | DI-extension unit tests only (`RecipeServiceCollectionExtensionsTests.cs:26-36`) |
| `Effort` typo, `MaxTokens`/`DeadlineSeconds`/`MaxProducts` ≤ 0 | `RecipeServiceCollectionExtensions.cs:19-22` | throws | DI-extension unit tests only (`:65-85`) |
| `ANTHROPIC_API_KEY` missing | `Program.cs:74-77` | warning log; app starts | none at host level; generator throws at first click (`AnthropicRecipeGenerator.cs:18-21`) |
| `ANTHROPIC_API_KEY` invalid | none | app starts; 401 at first click | none |
| Startup migration fails (SQL unreachable, login/firewall) | `Program.cs:79-83` | `MigrateAsync` throws before `app.Run()`, so the process exits | none |
| `ValidateOnBuild` / `ValidateScopes` | framework | on only in Development (WebApplicationBuilder default) | none — a broken registration in Production would surface on first resolve |

**AI client settings.** These are registered as a singleton at `Program.cs:59-69`: `ApiKey` from
`ANTHROPIC_API_KEY`, `Timeout = 60 s` and `MaxRetries = 1`. `test-plan.md:342-344` assigns these
two values to the Phase 2 composition test. The existing DI tests use `new AnthropicClient()`
(`RecipeServiceCollectionExtensionsTests.cs:18`), so they do not pin either value. One assumption
is unverified: that `AnthropicClient.Timeout` and `MaxRetries` are readable getters in the
Anthropic SDK version in use.

**Other Production-only pipeline pieces.**
- `UseExceptionHandler("/Error")` and `UseHsts()` apply only outside Development
  (`Program.cs:90-95`). `UseMigrationsEndPoint` applies only in Development (`:86-88`).
- Always on: `UseStatusCodePagesWithReExecute("/not-found")` (`:96`), `UseHttpsRedirection` (`:97`)
  and `UseAntiforgery` (`:99`).
- `/healthz` is `AddHealthChecks()` with no checks registered (`:57,101`), so it never touches the
  DB or the AI.
- Data Protection keys persist to `ApplicationDbContext` (`:52-54`).
- `/dev/ui` has no `[Authorize]`. Outside Development it calls `NavigationManager.NotFound()`
  (`Components/Pages/DevUi.razor:157-163`), and its body is wrapped in `@if (isDevelopment)` (`:12`).

**Interactive connection.**
- In code: `AddInteractiveServerComponents()` (`Program.cs:16`), `AddInteractiveServerRenderMode()`
  (`:103-104`), `blazor.web.js` and `<ReconnectModal />` (`Components/App.razor`).
- A `git grep` over tracked `*.cs/*.razor/*.json/*.yml/*.ts/*.ps1` found no `AddHubOptions`,
  WebSocket, or `UseForwardedHeaders` configuration.
- What the deployed circuit needs is therefore platform configuration, planned in
  `deployment-plan.md:164-166` and not yet applied: WebSockets on, ARR affinity on,
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` and HTTPS-only.
- A `WebApplicationFactory`/`TestServer` host cannot prove these, because they are Azure
  front-end settings. Only a check against the deployed app can.
- Locally, the circuit is proved for Development only, by `tests/e2e/seed.spec.ts` through
  `waitForInteractive` (`test-plan.md:259-263`).

### 2. Access enforcement (risk #4)

**Routes.**

| Route | File | Render mode | Authorization |
|---|---|---|---|
| `/products` | `Components/Pages/Products.razor:1-2,9` | InteractiveServer (prerendered) | `[Authorize]` |
| `/recipes` | `Components/Pages/RecipeSuggestions.razor:1-2,10` | InteractiveServer (prerendered) | `[Authorize]` |
| `/` | `Components/Pages/Home.razor:1,9-14` | static SSR | anonymous; links inside `<AuthorizeView>` |
| `/Account/Manage/*` | `Components/Account/Pages/Manage/_Imports.razor:2` | static SSR | `[Authorize]` |
| `/dev/ui`, `/not-found`, `/Error`, other `/Account/*` | respective pages | static SSR | anonymous |

**Anonymous request path.**
- `WebApplication` auto-inserts `UseRouting` → `UseAuthentication` → `UseAuthorization` ahead of
  the app's middleware, because `Program.cs` calls none of them explicitly. This comes from the
  docs (Context7 `aspnetcore.docs`).
- Razor component endpoints carry the page's `[Authorize]` as endpoint metadata. The cookie
  scheme (`Program.cs:22-27`, Identity default `LoginPath=/Account/Login`; no
  `ConfigureApplicationCookie` override found) therefore issues a 302 before the component runs.
- The router fallback `Routes.razor:4-8` → `RedirectToLogin.razor` (`NavigateTo("Account/Login?returnUrl={escaped absolute Uri}", forceLoad: true)`)
  applies to in-circuit navigation, not to this plain GET.
- The two paths can be told apart by their query string:
  - middleware path: `ReturnUrl=%2Fproducts`, capital R, path only (inferred from cookie handler
    defaults);
  - component path: lowercase `returnUrl=`, holding a full escaped absolute URI.
- **Recommended first assertion:** `Location` contains `/Account/Login` and `ReturnUrl=%2Fproducts`.
- Archived evidence: curl on `/products` returned 302 to `/Account/Login`
  (`context/archive/2026-10-03-pantry-add-products/plan.md:227,311`). The `/recipes` redirect was
  checked only in a browser walkthrough
  (`context/archive/2026-10-03-first-recipe-generation/plan.md:254,388`).
- Where authorization services come from is inferred, not observed: `AddInteractiveServerComponents`
  → SignalR `AddConnections` → `AddAuthorization()`. A missing registration would turn
  `[Authorize]` endpoints into a 500, and the recorded 302 rules that out.

**User identity and data filtering.**
- Pages take `[CascadingParameter] Task<AuthenticationState>` (`Products.razor:50-51`,
  `RecipeSuggestions.razor:114-115`), provided by `AddCascadingAuthenticationState`
  (`Program.cs:18`). They read `NameIdentifier` once in `OnInitializedAsync`.
- The id is then passed to:
  - `ProductService.GetProductsAsync` (`Products.razor:173`, `RecipeSuggestions.razor:155`),
  - `AddProductAsync` (`Products.razor:211`),
  - `UpdateProductAsync` (`:299`),
  - `DeleteProductAsync` (`:359`),
  - `RecipeService.GenerateAsync` (`RecipeSuggestions.razor:190`).
- Each of the 4 `ProductService` methods above calls `ArgumentException.ThrowIfNullOrEmpty(userId)`
  and filters by `UserId`:
  - Get: `ProductService.cs:53-60`.
  - Add: writes `UserId`, duplicate check per user (`:72-108,206-213`).
  - Update: `OwnsAsync` plus `p.Id == productId && p.UserId == userId` (`:117,131,149,199-203`).
  - Delete: same predicate with `ExecuteDeleteAsync` (`:163-171`).
- `RecipeService.GenerateAsync` reads only through `GetProductsAsync(userId)` (`Recipes/RecipeService.cs:30`).
- Neither page has a route or query parameter. Product ids for edit and remove come from
  server-side list items captured in `@onclick` closures (`Products.razor:143-144,238-261`), so the
  client never sends a product id.
- `Product.UserId` is a required FK to `AspNetUsers` (`Data/ApplicationDbContext.cs:28-34`). A test
  principal's `NameIdentifier` must therefore be a real seeded `ApplicationUser.Id`.
- If the claim is missing, `GetProductsAsync` throws. The page then renders "Nie udało się wczytać
  produktów." with a 200 (`Products.razor:32-34,186-192`).

**What the prerendered HTML exposes** (inferred from the prerender semantics in the docs; not
executed).
- Neither page uses `[StreamRendering]` or `PersistentComponentState`. Prerender waits for
  `OnInitializedAsync`, so a signed-in GET returns the finished state.
- `/products` lists the user's products in `<li class="list-group-item">` with
  `<strong>@item.Name</strong>` and `aria-label="Zmień {name}"` / `"Usuń {name}"`
  (`Products.razor:89-115,143-144`). The check "user B's product name is absent from user A's
  GET /products body, and A's own product is present" is therefore an HTTP-level assertion.
- `/recipes` shows no product names. It shows "Najpierw dodaj produkty." and a disabled button when
  the user has no products (`RecipeSuggestions.razor:29-32,45`). That gives an identity check:
  seed A with products and B with none.
- No non-Account component uses `[SupplyParameterFromForm]` or `FormName`. The `EditForm`s on
  `/products` (`:19,95`) submit only over the circuit, so no HTTP POST mutation path exists for
  products. The expectation that a raw POST to `/products` gets 400 is unverified.

**Existing coverage, not to duplicate.**
- Service level: `ProductServiceTests.cs:109,202,371,386,491` and `RecipeServiceTests.cs:166`.
- Browser level: `tests/e2e/products-cross-user-isolation.spec.ts`, A's product absent on B's live
  page, one direction only (`:56-70`).
- bUnit fakes auth (`KitchenAssistant.Tests/Support/RecipePageContext.cs:33-35`). The router
  enforces `[Authorize]`, so bUnit cannot test access (`test-plan.md:192-194`).
- No test covers the anonymous redirect or the HTTP pipeline (`test-plan.md:129`).

### 3. Hosting a Production-environment `WebApplicationFactory` in `KitchenAssistant.Tests`

**Project.**
- `KitchenAssistant.Tests.csproj` targets `net10.0` (`:4`) and references the app (`:25`). Its
  packages are listed at `:11-17`, and `Microsoft.AspNetCore.Mvc.Testing` is not among them.
- There is no central package management. `nuget.config` points to nuget.org only.
- The test project is already in `KitchenAssistant.slnx`, so new tests in it run in the `gate`
  `dotnet test` step (`.github/workflows/ci.yml:40-42`) and count toward the zero-executed guard
  (`scripts/ci/assert-tests-ran.ps1:16-38`).

**`Program` visibility.**
- The ASP.NET Core 10.0.12 ref pack ships `PublicProgramSourceGenerator`, which emits
  `public partial class Program`. Docs rule ASP0027 says a hand-written declaration is no longer
  needed.
- The worker could not load the net10 assembly, so this is inferred, not observed. The first
  compile of a `WebApplicationFactory<Program>` fixture will confirm it.
- Fallback: `KitchenAssistant.csproj:31` already has `InternalsVisibleTo KitchenAssistant.Tests`.

**When overrides take effect.**
- `Program.cs:29` reads the connection string during `CreateBuilder`. Overrides must therefore be
  host configuration (`builder.UseSetting(...)` / `UseEnvironment` in `ConfigureWebHost`, or
  `CreateHost` plus `ConfigureHostConfiguration`). Docs: integration-tests "Customize
  WebApplicationFactory", option 1.
- `ConfigureAppConfiguration` is applied too late for `:29`. It is fine for `Database:MigrateOnStartup`,
  read after `Build()` at `:79`.
- `ConfigureTestServices` runs after Program's registrations, so it can replace the DbContext
  options and `IRecipeGenerator`.
- `WebApplicationFactory` defaults to the **Development** environment, so `UseEnvironment("Production")`
  is mandatory. Without it the host would load `Generator=Fake` and serve `/dev/ui`.

**Database: the decision Phase 2 must make.**
- The migrations are SQL Server–specific: `nvarchar`, `SqlServer:Identity`
  (`Data/Migrations/00000000000000_CreateIdentitySchema.cs:18-58`), and `UseIdentityColumns` in the
  snapshot. There are 3 migrations: `CreateIdentitySchema`, `AddDataProtectionKeys` and `AddProducts`.

| Option | How | Proves | Gives up | CI change |
|---|---|---|---|---|
| (a) SQLite in-memory | `RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>()`, `AddDbContextFactory(o => o.UseSqlite(sharedOpenConnection))`, `EnsureCreated`, `Database:MigrateOnStartup=false` (pattern as `Support/SqliteTestDatabase.cs:10-50`) | access, prerender isolation, Production DI/pipeline, Fake never selected, client settings | `UseSqlServer` + `EnableRetryOnFailure` (`Program.cs:31-32`), migrate-on-startup path | none |
| (b) real SQL Server | Production env, `MigrateOnStartup` from `appsettings.Production.json`, `ConnectionStrings:DefaultConnection` → unique per-run DB; assert 3 applied migrations; drop DB after | everything in (a) plus the real provider and the startup migration | needs SQL Server: `services: mssql` in `ci.yml` (image `mcr.microsoft.com/mssql/server:2022-latest` as `compose.yaml:10`) or Testcontainers; local skip gate (xunit 2.9.3 has no `Assert.Skip`) | yes |

- `ubuntu-latest` has no LocalDB. The `gate` job has no `services:` block (`ci.yml:18-46`).
- An unverified inference: running EF `Migrate` against SQLite with the SQL Server snapshot is not
  a valid stand-in for (b).

**Authenticating as a seeded user.**
- **A — test `AuthenticationHandler`** (docs pattern). It is the simplest option, but it does not
  prove the Identity cookie wiring (`Program.cs:22-27,39-47`). The principal needs
  `NameIdentifier` set to a seeded `ApplicationUser.Id`.
- **B — real login form.** GET `/Account/Login`, extract `__RequestVerificationToken`, then POST
  `_handler=login`, `Input.Email` and `Input.Password` (`Components/Account/Pages/Login.razor:21,27,32`).
  Expect a 302 plus the `.AspNetCore.Identity.Application` cookie. It proves the most, including
  antiforgery, but needs HTML token parsing.
  - Production sets `RequireConfirmedAccount=false` (`appsettings.Production.json`), so a user
    created with `UserManager.CreateAsync` from `factory.Services` can sign in directly.
- **C — `UserManager` for setup plus B for sign-in.** The test-host worker recommended this.
- Pitfalls:
  - Use `AllowAutoRedirect = false` to observe 302s.
  - Don't send `X-Requested-With: XMLHttpRequest`, or the cookie handler returns 401.
  - Use `BaseAddress = https://localhost` to avoid surprises from `UseHttpsRedirection` and
    secure cookies.
  - Use the same client for the antiforgery GET and the POST.
  - `IdentityRevalidatingAuthenticationStateProvider` (`Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs:18-45`)
    revalidates only inside a live circuit, which plain `HttpClient` requests never open.
- Replace `IRecipeGenerator`, for example with `Support/ScriptedRecipeGenerator.cs`, so no real
  call can happen. `ANTHROPIC_API_KEY` from the machine environment is still read in Production.
- Unverified: whether `MapStaticAssets` (`Program.cs:102`) finds
  `KitchenAssistant.staticwebassets.endpoints.json` from the test bin outside Development. Check
  this on the first run.

### 4. Post-deploy smoke: current state

- Absent from the repo:
  - `deploy.yml`, OIDC scripts, `infra/azure/*` and Azure Monitor wiring;
  - any recorded Azure resource name or hostname.
- `deployment-plan.md:29-41` shows phases 0, 1 and 3-9 as not started and phase 2 as partial.
- `roadmap.md:44,89` shows F-01 as `ready`. Its blockers are the PAYG upgrade, B1 quota and the
  production AI key (`:86`).
- Planned smoke step: curl `/healthz` 12 × 10 s (`deployment-plan.md:203`).
- Planned manual post-deploy checks (`deployment-plan.md:236-241`):
  - `/healthz`, plus http redirecting to https;
  - register and log in with no confirmation;
  - WS 101 on `_blazor` — the plan uses the Counter page, which has been removed;
  - still logged in after `az webapp restart`;
  - no missing-key warning in the log tail.
- A real AI call is deferred: "verified when FR-005 lands".
- Planned availability test on `/healthz` only, which the plan itself notes cannot see DB or AI
  outages (`deployment-plan.md:251`).
- Local production-like stack: `scripts/local-prod.ps1:122-135` waits only for `/healthz` 200.
  `compose.yaml:34` defaults `ANTHROPIC_API_KEY` to empty.
- The 2026-10-05 spike recorded a real instance of risk #3 on that stack. On the first attempt
  the container was on a stale image (started with `-NoBuild`) and `.env` had an empty
  `ANTHROPIC_API_KEY`, and no recipe log lines appeared
  (`context/changes/recipe-generation-spike/spike.md:219`). The note does not record the
  `/healthz` state at the time.

## Code References

- `Program.cs:29` — connection string required, throws if missing
- `Program.cs:31-32` — SQL Server DbContext factory with `EnableRetryOnFailure`
- `Program.cs:42` — `RequireConfirmedAccount` from config (false in Production)
- `Program.cs:52-54` — Data Protection keys in the DB
- `Program.cs:57,101` — `/healthz`, liveness only
- `Program.cs:59-69` — `AnthropicClient` singleton: key, 60 s timeout, `MaxRetries = 1`
- `Program.cs:70` — `AddRecipes(configuration, environment)`
- `Program.cs:74-77` — missing key: warning only
- `Program.cs:79-83` — migrate on startup when `Database:MigrateOnStartup`
- `Program.cs:86-99` — environment-dependent pipeline
- `Recipes/RecipeServiceCollectionExtensions.cs:12-22` — Fake guard and option validation
- `Recipes/AnthropicRecipeGenerator.cs:18-21` — missing key detected at first generation
- `appsettings.json:12-19`, `appsettings.Production.json:1-8`, `appsettings.Development.json:8-10` — config layering
- `Components/Routes.razor:4-8`, `Components/Account/Shared/RedirectToLogin.razor` — router fallback redirect
- `Components/Pages/Products.razor:9,50-51,160-173` and `Components/Pages/RecipeSuggestions.razor:10,114-115,136-155` — `[Authorize]` and user-id sourcing
- `Pantry/ProductService.cs:53-213` — per-user filtering
- `Components/Pages/DevUi.razor:12,157-163` — Development-only kitchen sink
- `KitchenAssistant.Tests/KitchenAssistant.Tests.csproj:10-25` — packages (no Mvc.Testing)
- `KitchenAssistant.Tests/Support/SqliteTestDatabase.cs:10-50` — SQLite plus `EnsureCreated` pattern
- `KitchenAssistant.Tests/Recipes/RecipeServiceCollectionExtensionsTests.cs:18,26-85` — DI-extension tests (not host)
- `KitchenAssistant.csproj:31` — `InternalsVisibleTo KitchenAssistant.Tests`
- `.github/workflows/ci.yml:18-46` — `gate` job (ubuntu-latest, no services)
- `scripts/ci/assert-tests-ran.ps1:16-38` — zero-executed guard
- `Data/Migrations/00000000000000_CreateIdentitySchema.cs:18-58` — SQL Server–specific migration

## Architecture Insights

- **Startup throw is the only fail-fast mechanism.** Validation happens either in the builder phase
  (`AddRecipes`, connection string) or right after `Build()` (migration). A Production test host
  can assert "throws on start" just by creating the factory's client: `factory.CreateClient()` or
  `factory.Services` surfaces the startup exception.
- **The missing-key policy is a deliberate trade-off,** not an oversight: login works without AI
  (`deployment-plan.md:117`). Making it "fail loudly" (`test-plan.md:67`) can mean three things:
  - a Production-only startup throw, which reverses `:117`;
  - a readiness or smoke signal that is red when the key is missing;
  - an alert on the logged warning.
- **Access relies on per-page attributes, not a fallback policy.** A future page that forgets
  `[Authorize]` is public by default. An HTTP test that enumerates the protected routes, or a
  fallback policy, would close that gap. This is a design observation, not a current defect: both
  data pages carry the attribute.
- **The interactive connection is platform-dependent.** It has no code-level configuration, so the
  only automated evidence it can have on Azure is a post-deploy check that opens a circuit (WS 101
  on `_blazor`, or an agent or Playwright browser smoke against `/products`).

## Historical Context (from prior changes)

- `context/changes/deployment/deployment-plan.md:117` — missing key warns rather than throws, so
  login survives. This conflicts with the `test-plan.md:67` wording "fails loudly at startup on bad
  or missing config"; Phase 2 must pick one.
- `context/changes/deployment/deployment-plan.md:120-122`; `context/foundation/infrastructure.md:114,146`
  — migrations run at app startup and CI never reaches SQL.
- `context/changes/deployment/deployment-plan.md:164-166` — planned App Service settings:
  WebSockets on, ARR affinity on, `/healthz` health path, forwarded headers.
- `context/changes/deployment/deployment-plan.md:188-204` — planned `deploy.yml` with its own
  build/test job and a `/healthz`-only smoke. Two newer documents supersede it on one point:
  `test-plan.md:166-169` and `context/changes/testing-ci-test-gate/plan.md:73,212-218` say
  `deploy.yml` calls `ci.yml` via `workflow_call` and `needs:` it. On this point the deployment
  plan is stale; it was not updated after that decision. `deployment-plan.md:79,204` still names a
  `build` required check, while the live ruleset requires `gate`.
- `context/changes/testing-ci-test-gate/change.md:12` — `deploy.yml`, OIDC and the post-deploy
  smoke are deferred "until after test phases 2-4". This is in tension with `test-plan.md:85`
  ("Phase 2 lands alongside F-01") and with the post-deploy smoke gate that is "required after §3
  Phase 2" (`test-plan.md:154`).
- `context/archive/2026-10-03-first-recipe-generation/plan.md:29,214` — Fake is Development-only and
  startup refuses otherwise. This is implemented and holds today (`RecipeServiceCollectionExtensions.cs:12-16`).
- `context/archive/2026-10-03-pantry-add-products/plan.md:227,311` — the anonymous `/products`
  redirect was verified by curl (302 → `/Account/Login`). That supports the current expectation,
  but it was a manual check only.
- `test-plan.md:68,88-99` and `context/archive/2026-10-13-test-plan-refresh-2026-10-13/` — risk #4's
  browser facet is already covered by E2E. Phase 2's HTTP test owns the anonymous redirect and
  prerendered-HTML isolation. The optional E2E sub-phase is narrowed to generate → open details.
- `test-plan.md:342-344` — `MaxRetries = 1` and the 60 s timeout are owned by the Phase 2
  composition test.

## Related Research

- `context/archive/2026-10-11-testing-ai-failure-surfacing/research.md` — Phase 1 (bUnit harness, `ScriptedRecipeGenerator`)
- `context/changes/testing-ci-test-gate/research.md` — CI `gate` and ruleset

## Open Questions

These need a product or plan decision; they are not further discovery.

1. **Missing or invalid AI key in Production.** Should Phase 2 change the composition to throw at
   startup when the key is missing, reversing `deployment-plan.md:117`? Or should "loud" mean a
   failing smoke step or alert, keeping the startup warning? An invalid key cannot be caught at
   startup without a network call to Anthropic either way.
2. **Database for the Production test host:** SQLite (no CI change; migration path unproven) or a
   SQL Server service container in `gate` (proves `MigrateOnStartup` and `UseSqlServer`; adds CI
   time and a local skip mechanism).
3. **Test sign-in:** a test auth handler (cheap; skips cookie wiring) or the real login form with a
   `UserManager`-seeded user (proves Identity and antiforgery).
4. **Post-deploy smoke sequencing.** No deploy exists, and `testing-ci-test-gate/change.md:12`
   defers it. Phase 2 can do one of three things:
   - deliver the smoke as a script or checklist runnable against the local production-like stack
     now and wire it into `deploy.yml` later;
   - pull F-01 forward;
   - mark the "required after Phase 2" smoke gate (`test-plan.md:154`) as pending on F-01.
   It is also undecided whether the smoke signs in with a dedicated production test user, which
   no document provisions, and whether it makes a real AI call (cost, spend limit).
5. **Fallback authorization policy.** Should Phase 2 add a test that enumerates the
   `[Authorize]`-required routes, or recommend a fallback policy? This is optional and outside
   the stated risk scenario.
6. **To pin on the first test run (unverified inferences):**
   - the exact `Location` format (`ReturnUrl=%2Fproducts`);
   - that `Program` is public through the .NET 10 source generator;
   - `MapStaticAssets` behaviour from the test bin in Production;
   - readability of `AnthropicClient.Timeout` and `MaxRetries`;
   - prerendered `/products` containing the product list.
