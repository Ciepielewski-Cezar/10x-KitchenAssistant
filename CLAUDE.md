# CLAUDE.md

## Project

**Kitchen Assistant**: a web app that takes the ingredients a user already has and has AI generate recipes from them. The product spec is in `context/foundation/prd.md` (written in Polish), and the stack rationale is in `context/foundation/tech-stack.md`. Read the PRD before building a feature. Its Functional Requirements (FR-001…FR-006), Business Logic and Open Questions define the scope. MVP deadline: 2026-10-22.

The code was generated from `dotnet new blazor --auth Individual`. The pages `Counter`/`Weather`/`Auth` are template samples, not product features.

## Hard rules

- **Render mode is per page.** `Components/App.razor` renders `<Routes />` with no global `@rendermode`, so pages default to static SSR. A page opts into interactivity with `@rendermode InteractiveServer`, as `Components/Pages/Counter.razor` does. The pages under `Components/Account/` must stay static SSR, because Identity sign-in needs `HttpContext` and cookies.
- Domain entities go in `Data/`, with a `DbSet` on `ApplicationDbContext`. Each per-user entity is keyed to `ApplicationUser`, because the PRD requires every user's data to be private.
- **Scores come from app code, never from the AI.** Recipe score, missing-ingredient count and sort order are computed in C# by comparing each generated recipe's ingredients with the user's products. Never read those values from LLM output (see the PRD's *Business Logic*).
- The AI recipe generator (FR-005) must use the LLM's structured output.
- `context/` holds the 10xDevs bootstrap chain's artifacts (PRD, shape notes, tech-stack hand-off, bootstrap verification log). Never write to `context/archive/`. The block below is managed by `@przeprogramowani/10x-cli`, which may regenerate it, so edit only outside the markers.

## Architecture

- **One project, no layers.** `KitchenAssistant.csproj` (namespace `KitchenAssistant`) holds the UI, the auth code and the data access.
- **Auth.** ASP.NET Core Identity with cookie auth is configured in `Program.cs`:
  - `RequireConfirmedAccount = true` is set, but `IdentityNoOpEmailSender` sends no email. The register flow shows a confirmation link on screen instead.
  - Identity endpoints the Razor components need (logout, passkeys, external login) are mapped in `Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs`.
- **Data.**
  - `Data/ApplicationDbContext` extends `IdentityDbContext<ApplicationUser>` and uses SQL Server. Development runs on LocalDB (connection string in `appsettings.json`); production runs on Azure SQL.
- **Feature code.** Each feature's services, form models and labels live in a top-level feature folder with a matching namespace, e.g. `Pantry/` (`KitchenAssistant.Pantry`) holds `ProductService`. Pages call these services and never touch `ApplicationDbContext` directly. Don't give a feature folder the same name as a page class: a `Products` namespace would clash with `Components/Pages/Products.razor`.
- **Deployment.** The target is Azure App Service, with GitHub Actions deploying on merge to `main`.

## Commands

The target is .NET 10 (`net10.0`); `global.json` pins SDK 10.0.x. `dotnet-ef` 10.0.12 is a local tool in `dotnet-tools.json`, so run `dotnet tool restore` after cloning.

```bash
dotnet build
dotnet run --launch-profile https      # https://localhost:7020, http://localhost:5180
dotnet watch                           # hot reload; Ctrl+R restarts after a rude edit
dotnet ef migrations add <Name> --output-dir Data/Migrations
dotnet ef migrations has-pending-model-changes   # must exit 0 before committing
dotnet ef database update              # applies migrations to LocalDB
dotnet list package --vulnerable --include-transitive
```

Register every test project (e.g. `KitchenAssistant.Tests`) in `KitchenAssistant.slnx`. Run a single test with `dotnet test --filter "FullyQualifiedName~<Name>"`.

### Local run

- **Dev loop:** LocalDB, `Development` environment, account confirmation via the on-screen link. Secrets come from user-secrets: `dotnet user-secrets set ANTHROPIC_API_KEY <dev-key>`. If the key is missing, the app logs a warning and still starts.
- **Production-like stack** (`compose.yaml`, http://localhost:8090):
  - The app runs as a Linux container in `Production`, against a SQL Server 2022 container (host port 14330) with SQL auth.
  - The image is built by the SDK (`/t:PublishContainer`), with no Dockerfile.
  - Migrations run at startup (`Database:MigrateOnStartup`), and confirmation is off (`appsettings.Production.json`).
  - Secrets are in the git-ignored `.env` (template: `.env.example`).
  ```bash
  ./scripts/local-prod.ps1            # build image, start stack, wait for /healthz
  ./scripts/local-prod.ps1 -NoBuild   # restart without rebuilding
  ./scripts/local-prod.ps1 -Reset     # wipe the SQL volume; schema rebuilds from migrations
  ./scripts/local-prod.ps1 -Down      # stop, keep data
  ```
- `/healthz` is liveness only and must never query the database.
- Data Protection keys live in the `DataProtectionKeys` table, so logins survive restarts.
- Plans: `context/changes/local-dev/local-dev-plan.md` (local) and `context/changes/deployment/deployment-plan.md` (Azure).
<!-- BEGIN @przeprogramowani/10x-cli -->

## 10xDevs AI Toolkit - Module 2, Lesson 2

Turn one roadmap item into the first implementation cycle with the **change planning chain**:

```
/10x-roadmap -> /10x-new -> /10x-plan -> /10x-plan-review -> /10x-implement
```

`/10x-new`, `/10x-plan`, `/10x-plan-review`, and `/10x-implement` are the lesson focus. `/10x-frame` and `/10x-research` are not required rituals here; they are escalation paths introduced in the next lesson.

### Task Router - Where to start

| Skill | Use it when |
| --- | --- |
| **Change setup (lesson focus)** | |
| `/10x-new <change-id>` | You selected a roadmap item and need a stable change folder. Creates `context/changes/<change-id>/change.md` so planning, implementation, progress, commits, and later review all share one identity. Use AFTER roadmap selection, BEFORE `/10x-plan`. |
| **Planning (lesson focus)** | |
| `/10x-plan <change-id>` | You have a change folder and need a reviewable implementation plan. Reads roadmap context, foundation docs, codebase evidence, and any existing change notes; writes `plan.md` and `plan-brief.md` with phases, file contracts, success criteria, and `## Progress`. |
| **Plan readiness (lesson focus)** | |
| `/10x-plan-review <change-id>` | You have `plan.md` and need a light pre-code readiness check. Use it to catch missing end state, weak contracts, malformed progress, scope drift, or blind spots before code changes begin. |
| **Implementation (lesson focus)** | |
| `/10x-implement <change-id> phase <n>` | You have an approved plan and want to execute one phase with verification, manual gate, commit ritual, and SHA write-back to `## Progress`. |
| **Lifecycle closure** | |
| `/10x-archive <change-id>` | A change is merged or intentionally closed. Move it out of active `context/changes/` into archive state. |

### How the chain hands off

- `/10x-new` creates the durable change identity.
- `/10x-plan` turns that identity into an implementation contract.
- `/10x-plan-review` checks the plan before the agent mutates code.
- `/10x-implement` executes one planned phase, verifies, asks for manual confirmation when needed, commits, and records progress.

### Lesson boundaries

- Plan is the default router after roadmap selection. Start with `/10x-plan` unless the problem is unclear or external evidence is blocking.
- Do not run `/10x-frame + /10x-research` as ceremony for every change.
- Do not turn this lesson into a full end-to-end product build. A checkpoint with a planned and partially or fully implemented stream is valid.
- Code review of the implemented diff belongs to Lesson 3 via `/10x-impl-review`.
- Lifecycle closure via `/10x-archive` after a change is merged or intentionally closed.

### Paths used by this lesson

- `context/foundation/roadmap.md` - upstream roadmap
- `context/changes/<change-id>/change.md` - change identity
- `context/changes/<change-id>/plan.md` - implementation contract
- `context/changes/<change-id>/plan-brief.md` - compressed handoff
- `context/foundation/lessons.md` - recurring rules and pitfalls
- `docs/reference/contract-surfaces.md` - load-bearing names registry

Skills must not write to `context/archive/`. Archived changes are immutable; if a resolved target path starts with `context/archive/`, abort with: "This change is archived. Open a new change with `/10x-new` instead."

<!-- END @przeprogramowani/10x-cli -->
