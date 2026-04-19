import { defineConfig, devices } from "@playwright/test";

const defaultLocalBaseURL = "http://localhost:5230";
const normalizeBaseURL = (value: string) => value.replace(/\/$/, "");
const baseURL = normalizeBaseURL(
  process.env.WILEYCO_E2E_BASE_URL ?? defaultLocalBaseURL,
);
const useManagedWebServer = baseURL === defaultLocalBaseURL;

export default defineConfig({
  testDir: "./tests/playwright",
  timeout: 60_000,
  expect: {
    timeout: 15_000,
  },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report" }],
    ["json", { outputFile: "playwright-report/results.json" }],
  ],
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  webServer: useManagedWebServer
    ? {
        command:
          "dotnet run --project WileyCoWeb.csproj --no-launch-profile --urls http://localhost:5230",
        url: defaultLocalBaseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 180_000,
        env: {
          ASPNETCORE_ENVIRONMENT: "Development",
        },
      }
    : undefined,
  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
      },
    },
  ],
});
