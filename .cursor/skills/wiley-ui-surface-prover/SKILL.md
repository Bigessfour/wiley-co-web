---
name: wiley-ui-surface-prover
description: Systematically audit Wiley Widget UI buttons and surfaces with the browser tool to prove functions and flag no-ops for repair. Use when asked to verify UI interactivity, find dead buttons, prove panel functions, or audit surfaces in wiley-co-web.
---

# Wiley UI surface prover

Prove every interactive control on the local Wiley Widget (`http://localhost:5230`) does what the product plans say it does. A control is **proven** only when the action produces an observable effect. A control is a **no-op** when click or type changes nothing in the UI, network, console, or app state.

Use the Cursor browser (`cursor-ide-browser`: navigate, lock, snapshot, click, type, fill, screenshot, and CDP for console and network). Read each tool schema before calling it. Prefer accessible names, roles, and stable ids over CSS selectors.

Do not change application code during an audit. Report findings. Draft Playwright only when the user asks for tests.

## Read on demand

Re-read these before classifying a no-op. Routes and labels drift; the files win over the inventory below.

| Need                                  | File                                                                                                      |
| ------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| How to start the Mac stack            | `Scripts/start-clerk-local.sh`, `Scripts/status-clerk-local.sh`, `README.md`, `docs/clerk-mac-runbook.md` |
| Route list (source of truth)          | `Components/Layout/NavMenu.razor`                                                                         |
| Shell actions (save, reload, exports) | `Components/Pages/WileyWorkspace.razor`                                                                   |
| Theme toggle                          | `Components/Layout/MainLayout.razor` (`#app-shell-theme-toggle`)                                          |
| Planned behavior                      | `AGENTS.md`, `docs/wiley-widget-functional-breakdown.md`                                                  |
| Panel markup                          | `Components/Panels/*.razor`                                                                               |
| Playwright alignment                  | `docs/playwright-ui-test-strategy.md`, `tests/playwright/`                                                |

Starting inventory from `NavMenu.razor` (confirm on each run):

| Surface                   | Route                                 |
| ------------------------- | ------------------------------------- |
| Town Hub                  | `/`                                   |
| Workspace Overview        | `/wiley-workspace`                    |
| Decision Support (Jarvis) | `/wiley-workspace/decision-support`   |
| Help                      | `/support`                            |
| Break-Even                | `/wiley-workspace/break-even`         |
| Rates                     | `/wiley-workspace/rates`              |
| Scenario Planner          | `/wiley-workspace/scenario`           |
| Affordability             | `/wiley-workspace/affordability`      |
| Debt Coverage             | `/wiley-workspace/debt-coverage`      |
| Capital Gap               | `/wiley-workspace/capital-gap`        |
| Reserve Trajectory        | `/wiley-workspace/reserve-trajectory` |
| Trends                    | `/wiley-workspace/trends`             |
| Data Dashboard            | `/wiley-workspace/data-dashboard`     |
| Apartment Config          | `/wiley-workspace/apartment-config`   |
| QuickBooks Import         | `/wiley-workspace/quickbooks-import`  |
| Customer Viewer           | `/wiley-workspace/customers`          |
| Reference Data            | `/wiley-workspace/reference-data`     |

Also open Home and `/budget-dashboard` if `docs/wiley-widget-functional-breakdown.md` still lists them. Shell controls (sidebar, theme, exports, save baseline, reload) are in scope on every workspace route, not only Overview.

## Workflow

1. Confirm the local servers. Client must listen on port 5230 and the API on 5231. Check with `./Scripts/status-clerk-local.sh` and `curl -fsS http://127.0.0.1:5231/health`. If either is down, start with `./Scripts/start-clerk-local.sh` from the `wiley-co-web` root. `localhost` and `127.0.0.1` are the same stack. Staff login is off on the current Mac path. If a sign-in wall appears, stop and record it. Read `docs/post-production-operations-handbook.md` and `docs/sso-clerksuite.md`. Do not invent credentials.

2. Navigate the browser to `http://localhost:5230`. Lock the tab. Capture the initial screenshot and console. Note failed fetches before any click, so later dead buttons are not blamed on an API that was already down.

3. Inventory routes from `Components/Layout/NavMenu.razor`: Town Hub, Workspace Overview, every Analysis, Data, and Support deep link, plus Home and any other entry point in the functional-breakdown doc. Build the work list from the file, not from memory.

4. For each surface:
   - Navigate to the route. Wait until the panel header is visible and `aria-busy` is clear.
   - Snapshot and list interactive elements: `SfButton` / buttons with `OnClick` or `@onclick`, links, inputs, dropdowns, grid row actions, dialog triggers, the theme toggle, exports, save/apply, refresh/reload.
   - Interact with each one. Click, type sample data only where the edit is reversible, open and close dialogs, change dropdowns. After each action, check four signals: screenshot or snapshot diff, network calls, console messages, and visible state (labels, disabled flags, grid rows, status text).
   - Classify **proven** when an observable effect matches the planned behavior, or **no-op** when none of the four signals change.
   - For each no-op, look up the planned function in `docs/wiley-widget-functional-breakdown.md`, `AGENTS.md`, `WileyWorkspace.razor`, and the matching panel under `Components/Panels`. Write the intended behavior and the repair note (missing handler, handler returns early, control not bound, API call never issued).

5. Spend extra passes on the high-impact surfaces:
   - Workspace shell and sidebar: nav deep links, breadcrumbs, reload workspace, save baseline, theme toggle (`#app-shell-theme-toggle` flips `data-wiley-theme`).
   - Exports: `#export-customers-excel-button`, `#export-workspace-pdf-button`, `#export-scenario-excel-button`, and the archived scenario Excel/PDF buttons. Proof is a download or a status text change on `#document-export-status`, plus a network or console trace. Disabled while `IsExportingDocuments`, or archived buttons disabled with no saved scenario selected, is **blocked**, not a no-op.
   - Scenario save and apply: edit a field, confirm derived totals move, then save or apply only with a throwaway name you can tell from real snapshots.
   - QuickBooks import: exercise upload affordance and preview only. Do not commit an import.
   - Customer viewer: search, open the add/edit dialog, type sample values, then cancel. Do not persist a customer unless the user asked for a create-and-delete round trip.
   - Jarvis chat on Decision Support: send one short prompt and record the request to the chat endpoint and the rendered reply or error.
   - Rates and Break-Even: change one input and confirm the KPI, gauge, or chart updates.
   - Data Dashboard: confirm charts render and any filter or range control changes the visual or the request.

6. Write the report in chat with this table (split by surface if it is long):

   | Surface | Element            | Action taken  | Result | Evidence                           | Planned use / repair note                    |
   | ------- | ------------------ | ------------- | ------ | ---------------------------------- | -------------------------------------------- |
   | Rates   | Current rate input | Typed `12.50` | proven | KPI text changed; no console error | Live rate control recalculates the workspace |

   Result is `proven`, `no-op`, or `blocked` (disabled, auth wall, or skipped because the action is destructive). Evidence names the screenshot, the request URL and status, and any console error. End with a short regression list: console errors, failed network calls, and no-ops grouped by panel.

7. Stay non-destructive. Prefer read-only and reversible actions. Do not commit QuickBooks imports, delete rows, or overwrite the saved baseline with casual edits. If a save is required to prove the control, say so in the report and reset by reloading the workspace afterward. Use screenshots as proof. Between surfaces, reload or navigate home when session edits would leak into the next panel.

8. When the user wants tests, draft focused Playwright cases for new proven paths and for no-ops that should have failed a test. Match `tests/playwright/` (`*.spec.ts`, helpers in `tests/playwright/support/`) and `docs/playwright-ui-test-strategy.md`. Put the draft in chat. Add files only if the user asks.

## Classification rules

- One signal is enough to mark **proven** (a status string, a new row, a 200 response, a download, a dialog, a route change, or a console log tied to the click). Say which signal fired.
- **No-op** means all four signals stayed still after the control was actually enabled and the Blazor render had finished. Retry once after a short wait before filing a no-op, so a slow `Sf*` render is not recorded as dead.
- **Blocked** is a third bucket: the control is disabled, covered by an open dialog, or unsafe to activate. State the reason from the markup (`Disabled`, missing selection, in-flight export).
- A navigation that only highlights the already-active link is proven if `aria-current` or the URL updates. It is a no-op if the href does not match the route and nothing else happens.
- Cross-check no-ops against the functional-breakdown flows: workspace load, scenario planning, QuickBooks preview/commit, Jarvis, and export. Quote the planned behavior in the repair note.

## Common issues

- **Syncfusion persistence.** Grids, splitters, and sidebars restore column widths, filters, and collapsed state from browser storage. A "fresh" load can hide a button or keep a stale filter. If a control looks stuck, note the storage key, clear site data for `localhost`, and retry once in a clean tab.
- **Blazor render lag.** WASM finishes navigation before Syncfusion components accept input. Wait for the panel header and for `aria-busy` to clear. A click during first render is a false no-op.
- **Session state.** `WorkspaceState` is in memory and parts persist in the browser (`wileyWorkspaceStorage`, theme key `wiley.workspace.theme`). A Rates edit changes Break-Even. Record that spillover, then reload before the next surface so later proofs are not contaminated.
- **API down looks like a dead button.** If `http://127.0.0.1:5231/health` fails, fix the process before classifying UI no-ops. Check the client console for failed fetches first.
- **Popups outside the snapshot.** Syncfusion dialogs, dropdowns, and grid menus often appear only after open. Open, snapshot again, then interact. Closing the dialog is part of the proof.
- **Exports.** A file download may not change the page body. Treat a content-disposition response, a browser download, or `#document-export-status` text as the effect. Archived export buttons stay disabled until a saved scenario is selected.
- **Disabled is not dead.** Record the disabled reason. Do not force-click through the DOM.
