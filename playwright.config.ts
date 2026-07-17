import { defineConfig, devices } from '@playwright/test';

const isCI = !!process.env.CI;
const requestedPort = Number(process.env.PLAYWRIGHT_PORT ?? 3000);
const testPort = Number.isInteger(requestedPort) && requestedPort >= 1 && requestedPort <= 65_535
  ? requestedPort
  : 3000;
const testUrl = `http://127.0.0.1:${testPort}`;

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  retries: isCI ? 2 : 0,
  reporter: [['html', { open: 'never' }], ['list']],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? testUrl,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ],
  webServer: {
    command: `corepack pnpm dev --hostname 127.0.0.1 --port ${testPort}`,
    url: testUrl,
    reuseExistingServer: !isCI,
    timeout: 120_000
  }
});
