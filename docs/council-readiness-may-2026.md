# Council readiness (May 2026)

This runbook turns static “looks stuck” observations into **checklist evidence** for the Wiley Widget workspace (`wileywidget.townofwiley.gov` and Amplify hosts).

## 1. Prove Blazor vs static HTML vs API

- The first screen is **static HTML** in `wwwroot/index.html` (spinner + “Starting Wiley Widget”). It disappears when Blazor replaces `#app`; it is **not** a live Aurora sync meter.
- **If the static screen never clears:** check Network for `blazor.webassembly.js`, `dotnet.*.js`, `.wasm`, and `.dll` (404, blocked CDN, or CSP). Check Console for startup exceptions.
- **If the workspace shell loads but data looks wrong:** check Network for `GET …/api/workspace/snapshot` (or the App Runner base configured at build time). Status appears in the hero **Data source** strip and in **Hosting Information** / **Startup source** in the sidebar.
- **Automated smoke:** from repo root, `npm run playwright:test:hosted` runs Playwright against production hosts and writes JSON + traces under `TestResults/playwright-hosted/` (gitignored). Traces default to `--trace on`; set `WILEYCO_PLAYWRIGHT_TRACE=off` to skip. Install browsers once: `npx playwright install chromium webkit`. QuickBooks routing real-flow specs skip automatically when the base URL is not localhost (they need local API write access).
- **Single-host smoke (PowerShell):** `$env:WILEYCO_E2E_BASE_URL = 'https://wileywidget.townofwiley.gov'; npx playwright test tests/playwright/workspace-smoke.spec.ts --project=chromium`

## 2. Cross-check AWS and repo alignment

| Item                            | Where to verify                                                                                                                                                                                                           |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Syncfusion key in client bundle | Amplify build must succeed (`amplify.yml` fails if `SYNCFUSION_LICENSE_KEY` is missing). Published site should serve `appsettings.Syncfusion.local.json` (CI checks HTTP 200 in `.github/workflows/ci.yml`).              |
| Workspace API base URL          | Amplify branch env `WILEY_WORKSPACE_API_BASE_ADDRESS` (see `README.md`) → generates `wwwroot/appsettings.Workspace.local.json` at build time. Client `HttpClient` uses that base; relative paths are `api/workspace/...`. |
| CORS                            | `WileyCoWeb.Api/appsettings.json` and `apprunner-update.json` list Amplify + `wileywidget.townofwiley.gov` origins. Confirm **App Runner** effective env still includes `WorkspaceClientOrigins` after any console edits. |
| API health                      | `GET https://<app-runner-host>/health` (see `README.md` for current URL). CloudWatch: `WileyWidget.Startup.RuntimeBaseline`, snapshot errors, DB connection failures.                                                     |
| Grok / xAI                      | Runtime secret on App Runner (`XAI_API_KEY`); Jarvis shows fallback copy when the server cannot reach xAI (see `WorkspaceAiAssistantService`).                                                                            |

## 3. Council demo fallback (presentation day)

- **Automatic:** `WorkspaceBootstrapService` falls back to `wwwroot/data/workspace-bootstrap.json` when the snapshot API fails, then to a generated minimal state.
- **Manual:** In the workspace sidebar **Global Actions**, use **Load council demo data** to re-apply the bundled JSON without waiting on API errors.

## 4. Math credibility (one worked example)

Bundled sample (`wwwroot/data/workspace-bootstrap.json`):

- Total costs **$412,500**, projected volume **14,500** units → break-even rate = `412500 / 14500` ≈ **$28.4483**/unit (UI rounds for display). Scenario line items add **$252,000** → adjusted costs **$664,500** → **$45.8276**/unit at the same volume.

Client formulas live in `State/RateCalculator.cs`; server-side knowledge uses the same revenue-requirement style division in `WorkspaceKnowledgeService`. This is **revenue requirement + simplified rate design**, not a full AWWA M1 cost-of-service class allocation (which needs class usage, peaking, and cost pools).

## 5. Talking points for elected officials

- The widget is **decision-support and transparency** on real imported ledger data and saved scenarios.
- For large structural rate changes or Prop 218–style filings, position consultant-grade COS studies as the formal complement; this tool supports **scenario communication** and **what-if** clarity.
