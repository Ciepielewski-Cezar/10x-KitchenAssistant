# Observability Quick Wins — Plan Brief

> Full plan: `context/changes/observability-quick-wins/plan.md`
> Audit (upstream evidence): `context/audits/observability/2026-10-15_recipe-generation-pantry-products-login-registration.md`

## What & Why

This applies the observability audit's first three fixes (§6 items 1-3) before deployment-plan Phase 8 adds Azure
Monitor and its exception-based alert.

- **The core flow's most likely production regression is invisible.** AI output drift that hides every recipe looks
  like user error and is logged at Information. "Nothing usable" is a trace-only `LogError`.
- **A bad startup migration leaves App Insights empty.**
- **Log levels are tuned for the console.** EF SQL spam can hit the 0.1 GB/day cap, while auth and circuit failures
  are filtered out.

## Starting Point

- `RecipeService` returns `Failed` or `NoneWithinLimits` with no exception-carrying log on two paths, and no app code
  uses EventIds.
- `Program.cs` runs `MigrateAsync` between `Build()` and `Run()` with no catch.
- `appsettings.Production.json` has no `Logging` section.

## Desired End State

- **Nothing usable** throws a `RecipeGenerationException` into the existing catch. That produces an Error with the
  exception and EventId 1100.
- **Everything hidden** is classified by cause:
  - structural (all usable proposals lack a time while a limit is set, or there are unknown product IDs) logs a
    Warning with EventId 1102;
  - the user's own limits log Information with EventId 1101.
- **A failing startup migration** logs Critical with the exception, disposes the host so the exporters flush, and
  rethrows.
- **Production levels:**
  - EF `Database.Command` is at Warning.
  - `Authentication`, `Antiforgery` and `Components.Server.Circuits` are at Information.
  - The rest of `Microsoft.AspNetCore` stays at Warning.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| R2 mechanism | Throw `RecipeGenerationException` into the existing catch, with counts and response length in `Data` | Reuses the one exception-carrying log site, so no second Error path. | Parent brief / Audit R2 |
| Structural hiding: definition | All usable proposals have no time while a limit is set, **or** `UnknownIds > 0` | The brief's definition; anything else stays a user-limit case. | Parent brief |
| Structural hiding: level | Warning, not Error | The request succeeded and the AI's answer drifted; Error stays reserved for exception telemetry. | Plan |
| EventIds | `RecipeLogEvents` 1100-1103 with names | Alerts and tests key on stable ids, not message text. | Plan |
| Redacted response excerpt | Not added | Product names are user data; length plus counts is enough for now. | Plan |
| Log-level location | `appsettings.Production.json` only | The exporter runs only in Production; the dev console keeps SQL visible. | Plan |
| `Http.Connections` category | Not raised | Not in the brief's scope; measure ingestion with Phase 8 first. | Parent brief |
| Test logger | `Microsoft.Extensions.Diagnostics.Testing` `FakeLogger` | Records level, EventId and exception; same release train as TimeProvider.Testing. | Plan |
| X1 proof | Production test host; migration fails on the pre-created SQLite database; assert a Critical entry with the exception and provider disposal | Exercises the real `Program.cs`; disposal is the observable proxy for "exporters flushed". | Plan |

## Scope

**In scope:**

- R1 and R2 in `RecipeService.cs`, with a new `RecipeLogEvents.cs`.
- X1 in `Program.cs`.
- X3/X4 in `appsettings.Production.json`.
- Regression tests for each, plus small `CapturedLogs` extensions.

**Out of scope:**

- R3-R12 (provider classification, attempt logging).
- R8 (missing-key startup policy).
- X2/X6 (circuit scopes, resource attributes; these ship with Phase 8).
- X5 duplicate EF errors.
- Alert rules.
- Development log levels.
- Any user-facing message change.

## Architecture / Approach

- **Recipe outcomes.** No new types beyond a static EventId holder. "None usable" becomes an exception on the
  existing `catch (RecipeGenerationException)` path. The all-hidden branch picks one of two EventIds by cause.
- **Startup migration.** `Program.cs` gets a try/catch around the migration only.
- **Log levels.** Config is additive in the Production file.
- **Tests.** They use `FakeLogger<RecipeService>` for the service and the existing `ProductionAppFactory` (real
  `Program.cs`, Production environment) for startup and filter rules.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Recipe silent failures (R1, R2) | Exception telemetry for "none usable"; a Warning with an EventId for structural hiding | Misclassifying a user-limit case as structural adds Warning noise; tests pin both sides |
| 2. Startup migration (X1) | Critical log with the exception, host disposed, rethrow | The test relies on SQLite `MigrateAsync` failing after `EnsureCreated`; it asserts the throw, so a silent pass is impossible |
| 3. Exporter log levels (X3, X4) | Less SQL trace volume, plus visible auth, antiforgery and circuit events | Authentication challenges under bot traffic add volume; watch ingestion once Phase 8 is live |

**Prerequisites:** none. SDK 10.0.x, and NuGet access for the one test-only package.
**Estimated effort:** about 1 session across 3 small phases.

## Open Risks & Assumptions

- The audit's distro behaviour (only `ILogger` calls carrying an exception become exception telemetry, and EventId
  goes into custom dimensions) is assumed and unverified until Phase 8.
- The structural rule counts *any* unknown ID when everything is hidden, even if the unknown IDs were not what hid
  them. This is accepted: unknown IDs are themselves an AI-contract anomaly.

## Success Criteria (Summary)

- Each of the three failure shapes the audit marked **missed** (R1, R2, X1) now leaves an alertable record: an
  exception, or a Warning with a stable EventId.
- `dotnet build` and `dotnet test` pass, and every new test was seen red before its fix.
