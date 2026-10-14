import { existsSync, readFileSync } from 'node:fs';
import { parseEnv } from 'node:util';
import { defineConfig, devices } from '@playwright/test';

// Post-deploy smoke: runs against an already-running app (the deployed site, or the local prod-like stack at
// http://localhost:8090), so there is no webServer. Only SMOKE_ variables are taken from .env, because that file
// also holds the stack's secrets. In CI the file is absent and the variables come from the job.
if (existsSync('.env')) {
  for (const [name, value] of Object.entries(parseEnv(readFileSync('.env', 'utf8')))) {
    if (name.startsWith('SMOKE_')) process.env[name] ??= value;
  }
}

const baseURL = process.env.SMOKE_BASE_URL;
if (!baseURL) {
  throw new Error('Set SMOKE_BASE_URL (see context/foundation/test-stack.md, ## Smoke)');
}

export default defineConfig({
  testDir: './tests/smoke',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  // Generation has its own 60 s deadline, so a test needs more than the 30 s default.
  timeout: 120_000,
  reporter: 'html',
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    // No trace for the sign-in: it would record the smoke password and session cookie in test-results.
    { name: 'smoke-setup', testMatch: /.*\.setup\.ts/, use: { trace: 'off' } },
    {
      name: 'smoke',
      testMatch: /.*\.smoke\.spec\.ts/,
      use: { ...devices['Desktop Chrome'], storageState: 'playwright/.auth/smoke.json' },
      dependencies: ['smoke-setup'],
    },
  ],
});
