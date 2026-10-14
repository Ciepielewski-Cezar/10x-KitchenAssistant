# Production Composition and Access (Test-Plan Phase 2) Implementation Plan

## Overview

This is test-plan Phase 2, covering risks #3 and #4 (`context/foundation/test-plan.md:52-53,67-68,81`). It adds the
project's first HTTP integration layer. A `WebApplicationFactory<Program>` host runs the real `Program.cs` in the
**Production** environment and proves four things:

- the Production composition fails fast on bad config and never selects the Fake generator;
- anonymous requests to the data pages are redirected to login;
- a signed-in user's prerendered HTML contains only their own data;
- (after Phase 3) every page is private unless it explicitly opts out.

It also adds a Playwright **post-deploy smoke** that targets any base URL. The smoke is proven now against the local
production-like stack and is ready for `deploy.yml` when F-01 lands.

## Current State Analysis

The research (`research.md`) is the baseline. In short:

- **No test exercises the real `Program.cs`.** Composition is tested only through `AddRecipes` on a hand-built
  `ServiceCollection` (`KitchenAssistant.Tests/Recipes/RecipeServiceCollectionExtensionsTests.cs:26-85`).
  `Microsoft.AspNetCore.Mvc.Testing` is not referenced (`KitchenAssistant.Tests/KitchenAssistant.Tests.csproj:10-18`).
- **Fail-fast today:**
  - a missing connection string (`Program.cs:29`);
  - `Recipes:Generator=Fake` outside Development, a bad `Effort`, or a non-positive cap
    (`Recipes/RecipeServiceCollectionExtensions.cs:12-22`);
  - a failing startup migration (`Program.cs:79-83`).
- **A missing AI key** only logs a warning (`Program.cs:74-77`). This is deliberate: login keeps working without AI
  (`context/changes/deployment/deployment-plan.md:117`).
- **AI client settings.** `AnthropicClient` uses `Timeout = 60 s` and `MaxRetries = 1` (`Program.cs:60-69`), and no test
  pins either value (`test-plan.md:342-344` assigns them to this phase).
- **Access is enforced only by per-page `[Authorize]`**:
  - `Components/Pages/Products.razor:9` and `Components/Pages/RecipeSuggestions.razor:10`;
  - `Components/Account/Pages/Manage/_Imports.razor:2`;
  - the Manage endpoint group in `Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs:92`.

  There is no fallback policy, so a new page that forgets the attribute is public.
- **The anonymous redirect and prerendered-HTML isolation are untested.** The browser facet is covered by
  `tests/e2e/products-cross-user-isolation.spec.ts`.
- **No deploy exists.** `.github/workflows/` has only `ci.yml`. The deployment plan's planned smoke is `/healthz`
  only (`deployment-plan.md:203`), and its WebSocket check uses the deleted Counter page (`:238`).
- **The CI `gate` job** runs on `ubuntu-latest` with no `services:` (`.github/workflows/ci.yml:18-46`). The
  migrations are SQL Server–specific.

## Desired End State

- `dotnet test` (and therefore the required `gate` check) runs a Production-environment HTTP suite on in-memory
  SQLite. It proves:
  - **Composition:** the generator is `AnthropicRecipeGenerator`; Fake, a bad `Effort`, a non-positive cap and a
    missing connection string each stop startup; the client uses a 60 s timeout and 1 retry and gets its key from
    config; a missing key logs the warning; `/dev/ui` returns 404; `/healthz` returns 200.
  - **Access:** anonymous `/products`, `/recipes` and `/Account/Manage` get a 302 to `/Account/Login` with a
    `ReturnUrl`. Two users, signed in through the real login form, each see only their own products in the
    prerendered `/products` and their own state on `/recipes`.
  - **Secure by default:** a fallback policy requires an authenticated user. Every routable page is either on a
    hand-written public list (anonymous access works) or on a protected list (anonymous access is challenged). A new
    page that is on neither list fails the suite.
- `npx playwright test -c playwright.smoke.config.ts`, with `SMOKE_BASE_URL` set, does the following with a dedicated
  account:
  1. checks that `/healthz` returns 200;
  2. signs in;
  3. opens `/products` over the live interactive connection and adds a product;
  4. generates recipes with the real AI and opens one recipe's details;
  5. removes the product.

  Against `scripts/local-prod.ps1 -Reset` (real SQL Server, startup migration from an empty database) it is green with
  a valid key and red with an empty key.
- `test-plan.md` §3, §5 and §6.3/6.5/6.6, `test-stack.md`, `CLAUDE.md` and the deployment plan reflect the above.

### Key Discoveries:

- `WebApplicationFactory` defaults to Development, so `UseEnvironment("Production")` is mandatory. Without it the
  host loads `Generator=Fake` and serves `/dev/ui` (research §3).
- `Program.cs:29` reads the connection string while the builder is being created. Overrides must therefore be host
  settings (`builder.UseSetting`), not `ConfigureAppConfiguration`. `Database:MigrateOnStartup` is read after
  `Build()` (`:79`), so either mechanism works for it.
- Generation runs only over the interactive circuit (no HTTP POST path; research §2). Plain `HttpClient` requests
  therefore cannot trigger an AI call. The test host keeps the real `AnthropicRecipeGenerator` registration, with a
  non-secret dummy key, so the composition assertion reflects what Production resolves.
- `appsettings.Production.json` sets `RequireConfirmedAccount=false`. A user created with `UserManager` from
  `factory.Services` can sign in through the form straight away.
- The login form posts `_handler=login`, `Input.Email`, `Input.Password` and `__RequestVerificationToken`
  (`Components/Account/Pages/Login.razor:21-33`). A failed sign-in renders "Error: Invalid login attempt." (`:145`).
- `Components/Account/Pages/_Imports.razor` is the parent of `Manage/_Imports.razor`, which carries `[Authorize]`.
  `[AllowAnonymous]` placed in the parent `_Imports` would reach the Manage pages and override their `[Authorize]`.
- With a fallback policy, the authorization middleware also challenges requests that match **no** endpoint, and
  `MapStaticAssets` endpoints need `.AllowAnonymous()` (Context7, aspnetcore.docs "Require global user
  authentication").
- `PasskeyCreationOptions` is used only by `Manage/Passkeys.razor` (authenticated). `PerformExternalLogin` and
  `PasskeyRequestOptions` are used by anonymous visitors on the login page.
- The recipe details toggle is the button "Pokaż przepis" / "Ukryj przepis" (`Components/Ui/RecipeCard.razor:24`).
  The generate button is "Zaproponuj przepisy".
- The existing Playwright config always starts a Development `webServer` (`playwright.config.ts:39-47`). The smoke
  therefore gets its own config file.

## What We're NOT Doing

- **No startup throw for a missing `ANTHROPIC_API_KEY`.** The warning stays, as `deployment-plan.md:117` decided. For
  risk #3, "loud" means two things: the composition test pins the warning, and the smoke goes red when generation is
  unusable (missing *or* invalid key).
- **No SQL Server in CI.** The `gate` job keeps no `services:` block. The startup-migration path is proven by the smoke
  against `local-prod -Reset`, not on every PR.
- **No test authentication handler.** Tests sign in through the real login form.
- **No `deploy.yml`, OIDC or Azure work.** The smoke is wired into the deploy workflow by F-01 (change `deployment`).
  Until then the §5 smoke gate reads "pending F-01".
- **No fresh account per smoke run.** One dedicated smoke user, from `SMOKE_*` variables.
- **No separate E2E sub-phase** (generate → open details with the Fake generator). The smoke covers that flow over the
  live interactive connection, so §3's optional sub-phase is dropped as covered (`test-plan.md:88-99`).
- **No re-proof of the browser cross-user facet.** `products-cross-user-isolation.spec.ts` owns it.
- **No tests of the Identity pages' own logic** (`test-plan.md:351-353`). Phase 3 only checks that they stay reachable
  anonymously.
- **No custom 404 for anonymous visitors to unknown URLs.** Under the fallback policy they are redirected to login.
  This is an accepted consequence: signed-in users still get `/not-found`.
- **No invalid-key detection at startup.** It would need a network call to Anthropic.

## Implementation Approach

Build the test host first and prove Production composition with it (Phase 1). Then add access tests while the code
is unchanged, so they characterize today's behaviour (Phase 2). Next, introduce the fallback policy, with those tests
as the regression net (Phase 3). Finally, add the post-deploy smoke and update the docs (Phase 4).

Every HTTP test uses literal oracles: status codes, `Location` fragments, the literal Polish strings from the pages,
and hand-written route lists. No test computes its expected values from app code.

## Critical Implementation Details

- **Override timing.** Pass settings with `builder.UseSetting(...)` in `ConfigureWebHost`. Values set with
  `ConfigureAppConfiguration` arrive after `Program.cs:29` has already read the connection string.
- **The missing connection string can't be faked with `UseSetting(..., "")`.** An empty string is not `null`, so
  `?? throw` passes. Instead, point the content root at an empty temporary directory (`UseContentRoot`) so that no
  `appsettings*.json` loads. Assert that the `InvalidOperationException` names `DefaultConnection`.
- **The `[AllowAnonymous]` placement is load-bearing.**
  - Put it in each public page's own file. Never put it in `Components/Account/Pages/_Imports.razor` (the parent of
    Manage).
  - Never call `.AllowAnonymous()` on `MapRazorComponents<App>()` or on the `/Account` route group (`MapGroup`). Both
    would also reach `[Authorize]` pages and the `/Account/Manage` group, and `AllowAnonymous` wins.
- **Fallback-policy order.** `AddAuthorization` / `AddAuthorizationBuilder` must run before `Build()`. Mark
  `/healthz` and `MapStaticAssets()` `.AllowAnonymous()`, or anonymous pages lose their CSS and `blazor.web.js`
  (enhanced navigation breaks) and the App Service health probe gets a 302.

## Phase 1: Production test host and composition tests

### Overview

Add `Mvc.Testing` and a reusable Production-environment factory on in-memory SQLite. Then pin the Production
composition: the generator choice, the fail-fast cases, the AI client settings, the missing-key warning,
`/dev/ui` and `/healthz`.

### Changes Required:

#### 1. Test project package

**File**: `KitchenAssistant.Tests/KitchenAssistant.Tests.csproj`

**Intent**: Reference `Microsoft.AspNetCore.Mvc.Testing` so tests can host the real `Program`.

**Contract**: One `PackageReference`, version on the same 10.0.x patch line as the existing EF Core Sqlite reference
(10.0.13). No other package changes.

#### 2. Production factory

**File**: `KitchenAssistant.Tests/Support/ProductionAppFactory.cs` (new)

**Intent**: A `WebApplicationFactory<Program>` that boots the real composition in Production, with only the
database swapped for in-memory SQLite. A logger provider captures log entries so tests can assert on warnings.

**Contract**:

- `UseEnvironment("Production")`.
- Host settings, applied through `UseSetting`:
  - `ConnectionStrings:DefaultConnection` = a syntactically valid SQL Server string that is never opened;
  - `Database:MigrateOnStartup=false`;
  - `ANTHROPIC_API_KEY` = a constant non-secret test value, overridable per test.
- In `ConfigureTestServices`:
  - remove the `ApplicationDbContext` options configuration and re-register `AddDbContextFactory` on a shared open
    SQLite connection;
  - run `EnsureCreated` once (same pattern as `Support/SqliteTestDatabase.cs:10-25`).
- `IRecipeGenerator` is **not** replaced.
- Exposes the captured log entries.
- Exposes helpers to create a client with `AllowAutoRedirect = false` and `BaseAddress = https://localhost`.
- `Program` visibility: rely on the .NET 10 public `Program` source generator. If the first compile fails, fall back to
  the existing `InternalsVisibleTo` (`KitchenAssistant.csproj:31`).

#### 3. Composition tests

**File**: `KitchenAssistant.Tests/Http/ProductionCompositionTests.cs` (new)

**Intent**: Prove what the Production host assembles and what stops it from starting (risk #3).

**Contract**: One fact per behaviour, each using a fresh factory or `WithWebHostBuilder` for per-test settings:

- `IRecipeGenerator` resolves to `AnthropicRecipeGenerator`.
- `Recipes:Generator=Fake` → `factory.CreateClient()` throws `InvalidOperationException` mentioning "Development".
- `Recipes:Effort=extreme` → startup throws.
- `Recipes:MaxProducts=0` → startup throws.
- Content root at an empty temp directory → startup throws, naming `DefaultConnection` (see Critical Implementation
  Details).
- The resolved `AnthropicClient` has `Timeout == 60 s`, `MaxRetries == 1` and `ApiKey ==` the test value.
- With `ANTHROPIC_API_KEY` empty, the host still starts (`/healthz` 200) and the captured logs contain a Warning with
  "ANTHROPIC_API_KEY is not configured".
- `GET /dev/ui` → 404.
- `GET /healthz` → 200 with body `Healthy`.

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds with the new package and factory
- `dotnet test --filter "FullyQualifiedName~ProductionCompositionTests"` passes
- Deliberate break: temporarily changing `MaxRetries = 1` to `2` in `Program.cs` turns the client-settings test red (then revert)
- `dotnet test` (whole suite) passes, and `scripts/ci/assert-tests-ran.ps1` reports executed tests

#### Manual Verification:

- First-run pins recorded in the phase commit message: `Program` visibility mechanism used, and that `AnthropicClient.Timeout`/`MaxRetries`/`ApiKey` were readable

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Access tests (anonymous redirect and prerendered isolation)

### Overview

Characterize today's access behaviour at the HTTP level, signing in through the real Identity cookie pipeline. These
tests must pass on unchanged app code. They become the regression net for Phase 3.

### Changes Required:

#### 1. Sign-in and seeding helpers

**File**: `KitchenAssistant.Tests/Support/ProductionAppFactory.cs` (extend) or `KitchenAssistant.Tests/Support/HttpSignIn.cs` (new)

**Intent**: Create real users and sign them in the way a browser does, so the tests prove the cookie, Identity and
antiforgery wiring.

**Contract**:

- `SeedUserAsync()`:
  - creates a user with `UserManager<ApplicationUser>` from a `factory.Services` scope;
  - uses a unique email (GUID suffix) and a policy-valid password;
  - returns the id.
- Seeding products goes through `ProductService.AddProductAsync(userId, ProductForm)` from a scope (`Pantry/ProductService.cs:72`).
- `SignInAsync(client, email, password)`:
  - uses the same client for `GET /Account/Login` and the POST;
  - extracts `__RequestVerificationToken` with AngleSharp (already available through bUnit);
  - POSTs the form fields from Key Discoveries;
  - asserts a 302 whose `Location` is not `/Account/Login`;
  - leaves the Identity cookie in the client's cookie container.
- Never send `X-Requested-With`.

#### 2. Access tests

**File**: `KitchenAssistant.Tests/Http/AccessTests.cs` (new)

**Intent**: Prove risk #4's remaining facets. Anonymous requests to protected pages are challenged. Each signed-in
user's prerendered page shows only their own data.

**Contract**:

- **Anonymous redirect, a theory over `/products`, `/recipes` and `/Account/Manage`:** expect 302, with `Location`
  containing `/Account/Login` and `ReturnUrl=` plus the escaped path (e.g. `ReturnUrl=%2Fproducts`). If the first run
  shows a different but equivalent encoding, pin the observed literal in the test.
- **Products isolation, both directions.**
  - Setup: users A and B are seeded, and each has one uniquely named product.
  - A's `GET /products` HTML contains A's product name and not B's.
  - B's contains B's and not A's.
- **Recipes identity.**
  - Setup: A has a product and B has none.
  - A's `GET /recipes` does not contain "Najpierw dodaj produkty.", and its "Zaproponuj przepisy" button is not
    `disabled`.
  - B's contains "Najpierw dodaj produkty.".
- Each test seeds its own users, so tests are independent of order and of each other.

### Success Criteria:

#### Automated Verification:

- `dotnet test --filter "FullyQualifiedName~AccessTests"` passes on unchanged app code
- Deliberate break: temporarily removing `@attribute [Authorize]` from `Products.razor` turns the anonymous `/products` case red (then revert)
- Deliberate break: temporarily replacing the user id passed to `GetProductsAsync` in `Products.razor` with another seeded user's id turns the isolation test red (then revert)
- `dotnet test` (whole suite) passes

#### Manual Verification:

- The pinned `Location` literal and the prerendered `/products` content were observed (not assumed) on the first run, noted in the phase commit message

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Fallback authorization policy (secure by default)

### Overview

Make every endpoint require an authenticated user unless it explicitly opts out. Then prove both sides with
hand-written route lists and a guard that forces a decision for every new page.

### Changes Required:

#### 1. Fallback policy and anonymous infrastructure endpoints

**File**: `Program.cs`

**Intent**: Require an authenticated user wherever no authorization metadata exists. Keep liveness and static assets
public.

**Contract**:

- `builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(...RequireAuthenticatedUser()...)` (or the
  `AddAuthorization(o => o.FallbackPolicy = …)` equivalent), with a one-line comment pointing at the public-page rule.
- `app.MapHealthChecks("/healthz").AllowAnonymous()`.
- `app.MapStaticAssets().AllowAnonymous()`.
- `MapRazorComponents<App>()` stays without `AllowAnonymous`.

#### 2. Public pages opt out explicitly

**Files**:

- `Components/Pages/Home.razor`, `NotFound.razor`, `Error.razor` and `DevUi.razor`;
- each page directly under `Components/Account/Pages/`: `AccessDenied`, `ConfirmEmail`, `ConfirmEmailChange`,
  `ExternalLogin`, `ForgotPassword`, `ForgotPasswordConfirmation`, `InvalidPasswordReset`, `InvalidUser`, `Lockout`,
  `Login`, `LoginWith2fa`, `LoginWithRecoveryCode`, `Register`, `RegisterConfirmation`, `ResendEmailConfirmation`,
  `ResetPassword` and `ResetPasswordConfirmation`.

**Intent**: Keep today's anonymous pages reachable anonymously.

**Contract**: `@attribute [AllowAnonymous]` in each listed file, and nowhere in an `_Imports.razor`. Manage pages are
unchanged. `DevUi` keeps returning 404 outside Development.

#### 3. Anonymous Identity endpoints

**File**: `Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs`

**Intent**: Keep login-time endpoints usable by anonymous visitors.

**Contract**:

- `.AllowAnonymous()` on `PerformExternalLogin` and `PasskeyRequestOptions` only.
- `Logout` and `PasskeyCreationOptions` stay under the fallback policy.
- The Manage group keeps `RequireAuthorization()`.
- Nothing is added at the `/Account` group level.

#### 4. Route-policy tests

**File**: `KitchenAssistant.Tests/Http/AuthorizationPolicyTests.cs` (new)

**Intent**: Prove secure-by-default and keep the public surface an explicit, reviewed list.

**Contract**:

- **Public list (hand-written).** Every route from §2.
  - `/`, `/Account/Login`, `/Account/Register` and `/not-found`: anonymous GET → 200.
  - Every other public route: anonymous GET is **not** a challenge (no `Location` to `/Account/Login`). Some redirect
    elsewhere on missing query parameters, by design.
- **Protected list (hand-written).** `/products`, `/recipes` and every `/Account/Manage/*` route: anonymous GET →
  302 to `/Account/Login`.
- **Coverage guard.** The set of `RouteAttribute` templates on component types in the app assembly equals the union
  of the two lists. A new page fails the test until someone adds it to a list.
- **Infrastructure.**
  - Anonymous `GET /healthz` → 200.
  - The `blazor.web.js` script `src` and the stylesheet `href`s parsed from anonymous `GET /` → 200 each. This also
    pins `MapStaticAssets` serving from the test bin.
- **Unknown URL.**
  - Anonymous `GET /no-such-page` → 302 to `/Account/Login`. This is an accepted consequence.
  - Signed in → 404.

#### 5. Project rule

**File**: `CLAUDE.md` (Hard rules, outside the 10x-cli markers)

**Intent**: Stop the next agent from adding a public page wrong, or opening up Manage by accident.

**Contract**: One bullet:

- pages are private by default (a fallback policy in `Program.cs`);
- a public page needs `@attribute [AllowAnonymous]` in its own file, never in an `_Imports.razor` (`Account/Pages/_Imports.razor` is the parent of `Manage/`);
- add every new page to the public or protected list in `AuthorizationPolicyTests`.

### Success Criteria:

#### Automated Verification:

- `dotnet test --filter "FullyQualifiedName~AuthorizationPolicyTests"` passes
- Phase 1 and Phase 2 tests still pass unchanged: `dotnet test`
- Deliberate break: temporarily adding `@attribute [AllowAnonymous]` to `Components/Account/Pages/_Imports.razor` turns the Manage protected cases red (then revert)
- Deliberate break: temporarily adding a page `@page "/tmp-guard"` without listing it turns the coverage guard red (then delete)
- Deliberate break: temporarily removing `.AllowAnonymous()` from `MapStaticAssets()` turns the static-asset case red (then revert)
- `dotnet ef migrations has-pending-model-changes` exits 0

#### Manual Verification:

- Local E2E still green against the Development build: `npx playwright test` (sign-in, enhanced navigation, cross-user spec)
- In a browser, anonymous: home page styled, Login and Register work end to end, and `/Account/Manage` redirects to login

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Post-deploy smoke and documentation

### Overview

Add a Playwright smoke that works against any deployed URL. It checks the deployed app beyond liveness: a signed-in
page, the interactive connection, and a real AI generation. Prove it on the local production-like stack, then update
the strategy and cookbook docs.

### Changes Required:

#### 1. Smoke config

**File**: `playwright.smoke.config.ts` (new)

**Intent**: Run the smoke against an already-running app with no local `webServer`.

**Contract**:

- `testDir: './tests/smoke'`.
- `baseURL` from `SMOKE_BASE_URL`. The config throws with a pointer to `test-stack.md` if it is missing.
- Reads only `SMOKE_`-prefixed variables from `.env` (same pattern as `playwright.config.ts:8-12`).
- Projects:
  - `smoke-setup` (`*.setup.ts`);
  - `smoke`, with `storageState: 'playwright/.auth/smoke.json'`, which depends on `smoke-setup`.
- `retries: 0`.
- Per-test timeout of at least 120 s, because generation's own deadline is 60 s.
- Trace and screenshot on failure.
- No `webServer`.

#### 2. Smoke sign-in setup

**File**: `tests/smoke/smoke.setup.ts` (new)

**Intent**: Sign in the dedicated smoke account, and register it on a fresh environment (Production has no
confirmation).

**Contract**:

1. Reads `SMOKE_USERNAME` / `SMOKE_PASSWORD`, and throws if either is missing.
2. Logs in through `/Account/Login` using the role and label locators of `tests/e2e/auth.setup.ts`.
3. On "Invalid login attempt.", registers through `/Account/Register` instead, then waits for a signed-in-only state
   (the "Logout" button).
4. Saves `playwright/.auth/smoke.json`.

#### 3. Smoke spec

**File**: `tests/smoke/deployed-app.smoke.spec.ts` (new)

**Intent**: Prove that the deployed app works for a signed-in user over the live interactive connection, including
the AI (risk #3).

**Contract**:

- Header comment naming risk #3 and the facet ("beyond liveness: signed-in page, circuit, real generation").
- **Test 1:** `request.get('/healthz')` → 200.
- **Test 2:**
  1. `goto('/products')`, then `waitForInteractive` (imported from `tests/e2e/support/blazor.ts`).
  2. Add a uniquely named product (`Date.now()` suffix) and expect it visible.
  3. `goto('/recipes')`, then `waitForInteractive`.
  4. Click "Zaproponuj przepisy".
  5. Expect the first "Pokaż przepis" button visible within 90 s. Click it, then expect "Ukryj przepis".
- **`afterEach`:** removes the product through the UI and asserts it is gone, tolerating a test that failed before
  adding it (pattern: `products-cross-user-isolation.spec.ts`).
- Role, label and text locators only, and no `waitForTimeout`.

#### 4. Environment template and npm script

**Files**: `.env.example`, `package.json`

**Intent**: Make the smoke runnable with one command and documented variables.

**Contract**:

- `.env.example` gains `SMOKE_BASE_URL` (default comment `http://localhost:8090` for local-prod), `SMOKE_USERNAME`
  and `SMOKE_PASSWORD`, plus a note that the smoke spends one real AI call.
- `package.json` gains `"smoke": "playwright test -c playwright.smoke.config.ts"`.

#### 5. Documentation

**Files**:

- `context/foundation/test-plan.md`;
- `context/foundation/test-stack.md`;
- `context/changes/deployment/deployment-plan.md`.

**Intent**: Record the new layer, the patterns and the smoke's handoff to F-01.

**Contract**:

- **`test-plan.md`:**
  - §3 row 2: status and change folder.
  - §3: the optional e2e sub-phase paragraph is marked dropped (covered by the smoke).
  - §4: HTTP integration row with its version, and "real login form" instead of "test auth scheme".
  - §5: the HTTP integration row is required. The post-deploy smoke row reads "pending F-01 (`deploy.yml`); until then
    run against `local-prod`".
  - §6.3: the factory, sign-in and route-list pattern.
  - §6.5: the smoke pattern and its command.
  - §6.6: Phase 2 notes, including the accepted unknown-URL → login consequence and the decision that the missing key
    stays a warning.
- **`test-stack.md`:** a `## Smoke` section covering the config, variables, local-prod run and cost note.
- **`deployment-plan.md`:**
  - a note at the planned `deploy.yml` smoke step and at Phase 7: the smoke replaces the `/healthz` curl and the
    Counter-page WebSocket check;
  - Phase 4 gains the `SMOKE_*` secrets.

### Success Criteria:

#### Automated Verification:

- `npx playwright test -c playwright.smoke.config.ts --list` lists the setup and both smoke tests without starting a server
- `npx playwright test` (the E2E config) still lists only `tests/e2e` specs
- `dotnet test` passes (no regressions from earlier phases)

#### Manual Verification:

- `./scripts/local-prod.ps1 -Reset` with an empty `ANTHROPIC_API_KEY`, then `npm run smoke` with `SMOKE_BASE_URL=http://localhost:8090`: the app starts after migrating the empty SQL Server database, setup registers the smoke user, the healthz test passes and the generation test fails on the missing recipes (red, as designed)
- With a valid dev key in `.env`, `./scripts/local-prod.ps1 -NoBuild` then `npm run smoke`: all green
- Re-running `npm run smoke` reuses the existing smoke user (login, no second registration) and leaves no smoke product behind
- Docs updates read correctly: test-plan §3/§4/§5/§6, test-stack `## Smoke`, deployment-plan notes

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None new. The existing `RecipeServiceCollectionExtensionsTests` stay as the DI-level check. Phase 1 adds the
  host-level proof above them.

### Integration Tests:

- `ProductionCompositionTests`: Production DI and pipeline, fail-fast cases, AI client settings, missing-key warning.
- `AccessTests`: anonymous redirect, and two-user prerendered isolation through the real cookie sign-in.
- `AuthorizationPolicyTests`: public and protected route lists, the coverage guard, static assets, healthz, unknown URL.
- All run in the required CI `gate` check, on SQLite, with no external services.

### Manual Testing Steps:

1. Run the E2E suite after Phase 3 (`npx playwright test`).
2. Use the anonymous browser walkthrough after Phase 3.
3. Run the smoke against `local-prod -Reset`: red with no key, green with a key, and a rerun reuses the account.

## Performance Considerations

- Each `WebApplicationFactory` boot costs about a second. Share one factory per test class through `IClassFixture` and
  create fresh factories only for the startup-failure facts. The gate's 15-minute timeout is unaffected.
- The smoke spends one real Anthropic call per run, bounded by the existing caps (`Recipes:MaxTokens`, 60 s deadline).

## Migration Notes

- No schema change.
- The fallback policy changes runtime behaviour: anonymous visitors to unknown URLs now land on login. Nothing else
  that is visible should change. Phase 2 and Phase 3 tests plus the E2E run are the guard.

## References

- Research: `context/changes/testing-production-composition-access/research.md`
- Strategy: `context/foundation/test-plan.md` §2 rows #3/#4, §3 Phase 2, §5, §6.3/6.5
- Deployment plan: `context/changes/deployment/deployment-plan.md:117,164-166,188-204,236-241`
- SQLite pattern: `KitchenAssistant.Tests/Support/SqliteTestDatabase.cs:10-50`
- E2E patterns: `tests/e2e/auth.setup.ts`, `tests/e2e/support/blazor.ts`, `tests/e2e/products-cross-user-isolation.spec.ts`
- Docs: Context7 `aspnetcore.docs`: integration tests ("Customize WebApplicationFactory"), "Require global user authentication"

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Production test host and composition tests

#### Automated

- [x] 1.1 `dotnet build` succeeds with the new package and factory — b2621ea
- [x] 1.2 `dotnet test --filter "FullyQualifiedName~ProductionCompositionTests"` passes — b2621ea
- [x] 1.3 Deliberate break: temporarily changing `MaxRetries = 1` to `2` in `Program.cs` turns the client-settings test red (then revert) — b2621ea
- [x] 1.4 `dotnet test` (whole suite) passes, and `scripts/ci/assert-tests-ran.ps1` reports executed tests — b2621ea

#### Manual

- [x] 1.5 First-run pins recorded in the phase commit message: `Program` visibility mechanism used, and that `AnthropicClient.Timeout`/`MaxRetries`/`ApiKey` were readable — b2621ea

### Phase 2: Access tests (anonymous redirect and prerendered isolation)

#### Automated

- [x] 2.1 `dotnet test --filter "FullyQualifiedName~AccessTests"` passes on unchanged app code — c18f61f
- [x] 2.2 Deliberate break: temporarily removing `@attribute [Authorize]` from `Products.razor` turns the anonymous `/products` case red (then revert) — c18f61f
- [x] 2.3 Deliberate break: temporarily replacing the user id passed to `GetProductsAsync` in `Products.razor` with another seeded user's id turns the isolation test red (then revert) — c18f61f
- [x] 2.4 `dotnet test` (whole suite) passes — c18f61f

#### Manual

- [x] 2.5 The pinned `Location` literal and the prerendered `/products` content were observed (not assumed) on the first run, noted in the phase commit message — c18f61f

### Phase 3: Fallback authorization policy (secure by default)

#### Automated

- [x] 3.1 `dotnet test --filter "FullyQualifiedName~AuthorizationPolicyTests"` passes — d5476cd
- [x] 3.2 Phase 1 and Phase 2 tests still pass unchanged: `dotnet test` — d5476cd
- [x] 3.3 Deliberate break: temporarily adding `@attribute [AllowAnonymous]` to `Components/Account/Pages/_Imports.razor` turns the Manage protected cases red (then revert) — d5476cd
- [x] 3.4 Deliberate break: temporarily adding a page `@page "/tmp-guard"` without listing it turns the coverage guard red (then delete) — d5476cd
- [x] 3.5 Deliberate break: temporarily removing `.AllowAnonymous()` from `MapStaticAssets()` turns the static-asset case red (then revert) — d5476cd
- [x] 3.6 `dotnet ef migrations has-pending-model-changes` exits 0 — d5476cd

#### Manual

- [x] 3.7 Local E2E still green against the Development build: `npx playwright test` (sign-in, enhanced navigation, cross-user spec) — d5476cd
- [x] 3.8 In a browser, anonymous: home page styled, Login and Register work end to end, and `/Account/Manage` redirects to login — d5476cd

### Phase 4: Post-deploy smoke and documentation

#### Automated

- [x] 4.1 `npx playwright test -c playwright.smoke.config.ts --list` lists the setup and both smoke tests without starting a server
- [x] 4.2 `npx playwright test` (the E2E config) still lists only `tests/e2e` specs
- [x] 4.3 `dotnet test` passes (no regressions from earlier phases)

#### Manual

- [x] 4.4 `./scripts/local-prod.ps1 -Reset` with an empty `ANTHROPIC_API_KEY`, then `npm run smoke` with `SMOKE_BASE_URL=http://localhost:8090`: the app starts after migrating the empty SQL Server database, setup registers the smoke user, the healthz test passes and the generation test fails on the missing recipes (red, as designed)
- [x] 4.5 With a valid dev key in `.env`, `./scripts/local-prod.ps1 -NoBuild` then `npm run smoke`: all green
- [x] 4.6 Re-running `npm run smoke` reuses the existing smoke user (login, no second registration) and leaves no smoke product behind
- [x] 4.7 Docs updates read correctly: test-plan §3/§4/§5/§6, test-stack `## Smoke`, deployment-plan notes
