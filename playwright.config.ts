import { existsSync, readFileSync } from 'node:fs';
import { parseEnv } from 'node:util';
import { defineConfig, devices } from '@playwright/test';

// The test user (E2E_USERNAME, E2E_PASSWORD) lives in the gitignored .env. That file also holds the local
// prod-like stack's secrets (compose.yaml), so take only the E2E_ variables: nothing else reaches the app under
// test. In CI the file is absent and the variables come from the job.
if (existsSync('.env')) {
  for (const [name, value] of Object.entries(parseEnv(readFileSync('.env', 'utf8')))) {
    if (name.startsWith('E2E_')) process.env[name] ??= value;
  }
}

// 5180 was detected from Properties/launchSettings.json (the "http" profile's applicationUrl).
// E2E_PORT overrides it when that port is taken on this machine.
const PORT = Number(process.env.E2E_PORT ?? 5180);
const baseURL = `http://localhost:${PORT}`;

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'setup', testMatch: /.*\.setup\.ts/ },
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], storageState: 'playwright/.auth/user.json' },
      dependencies: ['setup'],
    },
  ],
  webServer: {
    // Build, then run that build on the port above. Development, because the Fake recipe generator (no AI cost)
    // is allowed only there; the database is LocalDB from appsettings.json.
    command: `dotnet build KitchenAssistant.csproj && dotnet run --project KitchenAssistant.csproj --no-build --no-launch-profile --urls ${baseURL}`,
    url: baseURL,
    env: { ASPNETCORE_ENVIRONMENT: 'Development' },
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
  },
});
