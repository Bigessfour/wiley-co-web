import os from "node:os";

import { defineConfig, devices } from "@playwright/test";

const defaultLocalBaseURL = "http://localhost:5230";
const defaultLocalApiURL = "http://127.0.0.1:5231";
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
  workers: (() => {
    const raw = process.env.PLAYWRIGHT_WORKERS;
    if (raw != null && raw !== "") {
      const parsed = Number.parseInt(raw, 10);
      if (Number.isFinite(parsed) && parsed > 0) {
        return parsed;
      }
      return 1;
    }
    // CI: keep a single worker unless PLAYWRIGHT_WORKERS is set (workflow sets 2).
    if (process.env.CI) {
      return 1;
    }
    // Local managed API + Blazor: default to several workers so full runs finish
    // in reasonable time; override with PLAYWRIGHT_WORKERS if needed.
    if (useManagedWebServer) {
      const cpus = os.availableParallelism?.() ?? os.cpus().length;
      return Math.min(Math.max(cpus, 2), 8);
    }
    return undefined;
  })(),
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
    ? [
        {
          command:
            "dotnet run --project WileyCoWeb.Api/WileyCoWeb.Api.csproj --launch-profile WileyCoWeb.Api --urls http://127.0.0.1:5231",
          url: `${defaultLocalApiURL}/health`,
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          env: {
            ASPNETCORE_ENVIRONMENT: "Development",
          },
        },
        {
          command:
            "dotnet run --project WileyCoWeb.csproj --launch-profile http --urls http://localhost:5230",
          url: defaultLocalBaseURL,
          reuseExistingServer: !process.env.CI,
          timeout: 180_000,
          env: {
            ASPNETCORE_ENVIRONMENT: "Development",
            WILEY_WORKSPACE_API_BASE_ADDRESS: defaultLocalApiURL,
          },
        },
      ]
    : undefined,
  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
      },
    },
    {
      name: "webkit",
      use: {
        ...devices["Desktop Safari"],
      },
    },
  ],
});
