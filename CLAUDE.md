# CLAUDE.md

## Project

**Kitchen Assistant**: a web app that takes the ingredients a user already has and has AI generate recipes from them. The product spec is in `context/foundation/prd.md` (written in Polish), and the stack rationale is in `context/foundation/tech-stack.md`. Read the PRD before building a feature. Its Functional Requirements (FR-001…FR-006), Business Logic and Open Questions define the scope. MVP deadline: 2026-10-22.

The code was generated from `dotnet new blazor --auth Individual`. The template's sample pages (`Counter`, `Weather`, `Auth`) have been removed.

## Hard rules

- **Render mode is per page.** `Components/App.razor` renders `<Routes />` with no global `@rendermode`, so pages default to static SSR. A page opts into interactivity with `@rendermode InteractiveServer` (`AddInteractiveServerComponents` is already wired in `Program.cs`). The pages under `Components/Account/` must stay static SSR, because Identity sign-in needs `HttpContext` and cookies.
- **Pages are private by default.** `Program.cs` sets a fallback authorization policy that requires a signed-in user. A public page needs `@attribute [AllowAnonymous]` in its own file, never in an `_Imports.razor` (`Account/Pages/_Imports.razor` is the parent of `Manage/`, so it would open Manage up). Add every new page to the public or protected list in `KitchenAssistant.Tests/Http/AuthorizationPolicyTests.cs`; the coverage guard fails until you do.
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

## 10xDevs AI Toolkit - Module 3, Lesson 4 (E2E Tests)

**For E2E tests, use the two M3L4 skills in this order:**

1. **`/10x-e2e-setup`** — one-time setup: Playwright config (`webServer`,
   auth `setup` project, `storageState`), a green seed test, and `context/foundation/test-stack.md`.
2. **`/10x-e2e`** — the per-risk loop: risk → explore the running app with
   `playwright-cli` → generate → review against the five anti-patterns →
   re-prompt by name → verify with a deliberate break.

The skills' `references/` carry the full rules, anti-patterns, seed pattern, and
prompt-template.

A few hard rules that hold even before you invoke the skill:

- **Locators:** `getByRole` / `getByLabel` / `getByText` first; `getByTestId`
  only when accessibility attributes are ambiguous. Never CSS selectors, XPath,
  or DOM structure.
- **Never `page.waitForTimeout()`.** Wait for state: `toBeVisible()`,
  `waitForURL()`, `waitForResponse()`.
- **Test independence + cleanup.** Each test runs standalone — its own setup,
  action, assertion, and cleanup; unique ids (timestamp suffix) so parallel runs
  and re-runs don't collide.

Two boundaries to keep straight:

- **DOM (snapshot) is the default.** Vision (`--caps=vision`) is a supplement for
  visual-only risks (layout, z-index, animation); for pixel regression prefer
  deterministic tools (`toHaveScreenshot`, Argos, Lost Pixel). VLM model
  selection/cost is a debugging topic (Lesson 5), not testing.
- **A red test is a signal, not a chore.** A changed selector → update the
  locator in a reviewed diff. A changed business behavior → the test caught a
  bug; never edit the assertion to match it. Fixing failing tests is Lesson 5.

<!-- END @przeprogramowani/10x-cli -->
