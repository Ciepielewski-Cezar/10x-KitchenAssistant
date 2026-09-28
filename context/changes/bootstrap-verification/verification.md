---
bootstrapped_at: 2026-09-25T20:53:45Z
starter_id: dotnet-blazor
starter_name: ".NET Blazor Web App (Identity + EF Core)"
project_name: kitchen-assistant
language_family: dotnet
package_manager: dotnet
cwd_strategy: subdir-then-move
bootstrapper_confidence: first-class
phase_3_status: ok
audit_command: "dotnet list package --vulnerable"
---

# Bootstrap verification — kitchen-assistant

## Hand-off

Source: `context/foundation/tech-stack.md`

```yaml
starter_id: dotnet-blazor
package_manager: dotnet
project_name: kitchen-assistant
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: first-class
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: true
  has_background_jobs: false
```

### Why this stack

A solo developer building Kitchen Assistant, a medium-scale web app with a 3-week after-hours MVP budget, chose the .NET language family and accepted the recommended ASP.NET Core default. They then swapped it for the Blazor Web App variant of the same stack, because the API-only webapi template lacks the UI and auth the PRD requires. The official `dotnet new blazor --auth Individual` template provides a C# UI with interactive server rendering, ASP.NET Core Identity for email/password accounts and private per-user spaces (FR-001), and EF Core with migrations on SQL Server, all in one deployable project. SQL Server was chosen over PostgreSQL because the template supports it natively: LocalDB for local development and Azure SQL Database in production need no post-scaffold provider swap or migration regeneration. It clears all four agent-friendly gates. Its scaffolding confidence is first-class rather than verified because this variant has not yet been run end-to-end by the bootstrapper. AI recipe generation (FR-005) still needs a manually added LLM SDK with structured output. Payments, realtime and background jobs are out of scope per the PRD. Deployment targets Azure App Service, with GitHub Actions auto-deploying on merge to main.

## Pre-scaffold verification

| Signal | Value | Severity | Notes |
| --- | --- | --- | --- |
| npm package | not run | — | non-JS starter (`dotnet new` template, no npm CLI) |
| GitHub repo (card `docs_url`) | not run | — | `docs_url` is `https://learn.microsoft.com/aspnet/core/blazor` (not GitHub); `gh` CLI not installed |
| GitHub repo (substitute) | `dotnet/aspnetcore` last pushed 2026-09-25T19:46:37Z | fresh | public GitHub API; upstream repo shipping the Blazor template |
| Local SDK | 10.0.401 (used); 8.0.425 also installed | — | template resolved from SDK 10.0.401 |
| Populated-cwd guard | no fingerprints (`*.csproj`, `package.json`, …) | — | guard did not fire |

## Scaffold log

**Resolved invocation**: `dotnet new blazor --auth Individual --interactivity Server --use-local-db -o .bootstrap-scaffold -n KitchenAssistant --no-restore`
**Strategy**: subdir-then-move
**Exit code**: 0
**Files moved**: 119
**Conflicts (.scaffold siblings)**: none
**.gitignore handling**: absent in scaffold (cwd `.gitignore` untouched)
**.bootstrap-scaffold cleanup**: deleted

CLI stdout:

```
The template "Blazor Web App" was created successfully.
This template contains technologies from parties other than Microsoft, see https://aka.ms/aspnetcore/10.0-third-party-notices for details.
```

CLI stderr: empty.

Move-up summary (all moved silently, no pre-existing paths):

| Entry | Files |
| --- | --- |
| `Components/` (Account/Identity pages, Layout, Pages, App, Routes) | 63 |
| `Data/` (`ApplicationDbContext`, `ApplicationUser`, `Migrations/CreateIdentitySchema`) | 5 |
| `wwwroot/` (`app.css`, `favicon.png`, `lib/bootstrap/**`) | 44 |
| `Properties/launchSettings.json` | 1 |
| `KitchenAssistant.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json` | 4 |
| `context/**` from scaffold | 0 (none shipped; cwd `context/` preserved) |

Notes:
- Target framework: `net10.0`. Root namespace: `KitchenAssistant` (fixed via `-n` in the card's `cmd_template`).
- `DefaultConnection` points at `(localdb)\mssqllocaldb` (`--use-local-db`); LocalDB is Windows-only.
- `--no-restore` in the template; a `dotnet restore` was run afterwards (exit 0, nuget.org only via the project-level `nuget.config`) so the audit could resolve the package graph.

## Post-scaffold audit

**Tool**: `dotnet list package --vulnerable --include-transitive` (exit 0)
**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
**Direct vs transitive**: 0/0/0/0 direct of total 0/0/0/0 (tool reports top-level and transitive separately)

Raw result:

```
The following sources were used:
   https://api.nuget.org/v3/index.json

The given project `KitchenAssistant` has no vulnerable packages given the current sources.
```

Top-level packages (all resolved 10.0.12): `Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.AspNetCore.App.Internal.Assets` (auto-referenced).

#### CRITICAL findings

None.

#### HIGH findings

None.

#### MODERATE findings

None.

#### LOW / INFO findings

None.

## Hints recorded but not acted on

| Hint | Value |
| --- | --- |
| bootstrapper_confidence | first-class |
| quality_override | false |
| path_taken | standard |
| self_check_answers | null |
| team_size | solo |
| deployment_target | azure-app-service |
| ci_provider | github-actions |
| ci_default_flow | auto-deploy-on-merge |
| has_auth | true (satisfied by the template's Individual accounts — Identity + EF Core) |
| has_payments | false |
| has_realtime | false |
| has_ai | true (no LLM SDK added — FR-005 needs one added manually) |
| has_background_jobs | false |

## Next steps

Next: a future skill will set up agent context (CLAUDE.md, AGENTS.md). For now, your project is scaffolded and verified — happy hacking.

Useful manual steps in the meantime:
- `git init` (if you have not already) to start your own repo history. The existing `.gitignore` is already the `dotnet new gitignore` set (`[Bb]in/`, `[Oo]bj/` covered).
- Apply the Identity migration to LocalDB (`dotnet ef database update`, or let the dev-time migrations page do it) and `dotnet run` to confirm register/login.
- Review any `.scaffold` siblings the conflict policy created — none this run.
- Address audit findings per your project's risk tolerance — none this run; the full breakdown is in this log.
