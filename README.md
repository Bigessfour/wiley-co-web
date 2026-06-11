# wiley-co-web

Wiley Widget — pure local Blazor WASM + ASP.NET Core API

**Core mission (import → categorize → allocate overhead → analyze → viz → Jarvis):**

Import everyday QuickBooks transactions (CSV/XLSX). Auto-categorize into exactly 4 enterprises (Water, Sewer/WSD, Trash, Apartments). Apply configurable global Town overhead % + management + labor. Compute per-enterprise net contribution and "holds its own" vs "vampire-feeding" flags. Stunning Syncfusion dashboards (4-quadrant break-even, health cards, interactive overhead sliders, gauges, grids). Jarvis (xAI Grok) answers council planning questions grounded in the live workspace knowledge.

Everything runs locally: `http://localhost:5230` (client) + `http://localhost:5231` (API). PostgreSQL via `docker-compose.dev.yml`. No AWS, Amplify, App Runner, Aurora, or cloud deployment in this repo.

## Local Development (the only supported path)

Use the single-command launcher (recommended):

```bash
./start-local.sh
```

- Starts docker postgres (real DB, no degraded mode).
- Starts API + Blazor client.
- Waits for health on 5230/5231.
- Use `./stop-local.sh` to clean ports/PIDs.

xAI Grok key (for Jarvis): when missing, the Decision Support / Jarvis rail shows an in-app prompt. Enter it in browser; it is saved only to gitignored `WileyCoWeb.Api/appsettings.Development.local.json` (or user secrets). Never committed.

Syncfusion license: use the keychain helper `Scripts/syncfusion-license-keychain.sh` (or env / local json). See `AGENTS.md` for MCP Syncfusion assistant.

See also:

- `docs/quickbooks-desktop-import-guide.md` — clerk operating procedure for QB Desktop exports.
- `docs/wiley-widget-functional-breakdown.md`
- `docs/playwright-ui-test-strategy.md`
- `docs/post-production-operations-handbook.md` (includes `/api/ai/health`).

High-risk gates (run before PRs):

```bash
dotnet test ... --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

## QuickBooks Desktop Import (core entry point)

The QuickBooks Import panel (and wrapper) + `QuickBooksCsvParser`/`QuickBooksExcelParser` + `QuickBooksImportService` + `QuickBooksRoutingService` (rules + AllocationProfiles) deliver the import → categorize → allocate flow.

Duplicate guard key is `(canonical enterprise, file hash)`.

Overhead application and net/vampire math live in `EnterpriseRateService` (static) + `WorkspaceKnowledgeService` + `EnterpriseLedgerCostService`.

All four enterprises are seeded in `WorkspaceEnterpriseCatalog` / `WorkspaceEnterpriseSeedCatalog`.

## Workspace Knowledge + Jarvis (analyze + AI)

`/api/workspace/knowledge` + `WorkspaceKnowledgeService` supply DirectCosts, OverheadBurden (using AppSettings.TownOverheadPercent + mgmt/labor), NetContribution, HoldsItsOwn, VampireImpact for the viz panels and for grounding Jarvis.

Jarvis (`WorkspaceAiAssistantService`, `JARVISPersonalityService`, `GrokSupercomputer`) uses xAI direct + the knowledge context. Routes under `/api/ai/*`.

## Data Viz (Syncfusion)

- `DataDashboardPanel`: 4 enterprise cards with dynamic "Holds its own" / "Vampire-feeding" badges, net contrib, burdens, impacts; live SfNumericTextBox sliders for overhead %.
- `BreakEvenPanel` + `ApartmentConfigPanel` + `RatesPanel`: 4-quadrant analysis, gauges, grids, baseline save.
- All via the WileyWorkspace shell + WorkspaceState.

Customer export (canonical): `#export-customers-excel-button` → XlsIO `ExportCustomerDirectoryAsync` (not SfGrid toolbar).

## Observability (local only)

File logs via `LogPathResolver` (container-aware for docker). OpenTelemetry / SigNoz hooks exist but are non-mandatory for the core loop.

## Non-goals / culled

All AWS/Amplify/AppRunner/Aurora/Cognito/IAM/AppRunner scripts, docs, configs, and references have been surgically removed (see git history on this branch). Non-participating panels (affordability, debt-coverage, capital-gap, scenario-planner, reserve-trajectory, trends, budget-dashboard, customer-viewer as full panel) and their supporting contracts/services remain in the tree only if still referenced by the main workspace switch; further UI pruning is follow-on work to keep core loop pristine. WileyWidget.Business layer retained only where still depended on by core Jarvis/knowledge paths.

This keeps the repo smaller, faster to clone/start, and 100% focused on the 5 core processes.

## Database (local Docker or native Postgres)

The API uses EF Core + Npgsql against PostgreSQL (pinned dialect compatibility).

- Preferred: `docker-compose.dev.yml` (postgres:16-alpine) — started automatically by `start-local.sh`.
- Alternative: native Postgres + gitignored `WileyCoWeb.Api/appsettings.Development.local.json` override (ConnectionStrings + Database:AllowDegradedStartup=false).

No cloud DB, no Aurora, no reset scripts in the tree. The migration history under `src/WileyWidget.Data/Migrations` is the source of truth for the import/ledger/enterprise tables used by the core loop.

- Opens browser to the workspace.
- Prints instructions: **provide your xAI key via the in-app prompt in the Decision Support / Jarvis panel** (the method kept per request; secure, writes only to gitignored local file; may require one API restart after first submit for full effect).

Companion:

```bash
./stop-local.sh
```

**In VS Code / Cursor:**

- Use the task **"🚀 Start Local Full Stack (recommended - single script)"** (added to `.vscode/tasks.json`).
- Use **"🛑 Stop Local Full Stack"** for cleanup.

The old manual terminal steps and parallel tasks remain for advanced use, but `./start-local.sh` (or the IDE task) is now the supported, single-invocation way to get a running stack for UI evaluation.

**After start (or via the script):**

- UI: http://localhost:5230/wiley-workspace
- Open **Decision Support** panel.
- Use the xAI key prompt (or the persistent "Dev: xAI Key (local only)" section we added to the panel).
- Real Postgres + (after key) real Jarvis.
- Syncfusion keys assumed pre-configured (as you did with the keychain/launchctl flow).

See the full "Local Full-Stack Development" section for Docker vs native details. This, plus the prior DB, key (UI prompt), and Syncfusion fixes, gives a complete local runtime without leaving startup as a manual/undeveloped process. Run `./start-local.sh` (with Docker up) to test. Use the kill task or `./stop-local.sh` to clean ports.

**Tips for completion / no undeveloped paths:**

- Real DB + real key = full non-degraded experience (real data paths, live AI).
- The dev key flow, DB connection, and prompt are fully wired (no stubs).
- For future clerk deployments: they will use the same native Postgres + the in-app prompt (or env/user-secrets) + the existing production key injection.
- After your evaluation, we can close any remaining loops (e.g. better hot-reload of key without restart, one-command helper script, docs polish).

See the local development section above for the current DB + key story (all AWS/Aurora/sync scripts and docs have been removed in the cull).

The QuickBooks import and workspace knowledge sections below are the current canonical descriptions.

## Workspace Knowledge Layer (core for analyze + Jarvis grounding)

The server-side knowledge layer is the shared calculation surface for Decision Support and Jarvis.

- `IWorkspaceKnowledgeService` and `WorkspaceKnowledgeService` build live financial guidance (DirectCosts, OverheadBurden using the 4 % sliders from AppSettings, NetContribution, HoldsItsOwn, VampireImpact) from the selected enterprise, fiscal year, current rate, costs, projected volume.
- `WileyCoWeb.Api` exposes that analysis at `/api/workspace/knowledge`.
- The Decision Support rail + UserContextPlugin now use the same server-backed knowledge so Jarvis answers are grounded in the import → overhead → net contrib numbers.
- See recent enhancements in `EnterpriseRateService.Calculate*` and `DataDashboardPanel` for the council 4-card viz.

Maintainer rule: do not reintroduce client-only financial heuristics or canned AI rationale where live knowledge is expected. Extend the knowledge service + thin API contract.

## Post-Production Operations (local focus)

- Primary handbook: [docs/post-production-operations-handbook.md](docs/post-production-operations-handbook.md) (covers `/api/ai/health`, local startup, key rotation via in-app prompt).
- Use [docs/quickbooks-desktop-import-guide.md](docs/quickbooks-desktop-import-guide.md) for import ops.
- Playwright strategy and functional breakdown live in docs/.
- All AWS-specific runbooks, closure plans, aurora reset docs, and iam/ have been deleted in this cull phase.

## Cursor / VS Code debugging (Blazor + API)

- Launch configurations live in [`.vscode/launch.json`](.vscode/launch.json) (e.g. **Debug Full Stack (Client + API)** on ports `5230` / `5231`).
- Step-by-step and agent playbook: [docs/debugging-wiley-widget-cursor.md](docs/debugging-wiley-widget-cursor.md).

## Playwright E2E (core local + CI)

`playwright.config.ts` + `package.json` scripts drive the suites.

- `npm run playwright:test:ci:highrisk` (and `npm run playwright:test:ci`) are the gates. They exercise the import/duplicate, break-even 4-quad, decision-support/Jarvis, customer export paths that protect the core loop.
- Use `npx playwright test --ui` or codegen for interactive.
- Docker proof script exists for parity with CI container.
- MCP playwright server is registered for agent/browser automation (allowed per updated AGENTS.md).

The primary target is always the local `http://localhost:5230/wiley-workspace`. Hosted targets are secondary and were tied to now-deleted Amplify hosting.

## Local Secrets On macOS (xAI + Syncfusion)

Use `.NET User Secrets` + keychain scripts:

- `dotnet user-secrets ... --project WileyCoWeb.csproj`
- `./Scripts/syncfusion-license-keychain.sh`
- `./Scripts/syncfusion-api-keychain.sh` (for the MCP assistant key)
- `./Scripts/mcp-sf-blazor-assistant.sh` to launch the Syncfusion Blazor MCP with key from keychain.

xAI key for Jarvis is intentionally local-dev only (in-app prompt writes to Development.local.json). No cloud secret paths remain in the tree.

## Observability (local)

File logging + optional OpenTelemetry. AWS X-Ray / CloudWatch / Amplify logging sections have been removed with the cloud bloat. Use `TestResults/`, `start-local.sh` logs, and the built-in health endpoints for the core loop.

This README now matches the pure local architecture after the cull. All prior AWS/Amplify/AppRunner/Aurora references have been surgically excised.
