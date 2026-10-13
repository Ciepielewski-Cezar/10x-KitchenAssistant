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
