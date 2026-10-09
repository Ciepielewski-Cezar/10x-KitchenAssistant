# CLAUDE.md

## Project

**Kitchen Assistant**: a web app that takes the ingredients a user already has and has AI generate recipes from them. The product spec is in `context/foundation/prd.md` (written in Polish), and the stack rationale is in `context/foundation/tech-stack.md`. Read the PRD before building a feature. Its Functional Requirements (FR-001…FR-006), Business Logic and Open Questions define the scope. MVP deadline: 2026-10-22.

The code was generated from `dotnet new blazor --auth Individual`. The template's sample pages (`Counter`, `Weather`, `Auth`) have been removed.

## Hard rules

- **Render mode is per page.** `Components/App.razor` renders `<Routes />` with no global `@rendermode`, so pages default to static SSR. A page opts into interactivity with `@rendermode InteractiveServer` (`AddInteractiveServerComponents` is already wired in `Program.cs`). The pages under `Components/Account/` must stay static SSR, because Identity sign-in needs `HttpContext` and cookies.
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

## UI

- **Bootstrap 5.3, not Tailwind.** Bootstrap is vendored as compiled CSS in `wwwroot/lib/bootstrap/` (no Sass, no Node). Don't add Tailwind, shadcn or a second CSS framework.
- **Tokens live in `wwwroot/app.css`.** Its first block holds the theme under shadcn names (`--primary`, `--background`, `--muted-foreground`…), copied from `context/changes/recipes-ui/theme-source.css`. The blocks below map them onto Bootstrap's `--bs-*` variables and re-point components that Bootstrap compiles with literal colours (`.btn-primary`, `.btn-danger`, `.form-check-input:checked`…).
  - To restyle, change a token value, not a component class. The `-rgb`, `-text-emphasis`, `-bg-subtle`, `-border-subtle`, link and button hover/active values are derived by hand (formulas in the comment above them), so recompute them whenever their source colour changes.
  - Dark values are under `[data-bs-theme=dark]`, but nothing sets that attribute yet, so the app is light-only.
  - Fonts (Space Grotesk, Space Mono) are self-hosted woff2 files in `wwwroot/fonts/`, declared with `@font-face` at the top of `app.css`. Don't load fonts from a CDN. A new file in `wwwroot/` needs a rebuild before `MapStaticAssets` serves it.
- **No literal colours in views or `*.razor.css`.** Use Bootstrap's role classes (`btn-primary`, `text-bg-success`, `alert-danger`, `text-body-secondary`, `card`, `list-group`) or `var(--token)`. No hex, `rgb()`, named colours, `style="color:…"` or fixed-theme classes such as `navbar-dark`. A data-URI SVG, which can't read `var()`, is the only exception: put it in a per-theme variable in `app.css` (see `--sidebar-toggler-icon`), or use it as a `mask` with `background-color: currentColor` (see `.bi` in `NavMenu.razor.css`).
- Before finishing a UI change, scan the touched files for literals (anything printed is a candidate for a token):
  ```bash
  grep -nE '#[0-9a-fA-F]{3,8}\b|rgba?\(|hsla?\(|oklch\(|:\s*(white|black)\b|navbar-(dark|light)' <changed .razor/.razor.css files>
  ```
- **Shared components.** Shared UI components live in `Components/Ui/` (namespace `KitchenAssistant.Components.Ui`, imported globally), so check there before creating one. When the same markup appears in a second page (badge, card, alert with action, empty state), extract a Razor component rather than copying it. The remaining candidates are listed in `context/changes/recipes-ui/research.md` §5.
  - **Form fields use `FormField`** (label above the input), not `form-floating`. The Account pages still on floating labels are legacy.
  - **Status messages carry an explicit kind.** New `StatusMessage` and `RedirectTo…WithStatus` callers pass a `StatusKind` (`Components/Account/StatusKind.cs`), never rely on an "Error" text prefix.
  - **States are proved on `/dev/ui`**, a Development-only kitchen sink (`Components/Pages/DevUi.razor`). Add every new shared component to it.
- Bootstrap's JS bundle is not loaded (`Components/App.razor`). Build interactive pieces such as collapse, modal or dropdown with Blazor state or native `<details>`/`<dialog>`, not with `data-bs-*` attributes.

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

## 10xDevs AI Toolkit - Module 3, Lesson 1

Open Module 3 by producing a **durable, risk-first quality contract** before any test is written — then drive each rollout phase through the standard change chain.

```
PRD + roadmap + archive
        │
        ▼
   /10x-test-plan  ──►  context/foundation/test-plan.md  (strategy §1–§5 frozen + cookbook §6 grows)
        │
        ▼  (one rollout phase at a time, /clear between handoffs)
   /10x-new ──► /10x-research ──► /10x-plan ──► /10x-implement
```

`/10x-test-plan` is a **stateful orchestrator**, not a one-shot generator. On first run it writes the phased rollout to `context/foundation/test-plan.md`. On every subsequent run it re-derives state from on-disk artifacts and presents the next handoff. The lesson focus is **strategy and rollout sequencing, not configuration**. Hooks, MCP servers, and CI YAML are configured in later lessons of this module.

### Task Router - Where to start

| Skill | Use it when |
| --- | --- |
| **Quality strategy as a rules-file (lesson focus)** | |
| `/10x-test-plan` | You have a PRD (and ideally a roadmap and a few archived slices) and you are about to write the project's first tests, or you noticed that AI-generated tests are landing on helpers while critical flows go uncovered. First invocation runs discovery (PRD + roadmap + archive + hot-spot scan), a 5-question user interview, and a synthesis pass with a mandatory challenger check, then writes `test-plan.md` in `context/foundation/` with a risk map (5–7 failure scenarios), a phased rollout table, a stack table, a quality-gates table, a cookbook section (`§6`, fills in as phases ship), and a negative-space section (what we deliberately don't test). Subsequent invocations advance the rollout one handoff at a time. |
| `/10x-test-plan --status` | A `test-plan.md` already exists and you want a compact snapshot of where the rollout stands — which phases are `not started`, `change opened`, `researched`, `planned`, `implementing`, or `complete`, and what the next action is. Does no work; safe to run any time. |
| `/10x-test-plan --refresh` | A `test-plan.md` already exists and one of: a new top-3 risk surfaced from the roadmap or archive, a tool's `checked:` date is older than three months, the project's tech stack changed, or §7 negative-space no longer matches what the team believes. Opens a new `test-plan-refresh-<YYYY-MM-DD>` change folder rather than editing the guide in place. |

### Rollout chain — what happens after the guide is written

The guide's §3 *Phased Rollout* table is the orchestrator's state. For each non-`complete` row the orchestrator selects the next handoff based on which artifacts exist in `context/changes/<change-id>/`:

| State on disk | Next handoff | Status transitions to |
| --- | --- | --- |
| change folder missing | `/10x-new <change-id>` | `change opened` |
| `change.md` only | `/10x-research` (with a risks-to-verify brief) | `researched` |
| `+ research.md` | `/10x-plan` (with cost × signal + cookbook-update constraints) | `planned` |
| `+ plan.md` with pending `## Progress` items | `/10x-implement <change-id> phase <N>` | `implementing` / `complete` |
| `+ plan.md` fully `[x]` | Mark §3 row `complete`; loop to next pending row | — |

Each handoff is a **STOP point**. The orchestrator copies the next command to the clipboard, asks the user to `/clear` and run it, then exits. Re-invoke `/10x-test-plan` (no arguments) to advance.

### Risk-first prioritization rules

- Risks are **failure scenarios in user / business terms**, not test names. "Logged-out user reaches paid content via stale token" is a risk; "test the login form" is not.
- 5 to 7 risks. Fewer is too coarse; more makes prioritization useless.
- Impact and likelihood are user/business ratings, not technical complexity.
- Every risk traces to a source: PRD section, archived slice, roadmap entry, Phase 2 interview question, hot-spot **directory** with churn count, or a tech-stack constraint. No invented risks.
- **Signal, not knowledge.** §2 cites *evidence that raised the risk*, never a file as "where the failure lives." File:line anchors, function names, schema names, and module names are forbidden in §2 — they belong in `/10x-research`'s output, produced per rollout phase against current code. The plan is a QA spec; it is not a code audit.
- Coverage is not the metric. **Risk coverage** is the metric.

### Dual-layer mapping rules

- Classic layer first: the cheapest test that gives a real signal wins. Promote to e2e only when no cheaper layer covers the risk.
- AI-native layer second, and only where it adds signal classic tests do not give cheaply.
- Every AI-native row has a **"When NOT to use"** line. If you cannot write one, drop the row.
- Every tool name carries a `checked: <YYYY-MM-DD>` date. Tool names are examples of the category, not endorsements.
- Both layers must be non-empty in the final guide if the project warrants them. Classic-only is a 2020 plan; AI-native-only is hype. AI-native phases are not mandatory — include them only when the brief justified them under cost × signal.

### Quality gates rules

- Required gates (lint, typecheck, unit+integration, e2e on critical flows) must map to actual CI steps. If a required gate is not yet wired, mark it as `required after §3 Phase <N>` and let the named rollout phase wire it.
- Post-edit hook is **recommended local**, not a CI substitute.
- Multimodal visual review is **selective**, applied to 1–3 critical screens, not to every page.
- Vision-driven fallback (Anthropic Computer Use or OpenAI CUA) is reserved for DOM-unreachable surfaces; expensive per action.

### Cookbook patterns (§6) — fills in over time

`test-plan.md` is both a phased strategy and a **growing cookbook**. §6 starts as placeholders (`TBD — see §3 Phase <N>`) and fills in incrementally — each rollout phase's plan ends with a sub-phase that updates the relevant §6 entry (location, naming, reference test, run command). After Module 3 completes, §6 becomes the canonical answer to "how do I add a test for X in this project?" — and is what `/10x-tdd` reads in Lesson 2.

### Lesson boundaries

- Do not write test code. That is Lesson 2 (`/10x-tdd` and unit-test authoring).
- Do not configure hooks, hook lifecycle, or debugging hooks. That is Lesson 3.
- Do not configure MCP servers, Playwright API, e2e code, or multimodal scenario code. That is Lesson 4.
- Do not run the bug-to-fix-to-regression-test workflow. That is Lesson 5.
- Do not author CI/CD pipelines from scratch or write GitHub Actions YAML. The guide names gates; configuration is owned by Module 1 Lesson 5 and Module 2 Lesson 5.
- Do not benchmark multimodal models. Cite criteria (cost, latency, agent-friendliness), never a ranking.
- Do not read the codebase for knowledge (call graphs, schemas, "which file owns this failure"). That is `/10x-research`'s job, per rollout phase.

### Paths used by this lesson

- `context/foundation/test-plan.md` — the quality contract produced and maintained by `/10x-test-plan`
- `context/foundation/prd.md` — primary risk source
- `context/foundation/roadmap.md` — likelihood weighting
- `context/foundation/tech-stack.md` — stack input (when present)
- `context/archive/<change-id>/plan.md` — implemented risk surface
- `context/changes/<change-id>/` — per-rollout-phase change folder (one per row in §3)

<!-- END @przeprogramowani/10x-cli -->
