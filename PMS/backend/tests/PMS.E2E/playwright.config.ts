import { defineConfig, devices } from '@playwright/test';

/**
 * The suite runs against a PMS.Api instance that is already serving the built React bundle
 * (same-origin, as the section 2 cookie-auth decision requires). Start it with:
 *
 *   cd PMS/frontend && npm run build
 *   cd PMS/backend/src/PMS.Api && dotnet run
 *
 * and point PMS_E2E_BASE_URL at it if it is not on the default dev URL.
 */
const baseURL = process.env.PMS_E2E_BASE_URL ?? 'https://localhost:7191';

export default defineConfig({
  testDir: './specs',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? 'line' : [['html', { open: 'never' }]],
  use: {
    baseURL,
    // The ASP.NET Core development certificate is self-signed.
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    // Chrome, Edge and Safari are the BRD's stated browser targets (Doc_BRD.md,
    // Compatibility). All three are covered here rather than assuming Chromium stands in
    // for the other two - the printed prescription is where they diverge most (C-47).
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'edge', use: { ...devices['Desktop Edge'], channel: 'msedge' } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } },
  ],
});
