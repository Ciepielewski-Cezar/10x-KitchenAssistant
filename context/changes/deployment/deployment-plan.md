# First production deploy: Kitchen Assistant → Azure App Service + Azure SQL

## Context

`context/foundation/infrastructure.md` (2026-09-28) picked **Azure App Service (Linux, B1, Poland Central) + Azure SQL Basic DTU**, deployed by **GitHub Actions with OIDC** on merge to `main`. `tech-stack.md` fixes the stack: .NET 10 Blazor Web App (per-page InteractiveServer), Identity, EF Core 10 on SQL Server, plus the Anthropic SDK for FR-005.

Current state (checked 2026-09-29):
- The repo is still the unmodified scaffold (`b7d9e0a`). There is no `.github/`, no `global.json`, no retry policy, no Data Protection persistence, no health endpoint, and no startup migration. The `Anthropic` 12.50.0 package is referenced but not used.
- The remote `Ciepielewski-Cezar/10x-KitchenAssistant` does **not exist** on GitHub yet. `gh` is authenticated with `repo` and `workflow` scopes.
- `az` is installed but **not logged in**. The Azure account is a **free trial**.

Decisions made with you:
- **Public** repo, so a `production` environment with a reviewer gate is free.
- **SQL auth** connection string first. Managed identity is deferred.
- **Email confirmation off in Production.**
- The free trial gets upgraded to pay-as-you-go. It's required, see Phase 0.
- **Paid tiers B1 + Basic SQL (~$18/mo)** were chosen over free ones:
  - F1 allows only 5 concurrent WebSockets and 60 CPU min/day, and has no Always On.
  - The SQL free offer auto-pauses (about 1 min resume, error 40613) and can pause until next month once its 100k vCore-seconds are used.
  - The remaining trial credit pays for the first weeks after the PAYG upgrade.
  - To scale down later: `az appservice plan update --sku F1`, though F1 isn't suitable for Blazor Server under real use.

Outcome: `https://<app>.azurewebsites.net` is live. Register and log in work. Blazor circuits run over WebSockets and auth survives restarts. Every merge to `main` deploys after your approval. Rollback, logs, budget and alerts are wired. The approved plan is stored at `context/changes/deployment/deployment-plan.md`.

**Owner legend:** 🤖 = agent does it · 👤 = you do it. These are human-only per infrastructure.md: creating Azure resources, secrets, Entra/OIDC, firewall, merges to `main`. **Status legend:** ⬜ todo · 🟡 in progress · ✅ done · ⛔ blocked.

## Phase tracker

| # | Phase | Owner | Status |
|---|---|---|---|
| 0 | Accounts & local prerequisites | 👤 (🤖 verifies) | ⬜ |
| 1 | GitHub repo bootstrap | 🤖 + 👤 approve | ⬜ |
| 2 | App production-readiness (code) | 🤖 | 🟡 mostly done via local plan L1 (2026-09-29); Azure Monitor package still to add |
| 3 | Azure provisioning | 👤 runs script, 🤖 verifies | ⬜ |
| 4 | App configuration & secrets | 👤 secrets, 🤖 non-secret config | ⬜ |
| 5 | GitHub OIDC + CI/CD pipeline | 👤 Entra, 🤖 workflow | ⬜ |
| 6 | First production deploy | 👤 merge + approve | ⬜ |
| 7 | Production verification | 🤖 + 👤 browser checks | ⬜ |
| 8 | Observability & cost guardrails | 👤 portal, 🤖 CLI | ⬜ |
| 9 | Rollback drill & hand-off docs | 🤖 | ⬜ |
| 10 | Post-MVP hardening (not executed now) | — | ⬜ backlog |

---

## Phase 0: Accounts & local prerequisites

- [ ] 👤 **Upgrade the free trial to pay-as-you-go** (Portal → Subscriptions → Upgrade). Remaining credit still applies. Why this is needed:
  - Free trials usually have **Basic VMs quota = 0** for App Service, and they *cannot* request a quota increase.
  - Trials are disabled after 30 days or once the credit is used up, which would take production down before the 2026-10-22 deadline.
- [ ] 👤 `az login` with the **personal** account. MFA is mandatory for Azure CLI sign-in, so have an authenticator ready.
- [ ] 🤖 Verify the tenant and subscription: `az account show --query "{sub:name, tenant:tenantId, user:user.name}"`. It must not be the Euvic tenant. If it is, stop and run `az login --tenant <personal-tenant-id>`.
- [ ] 🤖 Register resource providers, since new subscriptions often lack them: `az provider register --namespace Microsoft.Web --wait`. Repeat for `Microsoft.Sql`, `Microsoft.Insights`, `Microsoft.OperationalInsights` and `Microsoft.AlertsManagement`.
- [ ] 🤖 Check the runtime: `az webapp list-runtimes --os linux --query "[?contains(@,'DOTNETCORE:10')]"` should list `DOTNETCORE:10.0`.
- [ ] 🤖 Check quota: Portal → Quotas → App Service → Poland Central → Basic ≥ 1. If it's 0 after the upgrade, 👤 request 1–2. Small requests are usually auto-approved, but can take up to about an hour after an upgrade.
- [ ] 👤 **Anthropic console:**
  - Create a dedicated workspace `kitchen-assistant-prod` with its own API key and a **monthly spend limit**, e.g. $10.
  - Keep a separate dev key in user-secrets: `dotnet user-secrets set ANTHROPIC_API_KEY <dev-key>`.
  - Make sure the org has purchased credits, otherwise calls fail with 400/403 "credit balance too low".
- [ ] 👤 Smoke-test the prod key locally (never paste it into chat): `curl https://api.anthropic.com/v1/models -H "x-api-key: $KEY" -H "anthropic-version: 2023-06-01"` should return 200.

**Exit:** `az account show` shows the personal PAYG subscription, the providers are Registered, B1 quota ≥ 1, and the Anthropic key returns 200.

| Edge case | Symptom | Fix |
|---|---|---|
| Quota 0 on trial | `Operation cannot be completed without additional quota… Basic VMs: 0` | Upgrade to PAYG, then request quota. If still blocked after 1 h, retry the request or temporarily use `germanywestcentral`/`swedencentral` for **both** app and DB. |
| Wrong tenant | Resources end up in the employer's directory; OIDC needs a tenant admin | Stop. `az logout`, then `az login --tenant <personal>`. Never create anything until `az account show` is correct. |
| Git Bash path mangling | `/subscriptions/...` becomes `C:/Program Files/Git/subscriptions/...` | Run `az` from PowerShell, or prefix Bash commands with `MSYS_NO_PATHCONV=1`. |
| Corporate network | Port 1433 or `login.microsoftonline.com` blocked on the office network | Run the SQL/`az` steps from home, or use the Portal's Query editor for the DB. |

---

## Phase 1: GitHub repo bootstrap

- [ ] 🤖 (👤 approve) Create the repo and push the scaffold: `gh repo create Ciepielewski-Cezar/10x-KitchenAssistant --public --source . --remote origin --push`. The remote already points at this URL, so use `git push -u origin main` if `gh` complains that the remote exists.
- [ ] 🤖 Pre-push check: scan tracked files for secrets. `UserSecretsId` and the LocalDB string are harmless. Confirm with `git grep -iE "password|apikey|sk-ant"`.
- [ ] 🤖 (👤 approve) Verify secret scanning + push protection is on (default for public repos): `gh api repos/:owner/:repo --jq .security_and_analysis`.
- [ ] 🤖 (👤 approve) Create a branch ruleset on `main` via `gh api`:
  - Require a PR (0 approvals, because a solo developer can't approve their own PR).
  - Require the `build` status check once the workflow exists (added in Phase 5).
  - Block force-push and deletion.
- [ ] 🤖 Create the GitHub environment `production`, **lower-case**, because it becomes part of the OIDC subject:
  - Required reviewer: `Ciepielewski-Cezar`, with "Prevent self-review" **off**.
  - Deployment branches: `main` only.
  - `gh api -X PUT repos/:owner/:repo/environments/production ...`
- [ ] 🤖 All following code work happens on branch `deploy/initial-azure`.

**Exit:** the repo is public with `main` pushed, the ruleset is active, and the `production` environment exists with a reviewer.

---

## Phase 2: App production-readiness (code, 🤖)

> **Status 2026-09-29:** everything below is done on branch `local-dev-setup` (see `context/changes/local-dev/local-dev-plan.md`, L1) and verified locally, **except** item 6 (the `Azure.Monitor.OpenTelemetry.AspNetCore` package and its `UseAzureMonitor()` hook). Add those when Phase 8 starts. The tool manifest ended up at the repo root (`dotnet-tools.json`, the SDK 10 default), not `.config/`.

All changes are on `deploy/initial-azure` (or `local-dev-setup` merged into it). Files:

| File | Change |
|---|---|
| `Program.cs` | Retry policy, config-driven account confirmation, Data Protection → DB, health check, startup migration, Anthropic client DI, optional Azure Monitor. |
| `Data/ApplicationDbContext.cs` | Implement `IDataProtectionKeyContext`: `public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();` |
| `Data/Migrations/<ts>_AddDataProtectionKeys.cs` | `dotnet ef migrations add AddDataProtectionKeys --output-dir Data/Migrations` |
| `KitchenAssistant.csproj` | Add `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.12 and `Azure.Monitor.OpenTelemetry.AspNetCore` (latest stable). |
| `appsettings.Production.json` (new) | `{"Identity":{"RequireConfirmedAccount":false},"Database":{"MigrateOnStartup":true}}` |
| `global.json` (new) | `{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}`. The machine also has SDK 8, and this stops it from building with the wrong one. |
| `dotnet-tools.json` (new, repo root) | Local tool `dotnet-ef` 10.0.12 (`dotnet new tool-manifest` + `dotnet tool install dotnet-ef --version 10.0.12`), so CI and dev use the same EF tool. |
| `.github/dependabot.yml` (new) | Weekly updates for `nuget` + `github-actions`. |

`Program.cs` changes, described against the current file:
1. `options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())`. This handles transient errors such as 40613, 40501 and 49918. Identity uses no explicit transactions. Any future explicit transaction must be wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
2. `options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", true);`. Development keeps the on-screen confirmation link, Production turns it off, and an app setting can change it without a redeploy.
3. `builder.Services.AddDataProtection().SetApplicationName("KitchenAssistant").PersistKeysToDbContext<ApplicationDbContext>();`. Two reasons:
   - dotnet/aspnetcore#64488 reports that .NET 10 on Linux App Service no longer persists keys by default. If keys are lost, every restart or deploy logs everyone out and antiforgery tokens fail.
   - Keys stored in the DB also survive a future move to slots.
4. `builder.Services.AddHealthChecks();` + `app.MapHealthChecks("/healthz");`. This deliberately does **not** touch the DB, so health checks never keep it busy (risk register).
5. `AnthropicClient` singleton:
   - `new AnthropicClient { ApiKey = builder.Configuration["ANTHROPIC_API_KEY"], Timeout = TimeSpan.FromSeconds(60) }`. The key comes from user-secrets in dev and from an App Service env var in prod. The SDK default timeout is 10 min, which breaks the PRD's 1-minute guardrail.
   - If the key is missing, log a **warning** at startup instead of throwing, so a missing key only breaks FR-005, not login.
   - Final timeout/retry tuning belongs to the FR-005 implementation.
6. Azure Monitor only when configured: `if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])) builder.Services.AddOpenTelemetry().UseAzureMonitor();`
7. Startup migration after `Build()`:
   - `if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup")) { using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(); }`
   - This is safe on a single B1 instance, and EF 9+ takes a migration lock anyway. It means CI never has to reach SQL, since GitHub runner IPs rotate.
8. Forwarded headers need no code change. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set as an app setting in Phase 4.

Local verification (🤖):
- [ ] `dotnet build` is clean.
- [ ] `dotnet tool restore && dotnet ef migrations has-pending-model-changes` exits 0.
- [ ] `dotnet run --launch-profile https` gives: `/healthz` → 200 `Healthy`; register shows the confirmation link (Development behaviour).
- [ ] Production rehearsal against LocalDB: `$env:ASPNETCORE_ENVIRONMENT='Production'; dotnet run --no-launch-profile --urls http://localhost:5190`. Expect migrations to apply at startup (the `DataProtectionKeys` table exists), register to log in immediately, and the auth cookie to survive a restart.
- [ ] `dotnet list package --vulnerable --include-transitive` shows nothing high or critical.

| Edge case | Symptom | Fix |
|---|---|---|
| Forgotten migration | EF 9+ throws `PendingModelChangesWarning` at `MigrateAsync` → app won't start | CI gate `dotnet ef migrations has-pending-model-changes` (Phase 5). |
| Design-time tools run the startup code | `dotnet ef` tries to migrate | Minimal-hosting design-time stops at `Build()`, so the block after it doesn't run. If it ever does, guard with `if (!EF.IsDesignTime)`. |

---

## Phase 3: Azure provisioning (👤 runs, 🤖 writes script + verifies)

🤖 writes `infra/azure/provision.ps1`, an idempotent PowerShell script. It checks before creating each resource, takes parameters (`-AppName`, `-SqlServerName`, `-Location polandcentral`, `-SqlAdmin`), and prompts for the SQL password with `Read-Host -AsSecureString`. No secrets are written to disk or echoed. 👤 reviews it and runs it.

Steps inside the script:
- [ ] Check name availability first. Web app and SQL server names are **globally unique**. Proposed names are `kitchen-assistant-cc` and `sql-kitchen-assistant-cc`.
- [ ] `az group create -n rg-kitchen-assistant -l polandcentral`
- [ ] `az appservice plan create -n plan-kitchen-assistant -g rg-kitchen-assistant --sku B1 --is-linux`. **Never F1**: it has a 5-WebSocket cap and no Always On.
- [ ] `az webapp create -n <app> -g rg-kitchen-assistant -p plan-kitchen-assistant --runtime "DOTNETCORE:10.0"`. Use a colon, not a pipe. The pipe breaks in PowerShell.
- [ ] `az sql server create -n <sql> -g rg-kitchen-assistant -l polandcentral -u <admin> -p <pw>` + `az sql server update --minimal-tls-version 1.2`
- [ ] `az sql db create -n kitchen-assistant -s <sql> -g rg-kitchen-assistant --edition Basic`. Basic DTU, **not** the free serverless offer, which auto-pauses and runs out.
- [ ] `az sql server firewall-rule create ... --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0`
- [ ] 🤖 verify: `az resource list -g rg-kitchen-assistant -o table` shows the plan, site, SQL server and DB. Read the **actual** hostname with `az webapp show --query defaultHostName`, because it may be a unique `<app>-<hash>.polandcentral-01.azurewebsites.net`.

| Edge case | Symptom | Fix |
|---|---|---|
| SQL region closed | `Location 'polandcentral' is not accepting creation of new Windows Azure SQL Database servers` | Check aka.ms/sqlcapacity. Delete the RG (👤) and re-run with `-Location germanywestcentral` (or `swedencentral`) so app and DB stay in the **same region**. |
| .NET 10 runtime missing or "Preview" | `az webapp create` rejects the runtime | Try another region. Last resort: self-contained publish (`-r linux-x64 --self-contained`) with startup command `./KitchenAssistant`. |
| Name taken | `Website with given name already exists` / `ServerNameAlreadyExists` | Re-run with a different suffix. The script is idempotent. |

---

## Phase 4: App configuration & secrets

🤖 applies the non-secret config (appended to `provision.ps1`):
- [ ] `az webapp config set -n <app> -g rg-kitchen-assistant --web-sockets-enabled true --always-on true --ftps-state Disabled --http20-enabled true --startup-file "dotnet KitchenAssistant.dll" --generic-configurations '{"healthCheckPath":"/healthz"}'`
- [ ] `az webapp update -n <app> -g rg-kitchen-assistant --https-only true --client-affinity-enabled true`. ARR affinity stays **on** for Blazor circuits.
- [ ] `az webapp config appsettings set ... --settings ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_FORWARDEDHEADERS_ENABLED=true SCM_DO_BUILD_DURING_DEPLOYMENT=false`
- [ ] `az webapp log config -n <app> -g rg-kitchen-assistant --docker-container-logging filesystem`

👤 sets the secrets. The script prompts for them, and they never appear in chat or in the repo:
- [ ] Connection string: `az webapp config connection-string set ... --connection-string-type SQLAzure --settings DefaultConnection="Server=tcp:<sql>.database.windows.net,1433;Initial Catalog=kitchen-assistant;User ID=<admin>;Password=<pw>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"`. It arrives as `SQLAZURECONNSTR_DefaultConnection`, which ASP.NET Core maps to `ConnectionStrings:DefaultConnection`.
- [ ] `az webapp config appsettings set ... --settings ANTHROPIC_API_KEY=<prod-key>`
- [ ] 🤖 verify **names only** (no values): `az webapp config appsettings list --query "[].name"` and `az webapp config show --query "{ws:webSocketsEnabled, alwaysOn:alwaysOn, health:healthCheckPath}"`.

**Exit:** all settings are present, WebSockets and Always On are true, HTTPS-only is true.

---

## Phase 5: GitHub OIDC + CI/CD pipeline

👤 runs Entra/OIDC setup via `infra/azure/setup-github-oidc.ps1`, which 🤖 writes:
- [ ] `az ad app create --display-name gh-kitchen-assistant-deploy` → `appId`; `az ad sp create --id <appId>`
- [ ] Federated credential from a JSON **file**, because inline JSON quoting breaks in PowerShell. `fic.json`:
  `{"name":"gh-production","issuer":"https://token.actions.githubusercontent.com","subject":"repo:Ciepielewski-Cezar/10x-KitchenAssistant:environment:production","audiences":["api://AzureADTokenExchange"]}`
  Then `az ad app federated-credential create --id <appId> --parameters "@fic.json"`.
- [ ] `az role assignment create --assignee <appId> --role "Website Contributor" --scope $(az webapp show -n <app> -g rg-kitchen-assistant --query id -o tsv)`. This is scoped to the web app only, with no SQL, RG or billing rights.
- [ ] 🤖 (👤 approve) Store the environment secrets on `production` (not the repo): `gh secret set AZURE_CLIENT_ID|AZURE_TENANT_ID|AZURE_SUBSCRIPTION_ID --env production`, plus the variable `gh variable set AZURE_WEBAPP_NAME --env production`.

🤖 writes `.github/workflows/deploy.yml`:
- **Triggers:** `pull_request` → `main` (build only), `push` → `main` (build + deploy), `workflow_dispatch` (manual redeploy/rollback).
- **`concurrency: { group: production-deploy, cancel-in-progress: false }`**
- **Job `build`** (ubuntu-latest, `permissions: contents: read`):
  - `actions/checkout@v4`
  - `actions/setup-dotnet@v4` with `dotnet-version: 10.0.x`
  - `dotnet restore`
  - `dotnet build -c Release --no-restore`
  - `dotnet tool restore` + `dotnet ef migrations has-pending-model-changes`
  - `dotnet test -c Release --no-build`. There are no tests yet, so this passes trivially until `KitchenAssistant.Tests` exists.
  - `dotnet publish KitchenAssistant.csproj -c Release -o publish --no-build`
  - `actions/upload-artifact@v4` (name `webapp`, retention 30 days)
- **Job `deploy`**:
  - Runs `needs: build`, `if: github.event_name != 'pull_request'`, with `environment: { name: production, url: https://<host> }`.
  - `permissions: { id-token: write, contents: read }`
  - Steps: `actions/download-artifact@v4` → `azure/login@v2` (client/tenant/subscription IDs from secrets) → `azure/webapps-deploy@v3` (`app-name: ${{ vars.AZURE_WEBAPP_NAME }}`, `package: webapp`) → a smoke step that curls `https://<host>/healthz` with retries (up to 12 × 10 s) and **fails the job** if it never returns 200.
- [ ] 🤖 After the first PR run, add `build` as a required status check in the `main` ruleset.

| Edge case | Symptom | Fix |
|---|---|---|
| OIDC subject mismatch | `AADSTS700213: No matching federated identity record found` | The error prints the presented subject. Copy it **exactly** into the FIC (matching is case-sensitive, and the environment name must be lower-case). A job with `environment:` uses `…:environment:production`, not `…:ref:refs/heads/main`. |
| Missing `id-token: write` | `Unable to get ACTIONS_ID_TOKEN_REQUEST_URL` | Add it to the deploy job's `permissions`. |
| Role not yet propagated | `No subscriptions found` / 403 from Kudu or `webapps-deploy` | Wait 5–10 min and re-run the job. Check with `az role assignment list --assignee <appId> --all`. |
| PR from a fork | Secrets are unavailable | This is intended: PRs only build and never log in to Azure. |

---

## Phase 6: First production deploy

- [ ] 🤖 Open a PR `deploy/initial-azure` → `main` with Phase 2 + infra scripts + workflow. CI `build` must be green.
- [ ] 👤 Review and **merge** (human-only). The `deploy` job waits for approval in the `production` environment.
- [ ] 👤 Approve the deployment in the Actions UI.
- [ ] 🤖 Watch it: `gh run watch <id>`. On failure, `gh run view <id> --log-failed` and follow the table below.

| Edge case | Symptom | Fix |
|---|---|---|
| App won't start | Browser shows `:( Application Error`, smoke step fails | `az webapp log tail -n <app> -g rg-kitchen-assistant`. The usual causes are the next rows. |
| Missing connection string | `InvalidOperationException: Connection string 'DefaultConnection' not found` | The Phase 4 connection string is missing or named wrong. It must be exactly `DefaultConnection`. |
| SQL firewall | `SqlException 40615 … Client with IP address … is not allowed` | The `AllowAzureServices` rule is missing. Re-run the Phase 3 firewall step (👤). |
| SQL login failed | `SqlException 18456` | Wrong password in the connection string. Reset it with `az sql server update -p` (👤), then update the connection string. |
| Startup timeout | "Container didn't respond to HTTP pings on port 8080" | A slow first migration on 5 DTU. Set `WEBSITES_CONTAINER_START_TIME_LIMIT=600` and restart. Check that the startup command dll name matches. |
| Redirect loop / HTTP cookies | Infinite 307s, or "Failed to determine the https port" | `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is missing. |
| Oryx tries to build | Deploy log shows `dotnet restore` on the server, or it fails | `SCM_DO_BUILD_DURING_DEPLOYMENT=false`. |

---

## Phase 7: Production verification

- [ ] 🤖 `curl -sf https://<host>/healthz` returns `Healthy`. `http://` redirects to `https://`.
- [ ] 🤖 In the built-in browser: open the home page, then **register** a test user. It should log in directly with no confirmation page. Log out and log back in.
- [ ] 🤖/👤 **Check the WebSockets transport:** on the Counter page, DevTools → Network → WS should show `_blazor?id=…` with status **101**, not repeated long-polling POSTs. Clicking should increment the counter.
- [ ] 🤖 **Data Protection survives a restart:** `az webapp restart -n <app> -g rg-kitchen-assistant`. The user should still be logged in afterwards and forms should still submit (no antiforgery error).
- [ ] 🤖 Anthropic wiring: the startup log has **no** "ANTHROPIC_API_KEY missing" warning (`az webapp log tail`). The real call is verified when FR-005 lands.
- [ ] 🤖 `az webapp log tail` shows no unhandled exceptions over a 5-minute browse.

**Exit:** every box above is ticked.

---

## Phase 8: Observability & cost guardrails

- [ ] 🤖 (👤 approve) Log Analytics workspace **with a daily cap**: `az monitor log-analytics workspace create -g rg-kitchen-assistant -n log-kitchen-assistant -l polandcentral --quota 0.1`. The 0.1 GB/day cap stops runaway ingestion costs.
- [ ] 🤖 (👤 approve) App Insights: `az monitor app-insights component create --app ai-kitchen-assistant -g rg-kitchen-assistant -l polandcentral --workspace <ws-id>`. Then set app setting `APPLICATIONINSIGHTS_CONNECTION_STRING` (this restarts the app). The Phase 2 code switches Azure Monitor on automatically.
- [ ] 👤 Portal: add a **Standard availability test** on `https://<host>/healthz` every 5 min from 3 locations, with an alert to your email via an action group. This catches the "error page hid the outage for days" failure from the pre-mortem.
- [ ] 👤 Portal: Cost Management → **Budget** of $25/month, with alerts at 80% actual and 100% forecast. After the PAYG upgrade there is no spending cap.
- [ ] 👤 Anthropic console: confirm the workspace spend limit and email alerts.

---

## Phase 9: Rollback drill & hand-off docs

- [ ] 🤖 **Rollback drill:** run `gh workflow run deploy.yml --ref <previous-good-sha>`. The workflow_dispatch path builds and deploys that ref. Confirm `/healthz` passes, then redeploy `main`.
  - Rule: migrations must stay backward-compatible with the previous build, because nothing rolls schema back automatically.
  - `gh run rerun` only works within 30 days of the original run.
- [ ] 🤖 Fill in this plan's actuals (app name, hostname, region, run IDs) and tick every phase in the tracker. This file is the downstream "what's deployed" ground truth.
- [ ] 🤖 Add a short "Deploy & operate" block to `CLAUDE.md`, outside the 10x-cli markers:
  - the deploy workflow name;
  - `az webapp log tail …`;
  - rollback = `gh workflow run deploy.yml --ref <sha>`;
  - human-only actions list;
  - the new `infra/azure/*.ps1` scripts.
- [ ] 🤖 Record in `context/foundation/infrastructure.md`'s risk register (if you approve): "email confirmation off in Production (unverified emails)" and "SQL admin used by app until Phase 10".

---

## Phase 10: Post-MVP hardening (backlog, not part of this execution)

- [ ] Managed identity for SQL: set an Entra admin on the server, `CREATE USER [<app>] FROM EXTERNAL PROVIDER` + `db_datareader/db_datawriter/db_ddladmin`, and switch the connection string to `Authentication=Active Directory Default`. Then remove the SQL admin from the app.
- [ ] Replace `AllowAzureServices` with the web app's `possibleOutboundIpAddresses`, or with a private endpoint.
- [ ] Key Vault references for `ANTHROPIC_API_KEY`.
- [ ] Real email confirmation (Azure Communication Services Email) and re-enable `RequireConfirmedAccount`.
- [ ] P0v3 + a `staging` slot if zero-downtime deploys are needed.

---

## Critical files (new / modified)

- `Program.cs`, `Data/ApplicationDbContext.cs`, `Data/Migrations/*_AddDataProtectionKeys.cs`, `KitchenAssistant.csproj`
- `appsettings.Production.json`, `global.json`, `dotnet-tools.json`
- `.github/workflows/deploy.yml`, `.github/dependabot.yml`
- `infra/azure/provision.ps1`, `infra/azure/setup-github-oidc.ps1`
- `context/changes/deployment/deployment-plan.md`, `CLAUDE.md` (outside the managed markers)

## End-to-end verification summary

1. Local: build, the pending-model-changes gate, and a Production-mode rehearsal against LocalDB (Phase 2).
2. CI: the PR `build` check is green; after merge, the `deploy` job's smoke test passes on `/healthz` (Phases 5–6).
3. Prod: register/login, WS 101 on `_blazor`, auth survives `az webapp restart`, clean log tail (Phase 7).
4. Ops: the availability alert fires on a test (optional: stop the app for 10 min), the budget exists, and the rollback drill succeeds (Phases 8–9).

Estimated run cost: B1 ~$13.15 + SQL Basic ~$4.90 + App Insights ≈ $0 at the 0.1 GB/day cap, so **≈ $18/mo** plus Anthropic usage (capped in the console).
