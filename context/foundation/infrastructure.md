---
project: kitchen-assistant
researched_at: 2026-09-28
recommended_platform: Azure App Service (Linux, B1) + Azure SQL Database (Basic)
runner_up: Render (Starter, Frankfurt) + external Azure SQL
context_type: mvp
tech_stack:
  language: C#
  framework: ASP.NET Core Blazor Web App (.NET 10, InteractiveServer per page) + ASP.NET Core Identity + EF Core 10
  runtime: .NET 10 (net10.0) / Kestrel
  database: SQL Server (LocalDB dev, Azure SQL prod)
---

## Recommendation

**Deploy on Azure App Service (Linux, B1 plan, Poland Central) with Azure SQL Database (Basic DTU) in the same region.**

App Service is the only researched platform that runs .NET 10 natively (no Dockerfile) *and* hosts SQL Server as a first-party service, so the stack in `tech-stack.md` (EF Core SQL Server provider, Identity, GitHub Actions on merge to `main`) deploys without any provider swap or container layer. It scored highest on the platform comparison (14/16), and the interview answers tipped the rest: persistent connections are required (Blazor Server SignalR circuits), a single region is fine (Poland Central is in-country), co-location is undecided (Azure is the only option where DB and app share a vendor), and the only prior exposure is an Azure tutorial. Every other passing platform would still point at Azure SQL across clouds.

Chosen tiers: **B1** (not F1 — F1 caps WebSockets at 5 per instance and has no Always On) and **Basic DTU SQL** (not the free offer — see risk register). Estimated cost: **~$13.15/mo (B1, Poland Central, Linux) + ~$4.90/mo (Basic DTU) ≈ $18/mo**, excluding Anthropic API usage.

## Platform Comparison

Scoring: Pass = 2, Partial = 1, Fail = 0. CLI-first, Managed and Stable deploy API weighted ×2 (critical), Agent-readable docs and MCP ×1. Max 16. Hard filters applied first: must run a persistent .NET 10 Kestrel process holding WebSocket circuits (interview Q1 = Yes).

| Platform | Hard filter | CLI-first | Managed/Serverless | Agent-readable docs | Stable deploy API | MCP / Integration | Weighted total |
|---|---|---|---|---|---|---|---|
| Azure App Service | Pass | Pass | Pass | Pass | Partial | Pass | **14** |
| Render | Pass | Partial | Partial | Pass | Pass | Pass | **12** |
| Railway | Pass | Partial | Pass | Pass | Partial | Pass | **12** |
| Fly.io | Pass | Pass | Partial | Pass | Partial | Partial | 11 |
| Cloudflare (Containers) | Conditional | Pass | Partial | Pass | Partial | Partial | 11 |
| Vercel | **Fail** | — | — | — | — | — | dropped |
| Netlify | **Fail** | — | — | — | — | — | dropped |

**Azure App Service.** CLI: `az webapp deploy` / `az webapp up` / `az webapp config` / `az webapp log tail` cover the full loop (GA). Managed: native `DOTNETCORE:10.0` runtime stack, platform TLS, health checks; no container to maintain. Docs: markdown source on GitHub (`MicrosoftDocs/azure-docs`, `dotnet/AspNetCore.Docs`), Learn pages return markdown via `?accept=text/markdown`, plus the free Microsoft Learn MCP server; note `learn.microsoft.com/llms.txt` returned 404 (checked 2026-09-28). Deploy API: deterministic deploy via `azure/webapps-deploy@v3`, but **Partial** because instant rollback (slot swap) needs Standard/Premium; on B1 rollback = redeploy previous artifact. MCP: Azure MCP Server 2.0 is GA with App Service, SQL and Monitor tools (its IDE "Agent Mode" integration is public preview, checked 2026-09-28).

**Render.** No native .NET runtime — Docker required (GA). CLI (GA) has `deploys create --wait`, `logs --tail`, JSON output, but no rollback command (rollback via dashboard or REST API) → Partial. Managed Partial because the Dockerfile is ours to maintain. Docs: `render.com/llms.txt` + `.md` pages. Deploy API Pass: REST rollback, deploy hooks, `render.yaml` Blueprints with `autoDeployTrigger: checksPass`. MCP server GA since 2025-08-21 (cannot create image-backed services or delete anything). Free tier spins down after 15 min → Starter ($7/mo, 512 MB) is the floor. No SQL Server → external Azure SQL. ~$7–12/mo.

**Railway.** Railpack auto-detects `.csproj` and builds .NET, but Railway's own ASP.NET Core guide still says Railpack doesn't support .NET and ships a .NET 9 Dockerfile — docs conflict (checked 2026-09-28). CLI Partial: `railway up --ci`, `logs`, `variable`, `redeploy`, but rollback to a chosen deployment is dashboard-only (images retained 72 h on Hobby). Docs: `llms.txt` + `.md`. MCP: official (remote `mcp.railway.com` or `railway mcp local`). Nearest region Amsterdam. SQL Server only via community templates needing ≥2 GB RAM. ~$5–10/mo with Azure SQL.

**Fly.io.** `fly launch` generates a .NET Dockerfile; guide is thin and dated. CLI strong, but no native rollback command (`fly releases --image` + `fly deploy --image …`; secrets/`fly.toml` not reverted). Since 2026-04-09 autostop closes open WebSockets → must set `auto_stop_machines = "off"`, single machine (no built-in sticky sessions). `waw` region deprecated (2025-09-09); nearest `ams`/`fra`. `fly mcp server` is **experimental** and does not deploy. Credit card required, no free allowance. ~$6–14/mo with Azure SQL.

**Cloudflare (Containers).** Workers/Pages cannot run Kestrel; Containers (GA 2026-04-13) can, fronted by a JS Worker + Durable Object shim. Default `sleepAfter` 10 min, undocumented whether an open WebSocket counts as activity, ephemeral disk, irregular host restarts that drop circuits. Rearchitecture cost too high for the MVP deadline. ~$12–13/mo.

**Vercel — dropped.** No .NET runtime; Container Images on Functions are **Beta**, WebSockets **Public Beta** (2026-06-22), connections capped by function max duration (300 s Hobby / 800 s Pro) and scale-down after 5 min idle. Cannot hold Blazor circuits.

**Netlify — dropped.** Functions are TS/JS/Go only; no containers, no WebSockets.

A static Blazor WebAssembly build could be hosted on Cloudflare/Vercel/Netlify, but it would abandon InteractiveServer, break the static-SSR Identity pages that need `HttpContext`, and still require a separate .NET API host for EF Core and server-side scoring — so it solves nothing.

### Shortlisted Platforms

#### 1. Azure App Service (Recommended)

Native .NET 10 runtime (no Dockerfile), native SQL Server via Azure SQL in the same region (Poland Central), passwordless DB access via managed identity (GA), GA MCP server, first-party GitHub Actions (`azure/login@v2` with OIDC + `azure/webapps-deploy@v3`). Matches the deployment target already declared in `tech-stack.md` and `CLAUDE.md`. The one Partial — rollback on B1 — is acceptable for a dozen users and is mitigated by redeploying the previous build artifact.

#### 2. Render

Cheapest realistic always-on option (~$7 Starter + Azure SQL), strongest deploy API of the non-Azure options (REST rollback, Blueprints-as-code), GA MCP server. Gap vs. Azure: needs a Dockerfile, no SQL Server (cross-cloud DB from Frankfurt, shared outbound IP ranges to allowlist in the Azure SQL firewall), 512 MB is tight for .NET.

#### 3. Railway

Best DX and possibly zero-config .NET builds via Railpack, GA-ish MCP, usage billing that fits a tiny app. Gap vs. Azure: rollback is dashboard-only, conflicting .NET documentation, no SQL Server (external Azure SQL from Amsterdam), no official GitHub Action.

## Anti-Bias Cross-Check: Azure App Service

### Devil's Advocate — Weaknesses

1. **B1 has no deployment slots.** Every deploy restarts the single instance and drops every live Blazor circuit; rollback is "redeploy the previous artifact", not an instant swap. Slots start at P0v3 (~$65/mo) — 5× the cost.
2. **The Azure SQL free offer is a trap for this app.** 100k vCore-seconds ≈ 55 h online/month at 0.5 vCore; each visit keeps the serverless DB awake ≥ 15 min (default 60) after last activity; Always On pings or a DB-touching health check burn it in days; the default "auto-pause until next month" then leaves the DB inaccessible for the rest of the month. Resuming returns error 40613 and takes ~1 min, colliding with the PRD's one-minute recipe guardrail.
3. **Setup surface and identity plumbing.** Resource group, App Service plan, web app, SQL logical server, database, firewall, Entra app registration + federated credential for GitHub OIDC, role assignment, managed-identity SQL user (`CREATE USER … FROM EXTERNAL PROVIDER` run by the Entra admin). For a developer with only tutorial-level Azure exposure, this is where days disappear.
4. **.NET 10 may still show "(Preview)" in some regions.** .NET 10 has been on App Service since 2025-08-26 (preview) and GA since 2025-11-11, but the portal kept a preview tag per region during validation; no dated post confirms its removal for Poland Central (checked 2026-09-28). Fallback is a self-contained publish.
5. **No hard spend cap.** A pay-as-you-go subscription has no spending limit; B1 + Basic SQL + Application Insights ingestion + egress accrue silently unless a budget alert is configured by hand.

### Pre-Mortem — How This Could Fail

The team followed the tutorial they knew: a free-tier F1 web app plus the Azure SQL free offer, live on 20 October. Demo day went fine. A week later users complained that the recipe page froze whenever they had a second or third tab open — F1 allows five WebSocket connections, and WebSockets had never been switched on, so SignalR had quietly degraded to long-polling and masked the problem. Upgrading to B1 fixed it and Always On was enabled to kill cold starts. But the health check they had wired up touched the database every few minutes, so the free SQL allowance ran dry by 9 November and the database paused until December; nobody noticed for two days because the app returned a generic error page. Meanwhile every push to `main` restarted the only instance and logged out anyone mid-recipe. The OIDC setup had been done under a work account in the employer's Entra tenant; when IT cleaned up app registrations, deploys broke and the developer had no rights to recreate them. None of this was Azure being bad — every default was tuned for someone other than a solo developer with a dozen users.

### Unknown Unknowns

- **Corporate vs. personal tenant.** The git identity is a work (Euvic) account. If Azure is signed into via the employer's Entra tenant, creating app registrations / role assignments for GitHub OIDC may need a tenant admin, and the resources belong to the employer. Use a personal subscription.
- **WebSockets Off by default fails silently.** Blazor still works over long-polling, so the misconfiguration shows up only as latency and connection-limit weirdness, not as an error.
- **Data Protection keys survive restarts on B1 but not slot swaps.** Keys live in `%HOME%` (shared across instances/restarts, not across slots). Any future move to slots logs everyone out unless keys are persisted to the database.
- **Error 40613 + EF Core retry strategy.** `EnableRetryOnFailure` is needed for transient Azure SQL errors, but it throws on user-initiated transactions unless they are wrapped in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- **`az` is not installed on the dev machine** (checked 2026-09-28). Every CLI step — and any agent operating the deployment — needs the Azure CLI first.

## Operational Story

- **Preview deploys**: None on B1 (no slots). PRs run build + tests in GitHub Actions only; the first "preview" is production after merge to `main`. If previews become necessary, upgrade to P0v3 and add a `staging` slot deployed from PR branches (protected by App Service Authentication / Entra sign-in, since slots are publicly reachable by default).
- **Secrets**: `ANTHROPIC_API_KEY` and the SQL connection string live in App Service **Application settings / Connection strings** (encrypted at rest, injected as env vars; readable by anyone with Contributor on the web app). GitHub holds only OIDC identifiers (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) as secrets — no publish profile, no passwords. Rotation: rotate the Anthropic key in its console, then `az webapp config appsettings set --name <app> --resource-group <rg> --settings ANTHROPIC_API_KEY=<new>` (restarts the app). Optional hardening: Key Vault references with the app's managed identity.
- **Rollback**: Re-run the GitHub Actions deploy job for the last good commit (`gh run rerun <run-id>` on the previous successful run, or revert the commit on `main`). Time-to-revert ≈ 3–5 min (build + deploy + restart). EF Core migrations do **not** roll back automatically — schema changes must be backward-compatible with the previous build, or a down-migration bundle must be run manually.
- **Approval**: Human-only: merge to `main` (= production deploy), creating/deleting Azure resources, changing the App Service plan tier, rotating secrets, dropping or restoring the database, changing SQL firewall rules, Entra/OIDC changes. Agent may unattended: read logs, list deployments, read app settings names, run `az webapp restart`, open PRs.
- **Logs**: Runtime: `az webapp log tail --name <app> --resource-group <rg>` (enable first with `az webapp log config --name <app> --resource-group <rg> --docker-container-logging filesystem`). Pipeline: `gh run list --workflow <deploy.yml>` and `gh run view <run-id> --log-failed`. Structured queries: Azure MCP Server (Monitor / App Service tools) or Application Insights via `az monitor app-insights query`.

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| F1 tier caps WebSockets at 5/instance and has no Always On | Research finding | H (if F1 chosen) | H | Use B1 from day one; never deploy Blazor InteractiveServer to F1. |
| WebSockets left Off → silent long-polling fallback | Unknown unknowns | H | M | `az webapp config set --web-sockets-enabled true`; verify in browser devtools that `_blazor` uses a WebSocket transport. |
| Azure SQL free offer exhausted / auto-paused until next month | Devil's advocate | H | H | Use Basic DTU (~$4.90/mo) instead of the free offer; if the free offer is used, set "continue with additional charges" and keep health checks off the DB. |
| Paused/transient SQL returns 40613, first request exceeds 1-min guardrail | Devil's advocate | M | M | `EnableRetryOnFailure` in `UseSqlServer`; Basic DTU does not auto-pause. |
| Every deploy restarts the only instance and drops circuits | Devil's advocate | H | L | Deploy outside usage hours; Blazor reconnect UI; persist Data Protection keys so auth cookies survive. |
| No instant rollback on B1 | Devil's advocate | M | M | Keep migrations backward-compatible; roll back by re-running the last good deploy run; upgrade to P0v3 + slots post-MVP if needed. |
| Setup/identity complexity overruns the 3-week after-hours budget | Devil's advocate / Pre-mortem | M | H | Script resource creation with `az` CLI in the deploy plan; start with OIDC via `az ad app` + federated credential; defer managed-identity SQL auth (use SQL auth connection string in app settings) if it blocks. |
| Resources/OIDC created in employer's Entra tenant | Unknown unknowns / Pre-mortem | M | H | Use a personal Azure subscription and personal account; confirm tenant with `az account show` before creating anything. |
| .NET 10 runtime shown as "(Preview)" or missing in Poland Central | Devil's advocate | L | M | `az webapp list-runtimes --os linux` before creating the app; fallback: West Europe region or self-contained `linux-x64` publish. |
| Data Protection keys lost on slot swap (future) | Unknown unknowns | L | M | Persist keys to SQL with `PersistKeysToDbContext` (package `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`). |
| EF retry strategy breaks user-initiated transactions | Unknown unknowns | L | M | Wrap explicit transactions in `Database.CreateExecutionStrategy().ExecuteAsync(...)`. |
| Runaway spend (no hard cap on pay-as-you-go) | Devil's advocate | L | M | `az consumption budget create` with an alert at e.g. $25/mo; watch Application Insights ingestion. |
| Health check / Always On keeps DB busy | Pre-mortem | M | L | Health endpoint must not query the DB (or use a lightweight check on Basic DTU only). |
| Error page hides DB outage for days | Pre-mortem | M | M | Enable Application Insights with an availability/failure alert to email. |
| SQL firewall blocks migrations from CI (GitHub runner IPs rotate) | Research finding | M | M | Don't connect from CI; apply migrations at app startup (single instance) or run an EF migrations bundle manually from the dev machine with a temporary client-IP firewall rule. |
| `az` CLI missing locally → agent cannot operate the platform | Unknown unknowns | H | M | `winget install Microsoft.AzureCLI`, then `az login`. |

## Getting Started

Validated against the pinned stack: `net10.0`, EF Core / Identity 10.0.12, GitHub Actions deploy on merge to `main`.

1. **Install and sign in to the Azure CLI (personal subscription).**
   ```bash
   winget install Microsoft.AzureCLI
   az login
   az account show            # confirm the personal subscription/tenant, not the employer's
   az webapp list-runtimes --os linux | grep -i dotnetcore   # expect DOTNETCORE:10.0
   ```
2. **Create the resources in Poland Central (B1 Linux + Basic SQL).**
   ```bash
   az group create --name rg-kitchen-assistant --location polandcentral
   az appservice plan create --name plan-kitchen-assistant --resource-group rg-kitchen-assistant --sku B1 --is-linux
   az webapp create --name <app-name> --resource-group rg-kitchen-assistant --plan plan-kitchen-assistant --runtime "DOTNETCORE:10.0"
   az webapp config set --name <app-name> --resource-group rg-kitchen-assistant --web-sockets-enabled true --always-on true
   az sql server create --name <sql-server> --resource-group rg-kitchen-assistant --location polandcentral --admin-user <admin> --admin-password <password>
   az sql db create --name kitchen-assistant --server <sql-server> --resource-group rg-kitchen-assistant --edition Basic
   az sql server firewall-rule create --server <sql-server> --resource-group rg-kitchen-assistant --name AllowAzure --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
   ```
   ARR affinity is On by default — leave it on (Blazor Server circuits).
3. **Wire app settings (secrets stay in Azure, not in the repo).**
   ```bash
   az webapp config connection-string set --name <app-name> --resource-group rg-kitchen-assistant --connection-string-type SQLAzure --settings DefaultConnection="<azure-sql-connection-string>"
   az webapp config appsettings set --name <app-name> --resource-group rg-kitchen-assistant --settings ANTHROPIC_API_KEY=<key> ASPNETCORE_ENVIRONMENT=Production
   ```
   In code: `UseSqlServer(..., o => o.EnableRetryOnFailure())`, and persist Data Protection keys to the DB.
4. **Set up GitHub Actions with OIDC (no publish profile).** Create an Entra app registration + federated credential for `repo:<owner>/<repo>:ref:refs/heads/main`, grant it `Website Contributor` on the web app, and store `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` as GitHub secrets. Workflow: `actions/setup-dotnet` with `dotnet-version: 10.0.x` → `dotnet publish -c Release -o ./publish` → `azure/login@v2` → `azure/webapps-deploy@v3` with `package: ./publish`.
5. **Apply migrations and verify.** Apply EF Core migrations (at startup for the single B1 instance, or `dotnet ef migrations bundle` run from the dev machine with a temporary client-IP firewall rule), then check `https://<app-name>.azurewebsites.net`, confirm the `_blazor` connection uses WebSockets, and tail logs with `az webapp log tail`.

## Out of Scope

The following were not evaluated in this research:
- Docker image configuration
- CI/CD pipeline setup (beyond naming the actions used in Getting Started)
- Production-scale architecture (multi-region, HA, DR)
