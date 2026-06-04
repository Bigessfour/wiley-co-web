import { defineConfig, devices } from "@playwright/test";

const normalizeBaseURL = (value: string) => value.replace(/\/$/, "");
const isCI = process.env.CI === "true";

/**
 * Blazor WASM resolves the workspace API as same-host + port+1 (see ClientStartup.ResolveLocalApiBaseAddress).
 * CI starts Kestrel on 127.0.0.1:5231 only. On Linux, `localhost` often resolves to ::1 first, so
 * `http://localhost:5230` → `http://localhost:5231` misses the IPv4 listener and the shell never loads.
 */
const loopbackClientBaseURL = "http://127.0.0.1:5230";
const defaultLocalBaseURL = isCI
  ? loopbackClientBaseURL
  : "http://localhost:5230";
const defaultLocalApiURL = "http://127.0.0.1:5231";

/** CI: comma/space-separated subset, e.g. `chromium` or `chromium,webkit`. Default: both. */
function ciBrowserProjectNames(): ("chromium" | "webkit")[] {
  const raw = process.env.PLAYWRIGHT_CI_BROWSERS?.trim();
  if (!raw) {
    return ["chromium", "webkit"];
  }
  const parts = raw
    .split(/[\s,]+/)
    .map((s) => s.toLowerCase())
    .filter(Boolean);
  const out: ("chromium" | "webkit")[] = [];
  if (parts.includes("chromium")) {
    out.push("chromium");
  }
  if (parts.includes("webkit")) {
    out.push("webkit");
  }
  return out.length > 0 ? out : ["chromium"];
}

function browserProjects() {
  const names = isCI
    ? ciBrowserProjectNames()
    : (["chromium", "webkit"] as const);
  return names.map((name) =>
    name === "chromium"
      ? {
          name: "chromium",
          use: { ...devices["Desktop Chrome"] },
        }
      : {
          name: "webkit",
          use: { ...devices["Desktop Safari"] },
        },
  );
}
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
        timeout: 180_000,
        env: {
          ASPNETCORE_ENVIRONMENT: "Development",
        },
      },
      {
        command: `node ./Scripts/serve-wwwroot.mjs --root ${ciWwwroot} --port 5230`,
        url: defaultLocalBaseURL,
        reuseExistingServer: true,
        timeout: 120_000,
      },
    ]
  : useManagedWebServer
    ? [
        {
          command:
            "dotnet run --project WileyCoWeb.Api/WileyCoWeb.Api.csproj --no-build --configuration Debug --launch-profile WileyCoWeb.Api --urls http://127.0.0.1:5231",
          url: `${defaultLocalApiURL}/health`,
          reuseExistingServer: false,
          timeout: 180_000,
          env: {
            ASPNETCORE_ENVIRONMENT: "Development",
          },
        },
        {
          command:
            "dotnet run --project WileyCoWeb.csproj --no-build --configuration Debug --launch-profile http --urls http://localhost:5230 -p:DebugType=none -p:DebugSymbols=false",
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
  /** CI runs one worker; avoid extra scheduling overhead on long serial suites. */
  fullyParallel: !isCI,
  forbidOnly: !!process.env.CI,
  /** One retry in CI keeps wall time bounded; local default is no retry. */
  retries: isCI ? 1 : 0,
  workers: process.env.CI || useManagedWebServer ? 1 : undefined,
  /** Stop runaway jobs before the GitHub job timeout (45m) with little headroom. */
  globalTimeout: isCI ? 38 * 60 * 1000 : undefined,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report" }],
    ["json", { outputFile: "playwright-report/results.json" }],
  ],
  use: {
    baseURL: finalBaseURL,
    /** `trace: on` for every test was dominating CI wall time (I/O + zip). */
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },
  webServer: finalWebServer,
  projects: browserProjects(),
});
