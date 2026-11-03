# Observability Quick Wins Implementation Plan

## Overview

This change applies the first three items of the observability audit's fix order
(`context/audits/observability/2026-10-15_recipe-generation-pantry-products-login-registration.md` §6, items 1-3):

1. **R1/R2:** the recipe flow's silent failures become alertable. "Nothing usable" reaches the exceptions signal, and
   proposals hidden for a *structural* reason (AI output drift) log a Warning with a stable EventId.
2. **X1:** a failing startup migration is logged at Critical with its exception, and the host is disposed (exporters
   flushed) before the process dies.
3. **X3/X4:** Production log levels are tuned for an exporter: EF SQL commands go down to Warning, and the
   authentication, antiforgery and circuit categories come up to Information.

These are the cheapest, widest gaps. They land before deployment-plan Phase 8 adds the Azure Monitor OpenTelemetry
distro and its alert on failed requests or server exceptions (> 3 in 15 min). Under that distro only an `ILogger` call
that carries an `Exception` becomes exception telemetry.

Every fix starts with a failing regression test (CLAUDE.md, Module 3 Lesson 5).

## Current State Analysis

- **R2, "nothing usable"** (`Recipes/RecipeService.cs:80-84`). `LogError` is called with no exception, then `Failed`
  is returned. Under the distro this is a trace, so the exceptions alert never sees it.
- **R1, everything hidden** (`Recipes/RecipeService.cs:86-95`). This is always `LogInformation`, whatever the cause.
  - The ranker hides a proposal with `PrepTimeMinutes == null` whenever a time limit is set (`RecipeRanker.cs:17`).
    The default limit is 30 min, and the schema allows `null`.
  - Unknown product IDs never fall back to a name match (`RecipeClassifier.cs:118-121`). They count as missing, which
    pushes proposals over `MaxMissing = 2`.
  - Either way the user sees "nie mieści się w limitach" and nothing alertable is logged.
  - The separate unknown-ID Warning (`RecipeService.cs:74-77`) has no EventId.
- **No EventIds anywhere** in app code (repo-wide grep: none outside the test logger signature).
- **Exception path already exists.** `catch (RecipeGenerationException ex)` (`RecipeService.cs:113-117`) logs at
  Error with the exception and returns `Failed`. A throw from inside the `try` lands there.
- **X1, startup migration** (`Program.cs:85-89`). `MigrateAsync` runs between `Build()` and `Run()` with no
  try/catch, no log and no dispose. The exception escapes `Main`, and logger providers (and so the future OTel
  exporter) are never disposed or flushed.
- **X3/X4, log levels.**
  - `appsettings.json:5-8` sets `Default=Information` and `Microsoft.AspNetCore=Warning`.
  - `appsettings.Production.json` has no `Logging` section.
  - EF's `Executed DbCommand` (Information, with SQL text) is exported on every query.
  - Cookie unprotect, antiforgery and circuit events (Debug/Information in the framework) are dropped.
- **Test seams.**
  - `RecipeServiceTests` builds `RecipeService` with `NullLogger` (`RecipeServiceTests.cs:43-44`).
  - `ProductionAppFactory` runs the real `Program.cs` in Production on in-memory SQLite.
    - Its `CapturedLogs` keeps only `(Level, Message)` and is registered with `AddProvider(instance)`, so DI never
      disposes it (`Support/ProductionAppFactory.cs:65,102-123`).
    - Startup exceptions surface from `CreateNoRedirectClient()` unwrapped (`ProductionCompositionTests.cs:24-30`).
    - Code after `Build()` runs: the missing-key Warning test (`ProductionCompositionTests.cs:93`) proves it.
    - The factory calls `EnsureCreated()` before the host starts, so `MigrateAsync` against that database fails
      deterministically. Either the pending-model-changes check fires (a SQL Server snapshot against a SQLite model)
      or the tables already exist.

## Desired End State

- **None usable.** When the AI returns nothing usable, `RecipeService` throws a `RecipeGenerationException` into its
  own catch. That logs at Error **with the exception** and the stable EventId `RecipeLogEvents.GenerationFailed`.
  The exception's `Data` carries `RecipeCount` and `ResponseLength`. No trace-only Error remains, and the user still
  gets `Failed`.
- **Hidden for a structural reason.** When every usable proposal is hidden *and* one of these holds:
  - a time limit is set and every usable proposal lacks a preparation time;
  - the AI returned product IDs that are not on the user's list (`stats.UnknownIds > 0`);

  `RecipeService` logs a **Warning** with `RecipeLogEvents.ProposalsHiddenStructurally` and returns
  `NoneWithinLimits` (unchanged for the user).
- **Hidden by the user's own limits.** When every usable proposal is hidden only by those limits, the log stays at
  **Information** with `RecipeLogEvents.ProposalsHiddenByLimits`.
- **Unknown product IDs.** The existing Warning gets `RecipeLogEvents.UnknownProductIds`.
- **Startup migration failure.** It logs `LogCritical(ex, …)`, then `await app.DisposeAsync()`, then rethrows the
  original exception.
- **Production log levels** (in `appsettings.Production.json`):
  - `Microsoft.EntityFrameworkCore.Database.Command` is at Warning.
  - `Microsoft.AspNetCore.Authentication`, `Microsoft.AspNetCore.Antiforgery` and
    `Microsoft.AspNetCore.Components.Server.Circuits` are at Information.
  - Everything else under `Microsoft.AspNetCore` (request hosting, routing) stays at Warning.
- `dotnet build` and `dotnet test` pass. Every new test was seen red before its fix.

### Key Discoveries:

- `RecipeService.cs:113-117`: the existing `RecipeGenerationException` catch is the exception-carrying log site, so
  throwing into it reuses it.
- `RecipeRanker.cs:17`: with a limit set, a null time is always hidden. So "all usable proposals lack a time and a
  limit is set" always means "all hidden".
- `Support/ProductionAppFactory.cs:65`: `AddProvider(instance)` is never disposed by DI. Registering the same instance
  through `Services.AddSingleton<ILoggerProvider>(_ => Logs)` makes the host's dispose observable.
- `appsettings.Development.json` repeats the base `Logging` section, so a Production-only override leaves the dev
  console exactly as it is today (SQL still visible locally).
- `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 is already referenced. `Microsoft.Extensions.Diagnostics.Testing`
  ships from the same dotnet/extensions release train and provides `FakeLogger<T>`, which records level, EventId and
  exception.

## What We're NOT Doing

- **R3-R12** (attempt-logging `DelegatingHandler`, structured `StatusCode`/`Kind`, refusal details, message IDs,
  redacted response excerpts). A redacted excerpt is skipped on purpose: product names are user data.
- **EventIds on the timeout and `AnthropicException` catches.** That is classification work (audit §6 item 5).
- **R8** (fail startup on a missing key). It is a product decision; login must keep working without AI.
- **X2/X6** (circuit log scope, `ConfigureResource`). These ship with Phase 8 (audit §6 item 4).
- **X5 noise** (duplicate EF Error exceptions). Only the Information-level SQL command spam is cut here, and EF errors
  and retry warnings stay.
- **`Microsoft.AspNetCore.Http.Connections`.** X4 lists it, but the parent brief scoped three categories. Add it with
  Phase 8 once real ingestion can be measured.
- **Changing user-facing messages or statuses.** `NoneWithinLimits` and `Failed` are unchanged.
- **Alert rules.** They are Phase 8. The EventIds here are what such a rule will key on.
- **Development logging levels.**

## Implementation Approach

Three independent phases, each test-first: write the regression test, run it red, apply the fix, run it green, then
run the full suite. The phases touch disjoint files except for the test support class: Phase 2 extends `CapturedLogs`
and Phase 3 only reads from it. The order follows the audit's fix order, critical first.

**Level for structural hiding: Warning, not Error.** The user's request did not fail; the AI's answer drifted from
the contract. Error is kept for paths that carry an exception, so "Error" keeps meaning "exception telemetry". The
stable EventId lets a Phase 8 log-based alert count the Warning's rate.

## Phase 1: Recipe generation silent failures become alertable (R1, R2)

### Overview

Give the recipe flow stable EventIds. Make "nothing usable" an exception that goes through the existing catch, and
split "all hidden" into structural (Warning) and user-limit (Information).

### Changes Required:

#### 1. Test logger package

**File**: `KitchenAssistant.Tests/KitchenAssistant.Tests.csproj`

**Intent**: Reference `Microsoft.Extensions.Diagnostics.Testing` so the tests can inspect level, EventId and exception
per log record.

**Contract**: `<PackageReference Include="Microsoft.Extensions.Diagnostics.Testing" Version="10.10.0" />`, matching
the TimeProvider.Testing version. `dotnet list package --vulnerable` stays clean.

#### 2. Regression tests (written first, run red)

**File**: `KitchenAssistant.Tests/Recipes/RecipeServiceTests.cs`

**Intent**: Let `Service(...)` take an optional `FakeLogger<RecipeService>`, then add tests that pin each outcome's
log level, EventId and exception presence.

**Contract**: new tests:

- **No usable recipes** (theory over `{"recipes": []}` and the title-less recipe JSON):
  - exactly one Error record has `Id == RecipeLogEvents.GenerationFailed` and an exception that is a
    `RecipeGenerationException` with `Data["RecipeCount"]`;
  - no Error-or-above record has a null exception;
  - the status is still `Failed`.
- **Structural, no times.** With `MaxPrepMinutes = 30` and every usable proposal without a time, there is one Warning
  with `ProposalsHiddenStructurally` and no Information record with `ProposalsHiddenByLimits`; the status is
  `NoneWithinLimits`.
- **Structural, unknown IDs.** All proposals are hidden over the missing limit because their product IDs are unknown.
  There is a Warning with `ProposalsHiddenStructurally` and a Warning with `UnknownProductIds`.
- **User limits only.** Proposals with valid IDs and stated times are hidden by the time or missing limit. There is one
  Information record with `ProposalsHiddenByLimits` and no record at Warning or above.

#### 3. Log event IDs

**File**: `Recipes/RecipeLogEvents.cs` (new)

**Intent**: One place for the recipe flow's stable EventIds, so alerts and tests key on numbers and names, not on
message text.

**Contract**: `public static class RecipeLogEvents` holding `EventId` fields with explicit ids and names:

| Field | Id | Name |
|---|---|---|
| `GenerationFailed` | 1100 | `RecipeGenerationFailed` |
| `ProposalsHiddenByLimits` | 1101 | `RecipeProposalsHiddenByLimits` |
| `ProposalsHiddenStructurally` | 1102 | `RecipeProposalsHiddenStructurally` |
| `UnknownProductIds` | 1103 | `RecipeUnknownProductIds` |

The ids are immutable once shipped.

#### 4. RecipeService outcomes

**File**: `Recipes/RecipeService.cs`

**Intent**: Route "none usable" to the exception path, and classify "all hidden" by cause.

**Contract**:

- `usable.Count == 0` throws `RecipeGenerationException` with a message stating the count. Its `Data` holds
  `RecipeCount` and `ResponseLength` (`json.Length`). The trace-only `LogError` is removed.
- The `RecipeGenerationException` catch logs with `RecipeLogEvents.GenerationFailed`.
- The all-hidden branch computes `allWithoutPrepTime = meal.MaxPrepMinutes is not null && usable.All(p => p.PrepTimeMinutes is null)`
  and `stats.UnknownIds > 0`.
  - If either is true, it logs a Warning with `ProposalsHiddenStructurally`. The fields carry both causes
    (`AllWithoutPrepTime`, `UnknownIdCount`) plus the existing counts.
  - Otherwise it logs at Information with `ProposalsHiddenByLimits`.
  - Exactly one of the two is logged.
- The unknown-ID Warning takes `UnknownProductIds`.
- Update the class comment, which lists what becomes `Failed`.

### Success Criteria:

#### Automated Verification:

- New RecipeServiceTests fail before the RecipeService change: `dotnet test --filter "FullyQualifiedName~RecipeServiceTests"`
- New RecipeServiceTests pass after the change: `dotnet test --filter "FullyQualifiedName~RecipeServiceTests"`
- Build passes with no new warnings: `dotnet build`
- Full suite passes: `dotnet test`
- No vulnerable packages added: `dotnet list package --vulnerable --include-transitive`

---

## Phase 2: Startup migration failure reaches telemetry (X1)

### Overview

Make a failing startup migration leave a Critical log with its exception, and flush the providers before the process
dies.

### Changes Required:

#### 1. Test log capture

**File**: `KitchenAssistant.Tests/Support/ProductionAppFactory.cs`

**Intent**: Let tests see each record's EventId and exception, and whether the host disposed its logger providers.

**Contract**:

- `CapturedLogs.Entries` becomes a collection of a record `(LogLevel Level, EventId EventId, string Message, Exception? Exception)`.
  `Level` and `Message` keep their names, so existing callers compile unchanged.
- `CapturedLogs.Disposed` is set in `Dispose()`.
- The provider is registered with `logging.Services.AddSingleton<ILoggerProvider>(_ => Logs)` instead of
  `AddProvider(Logs)`, so the host's container owns and disposes it.

#### 2. Regression test (written first, run red)

**File**: `KitchenAssistant.Tests/Http/ProductionCompositionTests.cs`

**Intent**: Prove that a failing startup migration is logged at Critical with the exception, that the host is
disposed, and that startup still fails.

**Contract**: `Failing_startup_migration_is_logged_critical_and_flushed`:

- `ProductionAppFactory.With(("Database:MigrateOnStartup", "true"))`; `CreateNoRedirectClient()` throws.
- A `Critical` entry exists whose `Exception` is the thrown exception.
- `factory.Logs.Disposed` is true right after the throw.

The migration fails because the factory already ran `EnsureCreated()` on SQLite.

#### 3. Program.cs

**File**: `Program.cs`

**Intent**: Wrap the startup migration so its failure goes through `ILogger` with the exception and the host is
disposed before the exception escapes.

**Contract**: `try { MigrateAsync } catch (Exception ex) { app.Logger.LogCritical(ex, …); await app.DisposeAsync(); throw; }`.
The scope is disposed inside the `try`. The rethrow keeps the original exception and stack (`throw;`). There are no
behaviour changes on success.

### Success Criteria:

#### Automated Verification:

- New migration test fails before the Program.cs change: `dotnet test --filter "FullyQualifiedName~Failing_startup_migration"`
- New migration test passes after the change: `dotnet test --filter "FullyQualifiedName~Failing_startup_migration"`
- Full suite passes (existing CapturedLogs callers unaffected): `dotnet test`

#### Manual Verification:

- With the compose stack's SQL container stopped (`./scripts/local-prod.ps1`, then `docker compose stop sql`, then
  restart the app container), the app container's stdout shows a `crit:` line carrying the exception before the
  process exits

---

## Phase 3: Production log levels tuned for the exporter (X3, X4)

### Overview

Cut the largest trace volume (EF SQL commands) and surface the auth, antiforgery and circuit events that are invisible
today. Production only.

### Changes Required:

#### 1. Regression test (written first, run red)

**File**: `KitchenAssistant.Tests/Http/ProductionCompositionTests.cs`

**Intent**: Pin the effective Production filter rules through the real `ILoggerFactory`, not by reading JSON keys.
This also covers category-prefix matching.

**Contract**: `Production_log_levels_are_tuned_for_the_exporter`. A theory or table over these categories:

| Category | Information enabled? | Other checks |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Database.Command` | no | Warning enabled |
| `Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler` | yes | Debug disabled |
| `Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery` | yes | Debug disabled |
| `Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost` | yes | Debug disabled |
| `Microsoft.AspNetCore.Hosting.Diagnostics` | no | volume guard: per-request logs stay off |

#### 2. Production logging config

**File**: `appsettings.Production.json`

**Intent**: Add a `Logging:LogLevel` section with the four overrides. It merges over the base file's `Default` and
`Microsoft.AspNetCore`.

**Contract**:

```json
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
    "Microsoft.AspNetCore.Authentication": "Information",
    "Microsoft.AspNetCore.Antiforgery": "Information",
    "Microsoft.AspNetCore.Components.Server.Circuits": "Information"
  }
}
```

### Success Criteria:

#### Automated Verification:

- New log-level test fails before the config change: `dotnet test --filter "FullyQualifiedName~Production_log_levels"`
- New log-level test passes after the change: `dotnet test --filter "FullyQualifiedName~Production_log_levels"`
- Full suite passes: `dotnet test`

#### Manual Verification:

- On the compose stack (`./scripts/local-prod.ps1`), browsing `/products` produces no `Executed DbCommand` lines,
  and an anonymous hit on `/products` shows an `info: Microsoft.AspNetCore.Authentication…` challenge line
- After Phase 8 is live, daily ingestion is checked against the 0.1 GB/day cap

---

## Testing Strategy

### Unit Tests:

- `RecipeServiceTests` with `FakeLogger<RecipeService>`. Each test asserts level, EventId and exception presence
  across four outcomes: none usable, structural (no times), structural (unknown IDs), and user limits only.
- The existing outcome tests keep `NullLogger` (via the optional parameter) and stay unchanged.

### Integration Tests:

- `ProductionCompositionTests`:
  - a failing migration gives a Critical log with the exception and disposes the providers;
  - the Production filter rules match for the four tuned categories plus the hosting guard.

### Manual Testing Steps:

1. Compose stack with SQL stopped: the app container's log has a `crit:` line with the exception.
2. Compose stack running: there is no SQL text in the logs, and the auth challenge is visible at Information.

## Performance Considerations

- **Ingestion cap (0.1 GB/day).** `Executed DbCommand` is the largest trace source: at least 2 per page view, and
  3-5 contexts per update, each with SQL text. Dropping it more than offsets the raised categories.
- **Volume of the raised categories.**
  - Authentication logs one challenge or forbid line per anonymous protected hit; bot traffic is the volume risk.
  - Antiforgery and circuits are rare.
  - Hosting/routing per-request logs stay at Warning; the test guards this.
- **Code cost.** The new code adds a `.All` scan over at most a handful of proposals, only on the all-hidden path.

## Migration Notes

None. No schema changes. Config is additive in `appsettings.Production.json`, and the App Service app settings do
not override `Logging` today.

## References

- Audit: `context/audits/observability/2026-10-15_recipe-generation-pantry-products-login-registration.md` §3, §4, §5 (R1, R2, X1, X3, X4), §6 items 1-3
- Deployment plan Phase 8 (distro and alert): `context/changes/deployment/deployment-plan.md:119,253,255`
- Existing exception path: `Recipes/RecipeService.cs:113-117`
- Ranker null-time rule: `Recipes/RecipeRanker.cs:17`
- Production test host: `KitchenAssistant.Tests/Support/ProductionAppFactory.cs`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Recipe generation silent failures become alertable (R1, R2)

#### Automated

- [x] 1.1 New RecipeServiceTests fail before the RecipeService change
- [x] 1.2 New RecipeServiceTests pass after the change
- [x] 1.3 Build passes with no new warnings
- [x] 1.4 Full suite passes
- [x] 1.5 No vulnerable packages added

### Phase 2: Startup migration failure reaches telemetry (X1)

#### Automated

- [x] 2.1 New migration test fails before the Program.cs change
- [x] 2.2 New migration test passes after the change
- [x] 2.3 Full suite passes (existing CapturedLogs callers unaffected)

#### Manual

- [ ] 2.4 Compose stack with SQL stopped logs a crit line with the exception before exit

### Phase 3: Production log levels tuned for the exporter (X3, X4)

#### Automated

- [x] 3.1 New log-level test fails before the config change
- [x] 3.2 New log-level test passes after the change
- [x] 3.3 Full suite passes

#### Manual

- [ ] 3.4 Compose stack shows no Executed DbCommand lines and an Information auth challenge line
- [ ] 3.5 Daily ingestion checked against the 0.1 GB/day cap after Phase 8
