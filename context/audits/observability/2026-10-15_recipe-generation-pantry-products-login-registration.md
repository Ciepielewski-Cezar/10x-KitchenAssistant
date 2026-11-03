---
type: observability-audit
date: 2026-10-15 23:03
mode: audit
commit: 7d51bcc
branch: main
dirty_tree: true          # only .claude/ skill files and CLAUDE.md modified; no application code
areas: [recipe-generation, pantry-products, login-registration]
area_source: foundation
runtime_proof: not-run    # no tracker SDK in code yet; framework-level probes only (see §8)
error_tracker: none in code; assumed Azure Monitor OpenTelemetry distro (App Insights) per deployment-plan Phase 8
previous_report: null
findings: { critical: 1, high: 6, medium: 25, low: 22 }
---

# Observability audit — recipe generation, pantry products, login & registration (2026-10-15)

## 1. TL;DR

- **The most likely production regression in the core flow is invisible.** If the model stops returning `prepTimeMinutes`, or returns product IDs that don't match, every proposal is hidden by app-side ranking. Users on the default parameters are told "Żadna propozycja nie mieści się w limitach… zmień parametry". Nothing above Information is logged, and nothing reaches the alert (R1). "Nothing usable" is a `LogError` with no exception, so it is a trace and not exception telemetry (R2).
- **Failures are handled as outcomes, not exceptions.** Identity results, recipe statuses and pantry Duplicate/NotFound outcomes become UI strings, 200 responses or 302 redirects with no exception and often no log. The planned alert ("failed requests / server exceptions > 3 in 15 min") only sees `LogError(ex, …)` and 5xx responses.
- **Interactive work has no identity.** `/products` and `/recipes` run on the Blazor circuit. A click there is not an HTTP request, and no code adds a user id, operation, generation id or release. Captured exceptions arrive as uncorrelated rows (X2, X6).
- **Log levels are tuned for the console, not for an exporter.** `Default=Information` exports every EF SQL command into a workspace capped at 0.1 GB/day; reaching the cap blinds the rest of the day. Meanwhile `Microsoft.AspNetCore=Warning` drops the only signals of mass logout (cookie unprotect), antiforgery failures and circuit or transport trouble (X3, X4, L1).
- **The startup migration runs outside the host.** `Program.cs:85-89` has no catch, no log and no flush. A bad migration takes the whole site down and leaves App Insights silent (X1).
- **The template throws on expected client states.** Stale 2FA, passkey or antiforgery state, and a bad `ReturnUrl`, become 500s. They pollute the same alert that is meant to catch real failures (L5, L7–L9).

## 2. Capture model

| Piece | State | Evidence |
|---|---|---|
| Runtime | One long-running ASP.NET Core process: a Linux container on Azure App Service B1, with Azure SQL Basic. `/products` and `/recipes` are `@rendermode InteractiveServer` with prerender (static SSR pass, then a SignalR circuit). Account pages are static SSR. | `Components/Pages/Products.razor:2`, `RecipeSuggestions.razor:2`, `Components/App.razor` |
| Error tracker | **None in code** (no package in `KitchenAssistant.csproj`). **Assumed (user-confirmed):** deployment-plan Phase 8 lands as written: `AddOpenTelemetry().UseAzureMonitor()` when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, plus an alert on failed requests or server exceptions (> 3 in 15 min). | `context/changes/deployment/deployment-plan.md:119,253,255` |
| What becomes exception telemetry (per distro docs) | An `ILogger` call that passes an `Exception` object, and unhandled request exceptions logged by `ExceptionHandlerMiddleware`. `LogError` without an exception is only a trace. 4xx and 3xx responses count as successful requests. | — |
| Static SSR boundary | `UseExceptionHandler("/Error")` (non-Development) logs at Error with the exception and returns 500. `UseStatusCodePagesWithReExecute("/not-found")` re-executes for any body-less 4xx or 5xx response. | `Program.cs:98`, `Program.cs:102` |
| Circuit boundary | No `ErrorBoundary`, no `CircuitHandler`. An unhandled circuit exception is logged by `CircuitHost` at Error with the exception; the circuit dies and the user sees `#blazor-error-ui`. **It is never a failed request.** In practice both interactive pages catch everything themselves and call `LogError(ex, …)`. | `Components/Routes.razor`, `MainLayout.razor:19`, `Products.razor:186,226,332,375`, `RecipeSuggestions.razor:162,216` |
| Outside every boundary | Startup migration (`Program.cs:85-89`), checks before `Build()` (`Program.cs:35,43,76`), client JS (`ReconnectModal.razor.js`, `PasskeySubmit.razor.js`). | — |
| Logging | Default Microsoft.Extensions.Logging providers writing to console. All templates are structured (0 interpolated messages). `Default=Information`, `Microsoft.AspNetCore=Warning`; there is no Production override. | `appsettings.json:2-7` |
| Scrubbing | No processors or redaction code. EF sensitive-data logging is off. | — |
| Deploy identity | No `service.name`, `service.version` or `deployment.environment` in code. The local compose stack also runs as `Production`, so only the presence of the connection string separates it from Azure. | `compose.yaml:31-37` |
| Background work | No hosted services or fire-and-forget tasks. The only background loop is the framework's 30-minute auth revalidation. | `IdentityRevalidatingAuthenticationStateProvider.cs` |

**Assumptions (outside the repo, not findings), all unconfirmed:**
- Phase 8 Azure Monitor wiring and the App Insights resource are created as planned.
- The alert rule uses the **exceptions** signal, not only `requests/failed`. Only an exceptions signal can see circuit failures.
- App Service container stdout/stderr is collected (`AppServiceConsoleLogs` diagnostic setting). Without it, X1 leaves no trace anywhere.
- The `/healthz` availability test and its action group exist (deployment-plan Phase 8).
- The 0.1 GB/day Log Analytics cap is applied as planned, and someone is notified when the cap is reached.
- WebSockets are enabled and ARR affinity is set on App Service.

## 3. What reaches the tracker (static analysis, not runtime-observed)

| Failure shape | Response | Platform logs | Tracker (assumed distro) | Verdict |
|---|---|---|---|---|
| Unhandled exception in SSR page or minimal API | 500 `/Error` | Error + stack | exception + failed request | good |
| Exception caught in `Products`/`RecipeSuggestions` with `LogError(ex)` | circuit stays up, friendly alert | Error + stack | exception, no user/operation, plus duplicate EF exceptions | poor (X2, X5) |
| Unhandled circuit exception | `#blazor-error-ui`, circuit dies | Error + stack (CircuitHost) | exception only, never a failed request | poor (X2) |
| AI returns nothing usable | "Nie udało się wygenerować przepisów." | Error trace, no exception | trace only, no alert | **missed** (R2) |
| AI output drift hides every proposal | "nie mieści się w limitach" (looks like user error) | Information | nothing alertable | **missed** (R1) |
| Anthropic 401/429/529 | same failure message | Error + stack, status only in message text | exception, one bucket | poor (R4) |
| SDK retry then deadline | same failure message | Error "timed out", original 429/529 lost | exception without cause | poor (R3) |
| DB outage during prerender | 200 with alert | Error + stack ×3 (EF + app) ×2 (prerender + circuit) | ~6 exceptions per page view | noisy (X5, P4) |
| Startup migration fails | App Service 503, crash loop | stderr only | **nothing** | **missed** (X1) |
| Data Protection keys lost | everyone 302 to Login, stale POSTs 400 | Debug/Information (filtered) | nothing | **missed** (L1) |
| Systemic sign-in failure (e.g. `IsNotAllowed`) | 200 "Invalid login attempt." | Debug only | nothing | **missed** (L2) |
| Account Manage `IdentityResult` failure | 200/302 "Error: …" | nothing | nothing | **missed** (X8) |
| Stale 2FA / passkey / antiforgery state | 500 | Error + stack | exception + failed request (noise) | noisy (L7–L9) |

## 4. Systemic root causes

1. **Failures are modelled as outcomes and leave the exception path.**
   - **Mechanism:** `RecipeService.cs:80-95` returns `Failed` / `NoneWithinLimits` after a trace-only `LogError` or `LogInformation`. `Login.razor:145`, `ChangePassword.razor:81` and `IdentityRedirectManager.cs:40-63` render `IdentityResult`/`SignInResult` failures as text. `ProductService.cs:98-104,117-120,146-157` maps exceptions and misses to `Duplicate`/`NotFound` with no log. `IdentityNoOpEmailSender.cs:8-19` reports success.
   - **Why it exists:** a deliberate UX choice (friendly messages, no crashes), and the template's defaults.
   - **Cost:** the only alert is exception-based, so these are invisible.
   - **Explains:** R1, R2, R8, P1, P3, L2, L3, L6, L12, L15, X8.
2. **Circuit work carries no operation, user or release identity.**
   - **Mechanism:** the interactive pages run outside any HTTP request (`Products.razor:2`, `RecipeSuggestions.razor:2`). There is no `BeginScope` anywhere, no `CircuitHandler`, no app `ActivitySource`, and the planned Phase 8 line adds no Blazor sources and no `ConfigureResource`. `Error.razor:36` shows a raw traceparent.
   - **Explains:** X2, X6, R9–R11, X11, L16.
3. **Log-level configuration is console-era.**
   - **Mechanism:** `appsettings.json:4` (`Default=Information`) exports EF `Executed DbCommand` and duplicates EF's own Error logs next to the app's. `appsettings.json:5` (`Microsoft.AspNetCore=Warning`) drops cookie-unprotect, antiforgery, circuit and transport events, which the framework logs at Debug or Information.
   - **Explains:** X3, X4, X5, L1.
4. **Failures are classified by free text, and causes are lost on the way.**
   - **Mechanism:** one `RecipeGenerationException` type covers every provider outcome (`IRecipeGenerator.cs:10`). The Anthropic status lives only in `ex.Message` (`RecipeService.cs:118-121`). SDK retries are unlogged and the deadline masks their cause (`Program.cs:71-74`). `DbUpdateException` is classified by re-querying state rather than by `SqlException.Number` (`ProductService.cs:98,146`). `IdentityResult.Errors` are dropped (`DeletePersonalData.razor:81`, `Disable2fa.razor:64`).
   - **Explains:** R3–R7, P1, P2, X9, L11.
5. **Template code throws on expected client states.**
   - **Mechanism:** `?? throw` in `LoginWith2fa.razor:66-67` and `LoginWithRecoveryCode.razor:53`; `antiforgery.ValidateRequestAsync` in `IdentityComponentsEndpointRouteBuilderExtensions.cs:59,85`; `ToBaseRelativePath` in `IdentityRedirectManager.cs:24-28`; `PasskeySignInAsync` at `Login.razor:113`; `LocalRedirect($"~/{returnUrl}")` at `IdentityComponentsEndpointRouteBuilderExtensions.cs:50`.
   - **Effect:** ordinary stale-tab or bot traffic becomes 500s in the alert.
   - **Explains:** L5, L7, L8, L9, L11, X14.
6. **Startup work runs outside the host lifetime.**
   - **Mechanism:** `Program.cs:85-89` (`MigrateAsync` between `Build()` and `Run()`, no try/catch, no `DisposeAsync`), so OTel exporters never flush.
   - **Explains:** X1, X10.

## 5. Findings by area

### Recipe generation

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| R1 | `Recipes/RecipeService.cs:86-95`, `RecipeRanker.cs:17`, `RecipeSchema.cs:27-29`, `RecipeService.cs:74-77` | flattened-response | **critical** | The schema allows `prepTimeMinutes: null`, and the ranker hides null-time proposals whenever a limit is set (default 30 min). The same happens with unknown or wrong `productId`s, which push missing counts over `MaxMissing=2`. **Users:** every user gets a yellow "zmień parametry albo dodaj produkty" warning that blames their own choice. **Responders:** an Information line plus an optional Warning; no exception, no alert. | When proposals are hidden for a *structural* reason (all null times, unknown IDs), emit a Warning or Error with a stable EventId and alert on its rate. Keep the user-limit case at Information. |
| R2 | `Recipes/RecipeService.cs:80-83` | logged-not-captured | high | An empty `recipes`, or recipes without title or steps, produces `LogError` **without an exception**, which is a trace only. Every user sees "Nie udało się wygenerować przepisów."; the exceptions alert stays silent. The log has no answer size or sample. | Throw a `RecipeGenerationException` (counts in `Data`) into the existing catch, or pass an exception object. Add response length and a redacted excerpt. |
| R3 | `Program.cs:71-74`, `RecipeService.cs:100-111`, `appsettings.json:17` | identity-lost | high | When a 429/529/IO error triggers the SDK's silent retry and the retry then hangs, only "timed out after the 60 s deadline" (`TaskCanceledException`) is logged; the original status never reaches app logs (the SDK has no logger). The per-attempt timeout (60 s) equals the deadline (60 s), so the "per-attempt timeout" branch is effectively unreachable. | Add a `DelegatingHandler` via `AnthropicClient.Handlers` that logs each attempt's status, `request-id` and elapsed time; set the per-attempt timeout below the deadline. |
| R4 | `Recipes/RecipeService.cs:118-121` | identity-lost | medium | 401 (revoked key, permanent, all users), 400 (bad model id), 429 and 529 (transient) share one template; the status exists only inside `ex.Message` (`"Status Code: 529\n<body>"`, verified in SDK 12.50.0). Triage needs text search; an overload burst fires the same alert as a dead key. | Log `{StatusCode}`, `{ErrorType}` and `{ExceptionType}` as structured fields from `AnthropicApiException`. Use Error for 4xx and Warning with a rate rule for 429/529/IO. |
| R5 | `Recipes/IRecipeGenerator.cs:10`, `AnthropicRecipeGenerator.cs:21,76,82`, `RecipeResponseParser.cs:35,38` | identity-lost | medium | Missing key, refusal, `max_tokens`, context window exceeded, empty content and malformed JSON are all one exception type, so App Insights groups them as one problem. | Add a `Kind` enum logged as a field (or subclasses). Wrap the deadline as `Kind=Timeout` with the OCE as the inner exception. |
| R6 | `Recipes/AnthropicRecipeGenerator.cs:74-77` | identity-lost | medium | A refusal logs "stopped with 'refusal'" without `StopDetails` or `Message.ID`. Product names are user input, so one user's data can cause repeated refusals that nobody can trace. | Log `StopDetails` and `Message.ID` for non-`end_turn` stops. |
| R7 | `Recipes/RecipeResponseParser.cs:33-38`, `AnthropicRecipeGenerator.cs:79-83` | identity-lost | medium | Malformed output keeps the inner `JsonException` but not the text length or an excerpt, so a structured-output regression can't be diagnosed after the fact. | Add length and a redacted first ~200 characters to `exception.Data`. |
| R8 | `Program.cs:80-83`, `AnthropicRecipeGenerator.cs:19-22` | logged-not-captured | medium | A missing key in Production is only a startup Warning trace. The first alertable signal is the 4th failed user click within 15 minutes. A Fake generator outside Development already fails startup (`RecipeServiceCollectionExtensions.cs:12-16`); the key does not. | Fail startup outside Development when the key is missing, or log at Error/Critical with an exception at startup. |
| R9 | `Components/Pages/RecipeSuggestions.razor:58-64,216-220` | missing-context | medium | Timeout, 401, 529, refusal, missing key and DB bug all render the same message with no reference, so a support report can't be tied to an event. | Show a short generation or trace id in the alert (see X2). |
| R10 | `Recipes/RecipeService.cs:30`, `RecipeSuggestions.razor:218` | missing-context | low | A DB failure while re-loading products is logged as "Failed to generate recipes." with no stage. | Log the stage (load, call, parse, classify) in the scope. |
| R11 | `Recipes/AnthropicRecipeGenerator.cs:44-52` | missing-context | low | The success log lacks `Message.ID`, so a complaint about a bad recipe can't be traced to the provider call. | Add `{MessageId}`. |
| R12 | `RecipeSuggestions.razor:212-215`, `AnthropicRecipeGenerator.cs:31-41` | missing-context | low | Users who leave mid-wait (the ≤ 1 min NFR at risk) look identical to timeouts in the Information line (`TaskCanceledException`). | Log a distinct "abandoned" event with elapsed time. |

### Pantry products

The flow is in good shape: all four page catch sites log with the exception, unclassified `DbUpdateException`s are rethrown, and a load failure shows an alert rather than an empty list. The context and noise gaps are covered under X2 and X5.

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| P1 | `Pantry/ProductService.cs:98-107,146-160` | identity-lost | medium | Any `DbUpdateException` is classified by re-querying, never by `SqlException.Number` (2601/2627/547/1205), and nothing is logged. Example: a lost commit ack is retried by `EnableRetryOnFailure`, hits the unique index, and the user is told their just-added product "is already" there, while telemetry shows only EF's generic error. | Check the inner SqlException number. Log the explained outcome with `{SqlErrorNumber, UserId, ProductId}`. |
| P2 | `Pantry/ProductService.cs:101,149,154` | identity-lost | medium | Re-queries inside the catch can throw (retry limit, cancellation) and replace the original write exception. If `ct` was cancelled, `Products.razor:222,328` treat it as "user left", so nothing is recorded at all. | Log the original exception before re-querying, or chain it as the inner exception. |
| P3 | `Pantry/ProductService.cs:117-120,131-135,149-152,170`; `Products.razor:309-316,366-369` | missing-throw | medium | NotFound on update or delete shows "Ten produkt został już usunięty." and logs nothing. A user-scoping or ownership regression would look exactly like this. | `LogWarning("Product {ProductId} not found for user {UserId} during {Operation}")`; alert on spikes. |
| P4 | `Components/Pages/Products.razor:182-193` | flattened-response | medium | During a DB outage, a full-page GET `/products` renders an alert with **200**, so failed requests never move. The `catch (Exception)` treats programming bugs and outages the same way. | Keep the exception log, tag the failure class, and set 503 during prerender when the load fails. |
| P5 | `Program.cs:38`; `ProductService.cs:110-161` | missing-context | medium | `EnableRetryOnFailure()` defaults (6 retries, up to 30 s) apply per context, and an update opens 3–5 contexts. In a brownout, "Zapisz" spins for minutes with only uncorrelated EF retry warnings. | Bound the retries for interactive use, or put one deadline over the operation. Record the duration (X2). |
| P6 | `Products.razor:163,173,211,299,359` | missing-throw | low | A missing `NameIdentifier` claim surfaces as a generic `ArgumentNullException` under four different "Failed to …" messages. | Throw a specific exception in `OnInitializedAsync`. |
| P7 | `Products.razor:422-426` | noise | low | *Plausible, unverified:* a continuation after `Dispose` reads a disposed `cts.Token`, and the resulting `ObjectDisposedException` is logged as Error "Failed to load products." | Cancel without disposing, and filter the catch with `when (!cts.IsCancellationRequested)`. |
| P8 | `ProductService.cs:177-197`; `Products.razor:207-208,295-296` | flattened-response | low | Drift between form validation and service validation becomes "Spróbuj ponownie" forever, lumped in with DB errors. Captures outside the try can kill the circuit. | Catch `ArgumentException` separately as a contract bug, and move the captures inside the try. |
| P9 | `ProductService.cs:59,131,168,202,210` | missing-throw | low | Privacy depends on hand-written `Where` clauses; only tests would notice a regression. | Add an EF global query filter on `UserId`, or a cheap post-query assertion. |

### Login & registration

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| L1 | `Program.cs:58-60`, `appsettings.json:5` | logged-not-captured | high | A DB restore, new database or slot swap empties `DataProtectionKeys`. Cookie unprotect then fails (Information under `Microsoft.AspNetCore.Authentication`, filtered out; levels verified in the 10.0.12 framework), so everyone is 302'd to Login and open forms POST into 400s. **Responders:** no exception and no warning, only 302s and 400s. | Raise `Microsoft.AspNetCore.Authentication`, `.Antiforgery` and `.Components.Endpoints` to Information, and/or wrap `TicketDataFormat` to log a Warning or metric when a present cookie fails to unprotect. |
| L2 | `Components/Account/Pages/Login.razor:128-147`, `Program.cs:48` | flattened-response | high | `IsNotAllowed`, a wrong password and an unknown user all become "Error: Invalid login attempt." with a 200; the framework logs this at Debug only (verified). `RequireConfirmedAccount` defaults to `true` in code, and only `appsettings.Production.json` turns it off. If that file isn't applied, every existing user (none confirmed) fails with a correct password, and responders see only 200s. | Branch on `IsNotAllowed` and log a Warning with user id and reason. Fail startup when `RequireConfirmedAccount` is true while `IdentityNoOpEmailSender` is wired. |
| L3 | `ForgotPassword.razor:48-53,64`, `IdentityNoOpEmailSender.cs:8-19`, `Manage/Email.razor:108,133`, `ResendEmailConfirmation.razor:65` | swallowed | medium | Users are told "check your email" and nothing is sent. No production email is ever confirmed, so reset short-circuits at `:49`. There is no log at any level. This is a known product gap, but it is silent. | The no-op sender logs a Warning, or hide these links until a real sender exists. |
| L4 | `Register.razor:81-110` | flattened-response | medium | The user row commits at `:81`. A later throw (token generation, which depends on Data Protection, then email, sign-in, redirect) gives a 500 with no user id, and the retry then shows "Username already taken" with a 200 and no log. | Log the user id after create, and skip the token and email when there is no real sender. |
| L5 | `IdentityRedirectManager.cs:24-28` | noise | medium | `ToBaseRelativePath` throws for off-site, `//evil` or malformed `ReturnUrl` after a **successful** sign-in (verified). The result is a 500, and the auth cookie is lost because the exception handler clears headers. Bots probing for open redirects feed the alert. | Fall back to `/` with an Information log. |
| L6 | `Login.razor:125` | coverage-gap | medium | `lockoutOnFailure: false`, failures logged at Debug, unknown users at nothing. Credential stuffing leaves no signal. | Enable lockout, and log failed attempts at Information with a hashed username. Export the Identity meter (L10). |
| L7 | `Login.razor:113` | noise | medium | `PasskeySignInAsync` throws "No passkey assertion is underway" for a stale tab or expired state (verified), giving a 500 in the alert. | Catch it, log a Warning, and show "passkey sign-in expired". |
| L8 | `IdentityComponentsEndpointRouteBuilderExtensions.cs:59,85`; `PasskeySubmit.razor.js:43,98-107` | noise | medium | Conditional mediation POSTs `/Account/PasskeyRequestOptions` on every Login view. A stale antiforgery token throws, so a hidden 500 per view that the user never sees (`console.error` only). | Use `IsRequestValidAsync`, return 400 and log a Warning. |
| L9 | `LoginWith2fa.razor:66-67`, `LoginWithRecoveryCode.razor:52-53` | noise | medium | An expired 2FA cookie (reload, back button, crawler) gives a 500 and an English error page. This is already documented in `AuthorizationPolicyTests.cs:69-71`. | Redirect to Login with a status message. |
| L10 | Phase 8 `UseAzureMonitor()` (deployment-plan.md:119) | missing-context | low | The .NET 10 `Microsoft.AspNetCore.Identity` meter (sign-in results, `error.type`) isn't exported, so the cheapest failure-ratio signal is lost. | `.WithMetrics(m => m.AddMeter("Microsoft.AspNetCore.Identity"))`. |
| L11 | `ConfirmEmail.razor:45-47`, `ResetPassword.razor:67,80-87` | noise | low | A truncated `code` throws `FormatException`, giving a 500. Token failures come back as `InvalidToken` and are shown without codes. | Catch `FormatException`, and log the result codes. |
| L12 | `Register.razor:83-87`, `ExternalLogin.razor:184-187`, `ResetPassword.razor:87` | logged-not-captured | low | `IdentityResult` errors are rendered but logged only at Debug by the framework, so a spike is invisible. | Log the error codes at Information. |
| L13 | `ExternalLogin.razor:77,128-131,156-183` | swallowed | low | Dormant (no external provider registered). A failed `AddLoginAsync` leaves an orphan account silently. | Fix before adding any provider. |
| L14 | `Register.razor:119-123`, `ExternalLogin.razor:196-200` | identity-lost | low | A bare `catch` rethrows a new exception without the inner one. Unreachable today. | Pass the inner exception. |
| L15 | `IdentityRedirectManager.cs:62-63` | missing-throw | low | A signed-in principal whose user row is missing (wrong or restored DB) is 302'd to InvalidUser with no log. 14 Manage pages use this path. | Log a Warning with the claim user id. |
| L16 | `IdentityRevalidatingAuthenticationStateProvider.cs:31-44` | missing-context | low | A security-stamp mismatch force-signs-out circuits with no reason logged. DB exceptions there *are* logged at Error by the base class. | Log the user id and reason at Information. |
| L17 | `Login.razor:103-107` | flattened-response | low | Browser-side passkey errors (including "server responded 500") become "Error: …" with a 200 and no server log. | Log `Passkey.Error` at Information. |
| L18 | `Login.razor:141`, `LoginWith2fa.razor:83,88`, `LoginWithRecoveryCode.razor:71,76`, `PasskeySubmit.razor.js:32` | noise | low | Expected user mistakes are logged at Warning. The username is not URL-encoded (`+` becomes a space, the lookup returns null silently) and is placed in the query string, which is a privacy issue (see X15). | Log these at Information. Use `encodeURIComponent`, or move the username into the POST body. |

### Platform / plumbing

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| X1 | `Program.cs:85-89` | coverage-gap | high | A failing `MigrateAsync` (SQL unreachable, bad migration) escapes `Main` with no ILogger call and no host dispose, so the OTel exporter never flushes. App Service serves 503s and crash-loops. **Responders:** the availability test fires, App Insights is empty, and the cause sits only in container stderr. deployment-plan.md:134 already names "forgotten migration" as a risk. | try/catch → `app.Logger.LogCritical(ex, …)` → `await app.DisposeAsync()` → rethrow. Or run migrations as a deploy step that fails the pipeline. |
| X2 | `Products.razor:2`, `RecipeSuggestions.razor:2`, `Routes.razor`; no `BeginScope`/`CircuitHandler`/`ActivitySource` anywhere; `Products.razor:188,228,334,377`; `RecipeService.cs:23-121`; `Login.razor:130` | missing-context | high | Every product and recipe action runs on the circuit with no HTTP request. Exceptions and the SQL and Anthropic dependency spans from one click arrive as unrelated rows with no user, no operation, no duration and no generation id. You can't answer "one user or everyone?" or "which click?", and circuit failures never count as failed requests. | Add a `CircuitHandler` that opens a log scope with `UserId` (the GUID, never the email) and `CircuitId`. Add a scope or Activity per operation in `RecipeService`/page handlers. In Phase 8, `.WithTracing(t => t.AddSource("Microsoft.AspNetCore.Components*", app source))`. **The alert must use the exceptions signal.** |
| X3 | `appsettings.json:4` | config | medium | `Default=Information` exports every EF `Executed DbCommand` (with SQL text) as a trace. Every page view runs at least 2 queries (prerender plus circuit), and an update runs 3–5 contexts. At the planned 0.1 GB/day cap, ingestion stops and **exceptions are dropped for the rest of the day**. | `"Microsoft.EntityFrameworkCore.Database.Command": "Warning"` (keeps retry warnings and errors). Watch daily ingestion. |
| X4 | `appsettings.json:5` | config | medium | `Microsoft.AspNetCore=Warning` drops cookie unprotect, antiforgery failures, circuit evictions and reconnect rejections, and SignalR transport fallbacks (Debug/Information in the framework). A Data Protection or WebSockets problem is invisible (see L1). | Raise `…Authentication`, `…Antiforgery`, `…Components.Server.Circuits` and `…Http.Connections` to Information. |
| X5 | `Products.razor:2,186…`, `RecipeSuggestions.razor:2,162`; EF `CommandError`/`SaveChangesFailed` logs | noise | medium | One DB failure produces about 3 exception items (EF command, EF query or save, app), and prerender doubles that to about 6 per page view. One user trips the "> 3 in 15 min" alert, and counts stop meaning "users affected". The expected unique-index race (P1) also emits 2 EF Error exceptions. | Lower `Microsoft.EntityFrameworkCore.Database.Command`/`.Update` errors (the app log has the context), or alert on distinct operation or user. Skip the load during prerender, or tag it with `RendererInfo.IsInteractive`. |
| X6 | Phase 8 line (deployment-plan.md:119); no `ConfigureResource` | missing-context | medium | There is no `service.version` or `deployment.environment`, so an error spike can't be tied to a deploy or SHA. The local compose stack also reports `Production`. | `ConfigureResource(r => r.AddService("kitchen-assistant", serviceVersion: InformationalVersion).AddAttributes(deployment.environment.name))`. |
| X7 | `ReconnectModal.razor.js:19,37,42,52,54`; `MainLayout.razor:19`; no client telemetry | coverage-gap | medium | WebSocket or ARR misconfiguration and flapping circuits are invisible. Rejected circuits trigger silent `location.reload()`, losing form input, and `#blazor-error-ui` displays are never reported. | A small client beacon (`/client-log`, or the App Insights JS SDK) for reconnect failures and error-UI shows. |
| X8 | `Manage/ChangePassword.razor:79-82`, `SetPassword.razor:76-79`, `ConfirmEmailChange.razor:52-65`, `ExternalLogins.razor:122-124`, `Passkeys.razor:120-130,174-176`, `RenamePasskey.razor:80-82` | swallowed | medium | Store or concurrency failures become "Error: …" with a 200 or 302 and **no log**, so Account Manage can be broken without anyone knowing. | `LogWarning` with structured `IdentityResult.Errors` codes; log non-validation codes at Error. |
| X9 | `Manage/DeletePersonalData.razor:79-81`, `Manage/Disable2fa.razor:62-64` | identity-lost | medium | The 500 is captured, but "Unexpected error occurred deleting user." discards `IdentityResult.Errors`, so a GDPR delete failure can't be diagnosed. | Put the error codes in the exception message or log them first. |
| X10 | `Program.cs:35,43,76`; `RecipeServiceCollectionExtensions.cs:14,43` | coverage-gap | low | Fail-fast config and ICU checks before `Build()` are visible on stderr only. This is acceptable by design. | Document that console logs are the source for startup failures. |
| X11 | `Components/Pages/Error.razor:7-26,36` | missing-context | low | The page shows a raw traceparent (`00-<trace>-<span>-01`), so a responder must cut out `operation_Id`. Without a listener it shows `TraceIdentifier`, which no log contains. The page is English with dev advice in a Polish UI. | Show `Activity.Current?.TraceId.ToHexString()`, localise, and drop the dev text. |
| X12 | `Program.cs:102` | flattened-response | low | Every body-less 4xx/5xx renders "Not Found". The recorded operation name may collapse to `/not-found` (unverified). | A status-aware page; verify `http.route` once Phase 8 is live. |
| X13 | `Components/Ui/ElementFocus.cs:15-20` | swallowed | low | `JSException` (a real interop bug) is swallowed alongside the expected `JSDisconnectedException`. | Log `JSException` at Debug or Warning. |
| X14 | `IdentityComponentsEndpointRouteBuilderExtensions.cs:50` | noise | low | `LocalRedirect($"~/{returnUrl}")` with a `returnUrl` starting with `/` gives `~//…`, which throws, so a 500. | Validate the URL and fall back to `~/`. |
| X15 | `PasskeySubmit.razor.js:32` | config | low | The email goes into the query string of `/Account/PasskeyRequestOptions`, and so into request telemetry and App Service HTTP logs. | Move it into the POST body, and confirm query redaction in the instrumentation. |

## 6. Recommended fix order

1. **Make the recipe flow's silent failures alertable** (R1, R2; also R8). This is about 20 lines in `RecipeService.cs` and `Program.cs` and closes the only critical finding. Distinguish structural hiding (null times, unknown IDs) from user-limit hiding; only the former is an anomaly.
2. **Wrap the startup migration** (X1, X10). About 6 lines, and it removes total blindness for the most likely deploy failure.
3. **Tune log levels for the exporter** (X3, X4, X5, L1). One edit to `appsettings.json` / `appsettings.Production.json`. **Constraint:** raising `Microsoft.AspNetCore.*` categories to Information adds volume, so do it together with lowering EF commands, and check daily ingestion against the 0.1 GB cap.
4. **Give circuit work an identity** (X2, X6, X11, R9–R11, L16). Add a `CircuitHandler` log scope with the user GUID (never the email), an operation scope in `RecipeService`, and in Phase 8 `AddSource`/`ConfigureResource` with the version. Do this *with* Phase 8 so the alert is defined on **exceptions**. **Privacy:** user GUID only; no product names or emails in dimensions.
5. **Classify AI failures structurally** (R3–R7). Add an attempt-logging `DelegatingHandler`, a structured `StatusCode`/`Kind`, and set the per-attempt timeout below the deadline.
6. **Log Identity decision points** (L2, L3, L6, L10, L12, X8, X9). Branch on `IsNotAllowed`, export the Identity meter, log `IdentityResult` codes, make the no-op sender warn, and add a startup guard for `RequireConfirmedAccount` with no sender.
7. **Stop expected client states from throwing** (L5, L7, L8, L9, L11, X14). This is noise reduction, so do it before tightening the alert threshold.
8. **Pantry classification and NotFound visibility** (P1–P5).
9. **Client-side beacon** (X7) once the server side is solid.

## 8. Method and limits

- **Agents:** four read-only subagents ran in parallel: one each for recipe generation, pantry products and login & registration, plus one for plumbing. Each received the capture model above.
- **Framework-level probes (by subagents, in the scratchpad only, no repo changes):**
  - reflection over Anthropic SDK 12.50.0: exception hierarchy, message format, retry and timeout behaviour, using `dotnet fsi`;
  - `[LoggerMessage]` levels in the ASP.NET Core 10.0.12 shared framework (Identity, Authentication, Data Protection);
  - `SignInManager`/`UserManager` against EF InMemory with Trace logging;
  - `NavigationManager.ToBaseRelativePath` throw behaviour.
- **Spot-checked by hand:**
  - `RecipeService.cs:80-95` (trace-only Error, Information on all-hidden);
  - `RecipeRanker.cs:17` + `RecipeSchema.cs:27-29` + default 30-minute limit (R1);
  - `Program.cs:85-89` (no catch);
  - `LoginWith2fa.razor:66-67` + `AuthorizationPolicyTests.cs:69-71` (documented 500);
  - `IdentityComponentsEndpointRouteBuilderExtensions.cs:50,59,85`;
  - `DeletePersonalData.razor:79-81` and `ChangePassword.razor:79-82`;
  - `Login.razor:125-146` (`lockoutOnFailure: false`, `IsNotAllowed` unhandled);
  - `ForgotPassword.razor:48-53`;
  - `IdentityRedirectManager.cs:24-28`;
  - render modes of both interactive pages.
  
  All held.
- **Severity adjustments versus the subagents:**
  - L1 and L2 were reported as critical and are downgraded to high: users can sign back in, and L2 depends on a config key being lost.
  - X1 is high, not critical: the availability test fires; only the cause is missing.
  - Per-area context findings (pantry H1/H2, recipe #3, login #11) are merged into X2 as one mechanism.
- **Not verified:**
  - Distro behaviour (`Azure.Monitor.OpenTelemetry.AspNetCore` is not installed): ILogger-to-exception mapping, 4xx success scoring, route naming after status-code re-execute, query redaction.
  - P7 (dispose race) and the revalidation sign-out path are inferred from framework documentation.
  - Production log rendering was not observed.
- **Runtime proof:** not run. Without a tracker SDK in code, fake-ingest probes would first need the Phase 8 package added in a worktree. That is the natural before/after harness once Phase 8 is implemented.
- **Repo-wide sweep** (Program.cs, Components/, Pantry/, Recipes/, Data/ excluding Migrations and tests):

  | Pattern | Count |
  |---|---|
  | Empty C# catches | 2 |
  | Effectively empty JS catches | 2 |
  | Bare `catch` | 2 |
  | Catch → result or redirect without log | 4 |
  | `catch (Exception)` that only sets UI state without logging | 0 |
  | `LogError`/`LogWarning` with exception | 10 |
  | `LogError`/`LogWarning` without exception | 8 (one Error: `RecipeService.cs:82`) |
  | `throw new` dropping a cause | 4 |
  | Interpolated log messages | 0 |
- **Probe harness:** none created. No worktrees or temp files were left in the repo.
