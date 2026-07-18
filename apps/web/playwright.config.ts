import { defineConfig, devices } from "@playwright/test";

// Pré-condição: a API (porta 8080) e o banco precisam estar de pé — use o stack
// de dev (`docker compose up db api`) ou o modo híbrido. O Playwright sobe o
// `next dev` sozinho via `webServer`.
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: "list",
  use: {
    baseURL: "http://localhost:3000",
    trace: "on-first-retry",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],
  webServer: {
    command: "npm run dev",
    url: "http://localhost:3000",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
