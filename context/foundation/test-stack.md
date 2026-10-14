# Test stack

## E2E

<!-- Written by /10x-e2e-setup. Re-run it to change this section; other skills only read it. -->

- runner: Playwright Test, @playwright/test 1.64.0
- config: playwright.config.ts
- single-spec command: npx playwright test tests/e2e/<name>.spec.ts
- full-suite command: npx playwright test
- base URL: http://localhost:5180
- port: 5180 (detected from Properties/launchSettings.json, "http" profile applicationUrl; detected default 5180, override with E2E_PORT)
- web server command: dotnet build KitchenAssistant.csproj && dotnet run --project KitchenAssistant.csproj --no-build --no-launch-profile --urls http://localhost:$E2E_PORT (ASPNETCORE_ENVIRONMENT=Development: Fake recipe generator, LocalDB); reuseExistingServer outside CI
- auth setup project: setup (tests/e2e/auth.setup.ts), credentials from E2E_USERNAME / E2E_PASSWORD in .env (the config imports only the E2E_ variables from it)
- storageState: playwright/.auth/user.json (gitignored)
- seed: tests/e2e/seed.spec.ts — protects risk #3 (the interactive page cannot connect, so the user's action is lost): a product added over the live interactive connection persists after page reload. Interactive (InteractiveServer) pages need `waitForInteractive` from tests/e2e/support/blazor.ts before a non-idempotent action.
- browser CLI: playwright-cli, command skill at .claude/skills/playwright-cli/SKILL.md
- updated: 2026-10-12

## Smoke

- purpose: post-deploy check beyond liveness (test-plan risk #3): a signed-in page, the interactive connection and a real AI generation
- config: playwright.smoke.config.ts (no webServer; runs against an already-running app)
- command: npm run smoke (npx playwright test -c playwright.smoke.config.ts)
- variables: SMOKE_BASE_URL (required), SMOKE_USERNAME, SMOKE_PASSWORD; read from the environment or, for SMOKE_ names only, from .env
- projects: smoke-setup (tests/smoke/smoke.setup.ts: logs in, or registers the account on a fresh environment), then smoke (tests/smoke/deployed-app.smoke.spec.ts)
- storageState: playwright/.auth/smoke.json (gitignored)
- local run: ./scripts/local-prod.ps1 -Reset (empty database, startup migration), then SMOKE_BASE_URL=http://localhost:8090 npm run smoke. Green needs a valid ANTHROPIC_API_KEY in .env; with an empty key the generation test fails by design
- cost: one real Anthropic call per run, bounded by Recipes:MaxTokens and the 60 s deadline
- CI: not wired yet; F-01 (change `deployment`) adds it to deploy.yml with SMOKE_ secrets
- updated: 2026-10-14
