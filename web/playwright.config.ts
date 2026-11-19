import { defineConfig, devices } from '@playwright/test';

/**
 * Two targets:
 * - default: the Vite dev server with MSW mocks (started automatically, or reuses :5173).
 * - E2E_TARGET=compose: the docker compose stack at http://localhost:8080 (start it yourself);
 *   E2E_BASE_URL points it elsewhere when another stack already holds that port.
 */
const compose = process.env.E2E_TARGET === 'compose';
const composeUrl = process.env.E2E_BASE_URL ?? 'http://localhost:8080';

export default defineConfig({
  testDir: './e2e',
  timeout: 45_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list']],
  use: {
    baseURL: compose ? composeUrl : 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1366, height: 900 } } },
    { name: 'mobile', use: { ...devices['Pixel 7'] } },
  ],
  webServer: compose
    ? undefined
    : {
        command: 'npm run dev',
        url: 'http://localhost:5173',
        reuseExistingServer: true,
        timeout: 90_000,
      },
});
