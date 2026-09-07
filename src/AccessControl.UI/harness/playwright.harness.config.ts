import { defineConfig, devices } from '@playwright/test';

const uiBaseUrl = process.env.UI_URL || 'http://127.0.0.1:8150';

export default defineConfig({
  testDir: './specs',
  outputDir: './output',
  fullyParallel: false, // Run flows sequentially for deterministic state validation
  retries: 0,
  workers: 1,
  timeout: 30000,
  reporter: [
    ['list'],
    ['html', { outputFolder: './output/html-report', open: 'never' }]
  ],
  use: {
    baseURL: uiBaseUrl,
    screenshot: 'on',
    video: 'off',
    trace: 'on-first-retry',
    viewport: { width: 1280, height: 800 },
  },
  projects: [
    {
      name: 'harness-chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
