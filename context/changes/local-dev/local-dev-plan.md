# Local development & production-like local deploy: Kitchen Assistant

## Context

The cloud deploy (`context/changes/deployment/deployment-plan.md`) is parked for later. For now the goal is a working local setup at two levels:

1. **Dev loop**: `dotnet watch` against LocalDB, with user-secrets for the Anthropic key. This is used for day-to-day feature work (FR-001…FR-006).
2. **Production-like local deploy**: a Docker Compose stack with the app in a **Linux .NET 10 container**, running in `Production`, against a **SQL Server container with SQL auth**. It behaves like App Service Linux + Azure SQL, so Linux, Production-config and startup-migration problems show up here rather than on Azure.

The code changes in L1 are the same as **Phase 2 of the cloud plan**. Doing them now means the cloud plan later picks up at Phase 3.

Environment snapshot (checked 2026-09-29):

| Item | State |
|---|---|
| .NET SDKs | 10.0.401 and 8.0.425. No `global.json`, so nothing pins the SDK. |
| LocalDB | `MSSQLLocalDB` v17 installed (stopped, auto-starts on connect) |
| Dev database | `aspnet-KitchenAssistant-40efb3c6-…` exists; `00000000000000_CreateIdentitySchema` applied |
| HTTPS dev cert | Trusted, valid until 2027-07-29 |
| `dotnet-ef` | 10.0.12, global tool |
| User-secrets | **None set** (no `ANTHROPIC_API_KEY`) |
| Docker | Rancher Desktop 29.5.3 (moby, WSL), Compose v5.1.4, 16 GB, x86_64 |
| Ports 7020 / 5180 | Free |

**Owner legend:** 🤖 = agent · 👤 = you. **Status:** ⬜ todo · 🟡 in progress · ✅ done · ⛔ blocked.

## Phase tracker

| # | Phase | Owner | Status |
|---|---|---|---|
| L0 | Prerequisites | 🤖 verified | ✅ (only user-secrets missing, handled in L2) |
| L1 | Code readiness (= cloud Phase 2) | 🤖 | ✅ 2026-09-29 |
| L2 | Dev loop on LocalDB | 🤖 + 👤 secret | ✅ 2026-09-29 (Anthropic key deferred) |
| L3 | Production-like Docker stack | 🤖 + 👤 `.env` secrets | ✅ 2026-09-29 |
| L4 | Hand-off & docs | 🤖 | ✅ 2026-09-29 (commit f56e334) |

All work happens on the local branch `local-dev-setup`. Nothing is pushed to GitHub or Azure. Commit only when you say so.

---

## L1: Code readiness (🤖)

| File | Change |
|---|---|
| `Program.cs` | See the numbered list below. |
| `Data/ApplicationDbContext.cs` | Implement `IDataProtectionKeyContext` → `DbSet<DataProtectionKey> DataProtectionKeys`. |
| `Data/Migrations/<ts>_AddDataProtectionKeys.cs` | `dotnet ef migrations add AddDataProtectionKeys --output-dir Data/Migrations` |
| `KitchenAssistant.csproj` | Add `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.12. Add `<ContainerRepository>kitchen-assistant</ContainerRepository>` so the SDK can build the image with no Dockerfile. |
| `appsettings.Production.json` (new) | `{"Identity":{"RequireConfirmedAccount":false},"Database":{"MigrateOnStartup":true}}` |
| `global.json` (new) | `{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}`, which rules out SDK 8. |
| `dotnet-tools.json` (new, repo root: the SDK 10 default location) | Local tool `dotnet-ef` 10.0.12. Then `dotnet tool restore` works the same locally and in CI. |

`Program.cs` changes:
1. `UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())`
2. `RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", true)`. Development keeps the on-screen confirmation link; Production (the Docker stack, later Azure) skips it.
3. `AddDataProtection().SetApplicationName("KitchenAssistant").PersistKeysToDbContext<ApplicationDbContext>()`. Keys live in the DB, so logins survive container or app restarts. This is also the fix for .NET 10 on Linux App Service.
4. `AddHealthChecks()` + `MapHealthChecks("/healthz")` with no DB check.
5. `AnthropicClient` singleton: `ApiKey = builder.Configuration["ANTHROPIC_API_KEY"]`, `Timeout = 60 s`. If the key is missing, log a warning at startup; login still works. No FR-005 logic yet.
6. Startup migration after `Build()`, when `Database:MigrateOnStartup` is true (Production only).

Deferred to the cloud plan: Azure Monitor/OpenTelemetry (there's nothing to send to locally).

- [x] `dotnet build` is clean
- [x] `dotnet tool restore && dotnet ef migrations has-pending-model-changes` exits 0
- [x] `dotnet list package --vulnerable --include-transitive` shows 0 high/critical

---

## L2: Dev loop on LocalDB

- [ ] ⏸️ 👤 (deferred, you'll add it later) Set the dev key yourself, not in chat. Use a **dev** key, separate from the future prod key: `dotnet user-secrets set ANTHROPIC_API_KEY <dev-key>`
- [ ] ⏸️ 🤖 (after the key is set) Confirm the secret name exists: `dotnet user-secrets list` shows the name only.
- [x] 🤖 `dotnet ef database update` applies `AddDataProtectionKeys` to LocalDB.
- [x] 🤖 `dotnet watch --launch-profile https` should serve https://localhost:7020.
- [x] 🤖 Verify in the built-in browser:
  - `/healthz` returns `Healthy`.
  - Register a test user. The confirmation-link page appears (Development behaviour); click it, then log in.
  - The Counter page increments, so the InteractiveServer circuit works.
  - Log out and back in.
- [x] 🤖 Restart the app and confirm you're still logged in (keys are now in the `DataProtectionKeys` table).
- [ ] ⏸️ 🤖 (after the key is set; the warning is currently expected) The startup log has no "ANTHROPIC_API_KEY missing" warning.

Daily commands (they go into `CLAUDE.md` in L4):
```bash
dotnet watch --launch-profile https
dotnet ef migrations add <Name> --output-dir Data/Migrations
dotnet ef migrations has-pending-model-changes
dotnet ef database update
dotnet ef database drop -f && dotnet ef database update   # reset local DB (destructive, local only)
```

| Edge case | Symptom | Fix |
|---|---|---|
| LocalDB won't start | `SqlException: A network-related or instance-specific error… (provider: SQL Network Interfaces, error: 50)` | `sqllocaldb start mssqllocaldb`. If it's still broken: `sqllocaldb stop mssqllocaldb`, `sqllocaldb delete mssqllocaldb`, `sqllocaldb create mssqllocaldb`, then `dotnet ef database update`. |
| Wrong SDK picked | Build errors about `net10.0` not supported | `global.json` from L1 fixes it. Check with `dotnet --version`, which should say 10.0.x. |
| Port in use | `Failed to bind to address https://127.0.0.1:7020` | Find the process: `Get-NetTCPConnection -LocalPort 7020`. Stop the stale `dotnet watch` or change the port in `launchSettings.json`. |
| Cert warning | Browser says NET::ERR_CERT_AUTHORITY_INVALID | `dotnet dev-certs https --clean` then `dotnet dev-certs https --trust` (Windows shows a prompt). |
| Hot reload can't apply an edit | `dotnet watch` reports a "rude edit" | Press `Ctrl+R` in the watch terminal to restart. Signature, DI or `Program.cs` changes always need a restart. |
| Corporate TLS inspection (work laptop/VPN) | Anthropic calls fail with `The remote certificate is invalid` / `UntrustedRoot`; NuGet restore fails | Run off VPN, or make sure the corporate root CA is in the Windows cert store. Never disable certificate validation in code. |
| Circuit disconnects while debugging | "Attempting to reconnect…" after a breakpoint | Expected: pausing the server stops the circuit heartbeat. Resume, and the circuit reconnects or the page reloads. |

---

## L3: Production-like Docker stack

Mirrors Azure: Linux container, `ASPNETCORE_ENVIRONMENT=Production`, SQL auth, migrations at startup, email confirmation off. **No Dockerfile**: the image is built by the .NET SDK (`PublishContainer`) on the official `mcr.microsoft.com/dotnet/aspnet:10.0` base, which is the same runtime family App Service uses. So `infrastructure.md`'s "no Docker layer to maintain" still holds.

Files (🤖):

| File | Purpose |
|---|---|
| `compose.yaml` | Two services: `sql` and `app`, described below. |
| `.env.example` | Committed template: `MSSQL_SA_PASSWORD=`, `ANTHROPIC_API_KEY=`. |
| `.env` | 👤 your real local values. **Already covered by `.gitignore`**, and 🤖 verifies with `git check-ignore .env`. |
| `scripts/local-prod.ps1` | One command: build the image, `docker compose up -d`, wait for `/healthz`, print the URL. Supports `-Down` (stop) and `-Reset` (also delete the SQL volume). |

The two services in `compose.yaml`:
- `sql`:
  - Image `mcr.microsoft.com/mssql/server:2022-latest` with `ACCEPT_EULA=Y` and `MSSQL_SA_PASSWORD` from `.env`.
  - Named volume `sqldata`. Port `14330:1433`, so it can't collide with anything local.
  - Healthcheck: `/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$$MSSQL_SA_PASSWORD" -Q "SELECT 1"`.
- `app`:
  - Image `kitchen-assistant:local`, with `depends_on: sql: condition: service_healthy`.
  - Environment: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection=Server=sql,1433;Database=KitchenAssistant;User ID=sa;Password=${MSSQL_SA_PASSWORD};Encrypt=True;TrustServerCertificate=True`, and `ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}`.
  - Port `8090:8080`.

Steps:
- [x] 🤖 (auto) `scripts/local-prod.ps1` created `.env` with a random SA password (not printed). 👤 Add `ANTHROPIC_API_KEY` later. Original step: copy `.env.example` to `.env` and fill in a strong SA password (8+ characters, with upper, lower, digit and symbol) plus the dev Anthropic key.
- [x] 🤖 Build the image: `dotnet publish KitchenAssistant.csproj -c Release --os linux --arch x64 /t:PublishContainer -p:ContainerImageTag=local`
- [x] 🤖 `docker compose up -d`. Then `docker compose ps` should show `sql` healthy and `app` running.
- [x] 🤖 `docker compose logs app` should show migrations applied (the `KitchenAssistant` DB was created), `Now listening on: http://[::]:8080`, and no Anthropic warning.
- [x] 🤖 Verify at http://localhost:8090 in the built-in browser:
  - `/healthz` returns `Healthy`.
  - **Register logs in directly** with no confirmation page, which shows the Production config is active.
  - The Counter page is interactive, and DevTools → Network → WS shows `_blazor` with **101**.
- [x] 🤖 **Restart test:** `docker compose restart app`. You should still be logged in (Data Protection keys in SQL).
- [x] 🤖 **Cold-start test:** `docker compose down`, then `up -d`. Data should persist through the volume, and the user can still log in.
- [x] 🤖 **Clean-DB test:** `scripts/local-prod.ps1 -Reset`. Migrations should build the schema from scratch. This is exactly what the first Azure deploy does.

Known differences from Azure (so nothing here gives false confidence):
- The stack runs plain HTTP on 8090, with no TLS terminator or forwarded headers. `UseHttpsRedirection` logs "Failed to determine the https port" and doesn't redirect. That's expected here; on Azure it's covered by `ASPNETCORE_FORWARDEDHEADERS_ENABLED`.
- `TrustServerCertificate=True` is only here because the SQL container uses a self-signed cert. Azure uses `False`.
- It uses `sa`. Azure uses the SQL admin login (and later a managed identity).
- SQL Server 2022 is not the Azure SQL engine: mostly the same T-SQL surface, but different defaults and no DTU throttling.

| Edge case | Symptom | Fix |
|---|---|---|
| Weak SA password | `sql` container exits right away; log says `password does not meet SQL Server password policy` | Use a stronger password in `.env`, then `scripts/local-prod.ps1 -Reset`. |
| SQL slow on first boot | `app` stays in "waiting", or the migration hits a timeout | The healthcheck gate plus `EnableRetryOnFailure` should absorb it. If not, run `docker compose up -d app` again once `sql` is healthy. |
| Image pull blocked | `docker pull mcr.microsoft.com/...` times out (corporate proxy/VPN) | Set the proxy under Rancher Desktop → Preferences → Proxy, or pull off VPN. |
| `PublishContainer` can't reach Docker | `CONTAINER1002` / "docker not found" | Rancher Desktop must use the **dockerd (moby)** engine, not containerd, and be running. Check `docker info`. |
| Port 8090 or 14330 in use | `Bind for 0.0.0.0:8090 failed` | Change the host port in `compose.yaml`. |
| Stale image | Code changes don't show up | Rebuild with the `dotnet publish … /t:PublishContainer` step, then `docker compose up -d --force-recreate app`. The script does both. |
| Logged out after `-Reset` | Cookie can't be decrypted: new DB, new keys | Expected. Clear the localhost:8090 cookies and log in again. |
| Env var not picked up | Anthropic warning in the logs even though `.env` is set | `.env` must sit next to `compose.yaml`, with no quotes around values. Check with `docker compose config` (**this prints secrets, so run it yourself, not the agent**). |

---

## L4: Hand-off & docs (🤖)

- [x] Add a "Local run" block to `CLAUDE.md`, outside the 10x-cli markers: the dev-loop commands, `scripts/local-prod.ps1` usage, and the ports (7020/5180 for dev, 8090 for the prod-like stack, 14330 for container SQL).
- [x] In `context/changes/deployment/deployment-plan.md`, mark **Phase 2 ✅** (done via local L1) and note that `global.json`, the tool manifest and the Data Protection migration already exist.
- [x] Tick the phase tracker in this file.
- [x] Committed on `local-dev-setup` as `f56e334`.

## Critical files

- Modified: `Program.cs`, `Data/ApplicationDbContext.cs`, `KitchenAssistant.csproj`, `CLAUDE.md`, `context/changes/deployment/deployment-plan.md`
- New: `Data/Migrations/*_AddDataProtectionKeys.cs`, `appsettings.Production.json`, `global.json`, `dotnet-tools.json`, `compose.yaml`, `.claude/launch.json`, `.env.example`, `scripts/local-prod.ps1`

## Done when

1. The dev loop works: https://localhost:7020, register with the confirmation link, login, and login survives a restart.
2. The prod-like stack works: http://localhost:8090, register without confirmation, WS 101, login survives `restart` and `down`/`up`, and the schema builds from an empty DB.
3. `has-pending-model-changes` is clean, and there are no vulnerable packages.

## Results (2026-09-29)

| Check | Dev loop (LocalDB, 7020) | Prod-like stack (Docker, 8090) |
|---|---|---|
| `/healthz` | ✅ `Healthy` | ✅ `Healthy` |
| Register | ✅ confirmation-link page (Development) | ✅ logged in directly (Production config) |
| InteractiveServer circuit | ✅ WebSocket `wss://localhost:7020/_blazor`, counter increments | ✅ WebSocket `ws://localhost:8090/_blazor`, counter increments |
| Login survives app restart | ✅ | ✅ `docker compose restart app` |
| Data survives stack down/up | n/a | ✅ volume `kitchen-assistant_sqldata` |
| Schema from empty DB | n/a | ✅ `-Reset`: `CREATE DATABASE` + both migrations at startup |
| Old cookie after `-Reset` | n/a | ✅ handled gracefully: redirect to login, no error page |
| `has-pending-model-changes` / vulnerable packages | ✅ clean / ✅ none | same image |

Expected startup warnings (none of them block anything):
- `ANTHROPIC_API_KEY is not configured`: goes away once you set the key (user-secrets for dev, `.env` for Docker).
- `No XML encryptor configured … persisted to storage in unencrypted form`: Data Protection keys sit in the DB unencrypted. That's acceptable for the MVP, since Azure SQL encrypts at rest with TDE. Hardening option for later: `ProtectKeysWithCertificate` or Key Vault.
- `Failed to determine the https port for redirect` (Docker only): the stack is plain HTTP. On Azure, TLS termination plus `ASPNETCORE_FORWARDEDHEADERS_ENABLED` covers it.

Test accounts: throwaway `*@kitchen.test` users exist only in the local DBs (LocalDB and the Docker volume). Wipe them with `dotnet ef database drop -f && dotnet ef database update` or `scripts/local-prod.ps1 -Reset`.
