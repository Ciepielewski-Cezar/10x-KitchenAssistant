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

## 10xDevs AI Toolkit - Module 2, Lesson 1

Move from sprint-zero setup to project orchestration with the **roadmap chain**:

```
(Module 1 foundation docs) -> /10x-roadmap -> backlog-ready roadmap items
```

`/10x-roadmap` is the lesson focus. `/10x-new` is intentionally introduced in Module 2, Lesson 2, when a selected roadmap item becomes an implementation change folder.

### Task Router - Where to start

| Skill | Use it when |
| --- | --- |
| **Roadmap (lesson focus)** | |
| `/10x-roadmap` | You have `context/foundation/prd.md` and a scaffolded project baseline, and you need a vertical-first MVP roadmap. The skill reads the PRD, inspects the code baseline, uses available foundation docs such as `tech-stack.md`, `infrastructure.md`, and `deploy-plan.md`, then writes `context/foundation/roadmap.md`. Use it BEFORE creating per-change folders or implementation plans. |
| **Re-run upstream if needed** | |
| `/10x-shape` / `/10x-prd` / `/10x-tech-stack-selector` / `/10x-bootstrapper` / `/10x-agents-md` / `/10x-infra-research` | Bundled from Module 1 so foundation contracts can be fixed before roadmap sequencing. If roadmap generation exposes a PRD gap, repair the PRD before pretending the backlog is ready. |

### How the chain hands off

- `/10x-roadmap` bridges product and implementation. It does not choose frameworks, design schemas, or write a per-change implementation plan.
- The output is `context/foundation/roadmap.md`: ordered milestones, vertical slices, bounded foundations, dependencies, unknowns, risk, and backlog handoff fields.
- Roadmap items should receive stable human-readable identifiers in backlog tools. The actual `context/changes/<change-id>/` folder is created in Lesson 2 with `/10x-new`.

### Roadmap boundaries

- Default to vertical slices: user-visible outcomes that cross UI, data, business logic, and integrations.
- Horizontal work is allowed only as a bounded enabler that names the downstream vertical milestone it unlocks.
- Avoid orphan horizontal work such as "build the whole database", "build all API endpoints", or "design the whole UI" before the first user-visible flow.
- Roadmap is not a calendar estimate. Do not invent dates, story points, or sprint velocity unless the user explicitly asks for a separate planning artifact.

### Foundation paths used by this lesson

- `context/foundation/prd.md` - input
- `context/foundation/tech-stack.md` - optional input
- `context/foundation/infrastructure.md` - optional input
- `context/deployment/deploy-plan.md` - optional input
- `context/foundation/roadmap.md` - output
- `context/foundation/lessons.md` - recurring rules and pitfalls
- `docs/reference/contract-surfaces.md` - load-bearing names registry

Skills must not write to `context/archive/`. Archived changes are immutable; if a resolved target path starts with `context/archive/`, abort with: "This change is archived. Open a new change with `/10x-new` instead."

<!-- END @przeprogramowani/10x-cli -->
