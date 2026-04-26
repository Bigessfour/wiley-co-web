import { defineConfig, devices } from "@playwright/test";

const defaultLocalBaseURL = "http://localhost:5230";
const defaultLocalApiURL = "http://127.0.0.1:5231";
const normalizeBaseURL = (value: string) => value.replace(/\/$/, "");
const isCI = process.env.CI === "true";
const ciWwwroot = "./publish_output/wwwroot";
const ciApiDll = "./api_output/WileyCoWeb.Api.dll";

const finalBaseURL = isCI
  ? normalizeBaseURL(process.env.WILEYCO_E2E_BASE_URL ?? defaultLocalBaseURL)
  : normalizeBaseURL(process.env.WILEYCO_E2E_BASE_URL ?? defaultLocalBaseURL);

const useManagedWebServer = !isCI && finalBaseURL === defaultLocalBaseURL;
const finalWebServer = isCI
  ? [
      {
        command:
          'bash -lc "cd ./api_output && dotnet ./WileyCoWeb.Api.dll --urls http://127.0.0.1:5231"',
        url: `${defaultLocalApiURL}/health`,
        reuseExistingServer: true,
        timeout: 120_000,
        env: {
          ASPNETCORE_ENVIRONMENT: "Development",
        },
      },
      {
        command: `node ./Scripts/serve-wwwroot.mjs --root ${ciWwwroot} --port 5230`,
        url: defaultLocalBaseURL,
        reuseExistingServer: true,
        timeout: 60_000,
      },
    ]
  : useManagedWebServer
    ? [
        {
          command:
            "dotnet run --project WileyCoWeb.Api/WileyCoWeb.Api.csproj --no-build --configuration Release --launch-profile WileyCoWeb.Api --urls http://127.0.0.1:5231",
          url: `${defaultLocalApiURL}/health`,
          reuseExistingServer: false,
          timeout: 180_000,
          env: {
            ASPNETCORE_ENVIRONMENT: "Development",
          },
        },
        {
          command:
            "dotnet run --project WileyCoWeb.csproj --no-build --configuration Release --launch-profile http --urls http://localhost:5230 -p:DebugType=none -p:DebugSymbols=false",
          url: defaultLocalBaseURL,
          reuseExistingServer: false,
          timeout: 180_000,
          env: {
            ASPNETCORE_ENVIRONMENT: "Development",
            WILEY_WORKSPACE_API_BASE_ADDRESS: defaultLocalApiURL,
          },
        },
      ]
    : undefined;

export default defineConfig({
  testDir: "./tests/playwright",
  timeout: 60_000,
  expect: {
    timeout: 15_000,
  },
  globalSetup: "./tests/playwright/global-setup.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI || useManagedWebServer ? 1 : undefined,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report" }],
    ["json", { outputFile: "playwright-report/results.json" }],
  ],
  use: {
    baseURL: finalBaseURL,
    trace: isCI ? "on" : "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  webServer: finalWebServer,
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
