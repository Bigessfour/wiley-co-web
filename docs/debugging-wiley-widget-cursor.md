# Debugging Wiley Widget in Cursor / VS Code (agent + human playbook)

Use this when chasing **runtime** errors (exceptions, wrong API data, Blazor render issues). It matches [.vscode/launch.json](../.vscode/launch.json) and [.vscode/tasks.json](../.vscode/tasks.json).

## 1. Install (once)

**Extensions**

- **Workspace recommendations** — Cursor/VS Code will prompt for [.vscode/extensions.json](../.vscode/extensions.json), or install those IDs from the Extensions view.
- **Cursor** — The Cursor CLI often **cannot** install Microsoft `ms-dotnettools.*` packs (“extension not found”). Install **`anysphere.csharp`** in Cursor (CLI: `cursor --install-extension anysphere.csharp`) for editing and debugging. **Blazor WASM companion** (`ms-dotnettools.blazorwasm-companion`) may need to be installed from the **Extensions** UI (OpenVSX/Microsoft) if the CLI fails.
- **VS Code** (optional, full Microsoft stack on the same machine) — `ms-dotnettools.csdevkit`, `ms-dotnettools.csharp`, `ms-dotnettools.blazorwasm-companion` install via `code --install-extension …` or the marketplace.
- **Runtime** — `ms-dotnettools.vscode-dotnet-runtime` (often pulled in automatically).

**.NET SDK** — Repo targets **.NET 9**; run `dotnet --version` (expect **9.0.x**). Already satisfied if you build locally.

**Browser**

- For **Blazor WebAssembly** debugging: **Edge** or **Chrome**, as in [launch.json](../.vscode/launch.json) (`browser` / `inspectUri` WASM debug proxy).

### Where is “Run and Debug” in Cursor?

Cursor is VS Code–compatible; the debug UI is the same but the **left icon bar** can hide icons.

**Keyboard:** Cursor may **override** the VS Code default **Ctrl+Shift+D** (e.g. open Agent or a terminal). Prefer:

1. **Command Palette** — **Ctrl+Shift+P** → type **`View: Show Run and Debug`** or **`Run and Debug: Focus on Run View`** → Enter (most reliable in Cursor).
2. **Menu** — **View** → **Run** (opens the Run and Debug side bar).
3. **Activity bar** — Left edge: **Run and Debug** (play icon with a **small bug**). If missing, **right‑click the activity bar** → enable **Run** / **Run and Debug**.
4. **Rebind** — **Ctrl+Shift+P** → **Preferences: Open Keyboard Shortcuts** → search **`View: Show Run and Debug`** → assign a shortcut that does not conflict (e.g. **Ctrl+Alt+D**).
5. **No launch configs?** Use the dropdown at the top of Run and Debug and pick **Debug API Host** or **Debug Full Stack (Client + API)** (from [.vscode/launch.json](../.vscode/launch.json)). Open the **folder** that contains `.vscode/launch.json` (repo root).

## 2. Pick the surface (decision tree)

| Symptom                                                                         | Launch configuration                                                               |
| ------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| 500/404 from API, EF, secrets, `Program.cs` maps                                | **Debug API Host**                                                                 |
| Exception in Razor/components, `Services/*` on the **client**, `WorkspaceState` | **Debug Blazor Client (http)**                                                     |
| Snapshot/knowledge/capital-gap calls wrong data; need both                      | **Debug Full Stack (Client + API)** (compound)                                     |
| Failure only in a test                                                          | **Debug Component Tests** / **Debug Integration Tests** (set filter when prompted) |
| Process already running (`dotnet run` in terminal)                              | **Attach to Running Process** → pick the `WileyCoWeb.Api` or `WileyCoWeb` process  |

**Ports (defaults in this repo)**

- Blazor dev host: `http://localhost:5230`
- API: `http://localhost:5231`
- Full-stack debugging expects `WILEY_WORKSPACE_API_BASE_ADDRESS=http://localhost:5231` for the client (already set on **watch: debug client** task).

### Docker Postgres + terminal stack (no F5)

Matches [docker-compose.yml](../docker-compose.yml) (`wileyco_local` / `localdevpw` on **host port 5433** — avoids Windows PostgreSQL on 5432):

1. **Tasks** → **Start Local API (Docker Postgres)** — or `pwsh -NoProfile -File .\Scripts\start-wiley-widget-local.ps1`
2. **Tasks** → **Start Local Blazor Client** — or `pwsh -NoProfile -File .\Scripts\start-wiley-widget-client.ps1`  
   Or one command: `start-wiley-widget-local.ps1 -StartClient`
3. Open http://localhost:5230/wiley-workspace

First run creates gitignored `appsettings.Development.local.json` and `appsettings.Workspace.local.json` from the `*.example.json` templates. API logs should show `DatabaseConfigured=True` (not degraded mode) when Postgres is reachable.

**Syncfusion license (Blazor WASM):** Store `SYNCFUSION_LICENSE_KEY` in Windows **Machine** or **User** environment variables. Before build/run, `Scripts/Apply-SyncfusionLicenseEnv.ps1` copies it into `wwwroot/appsettings.Syncfusion.local.json` and sets process env for the API. The client calls `SyncfusionLicenseProvider.RegisterLicense` from that JSON before `AddSyncfusionBlazor` ([Syncfusion docs](https://blazor.syncfusion.com/documentation/getting-started/license-key/how-to-register-in-an-application)). F5 **Debug Build** tasks run the prepare step automatically.

### Startup logs (local scripts + client)

| Source | Where to look |
| --- | --- |
| API + script steps | Terminal running `start-wiley-widget-local.ps1`; file `TestResults/local-startup/local-start-*.log` |
| Blazor dev server | **Separate window** when using `-StartClient`; file `TestResults/local-startup/local-client-*.log` |
| WASM client boot | Browser DevTools → Console: lines prefixed `[WileyWidget.Client.Startup]` |
| API runtime | Same terminal as API: `WileyWidget.Startup.*`, `DatabaseConfigured=True` expected |

If **localhost:5230** fails: confirm **5230 is listening** (`Get-NetTCPConnection -LocalPort 5230 -State Listen`). API-only on 5231 is not enough — the UI requires the client script window to finish `dotnet run`.

## 3. Agentic debug loop (repeat until root cause)

Use this loop in **Chat** or with a colleague. Each step produces evidence.

1. **Reproduce** — One URL or action (e.g. open panel, call endpoint). Note **expected vs actual** (status code, message, UI text).
2. **Classify** — Server-only, client-only, or both (Network tab / exception stack).
3. **Launch** — Start the smallest config from §2 (often **Debug API Host** first for HTTP errors).
4. **Break** — Set breakpoints on the **handler** (`Program.cs` minimal API), **service** (`WileyWidget.Services`), or **component** (`.razor.cs`).
5. **Exceptions** — **Run → Break on Exceptions** (or use **Debug with Exception Insights** in launch.json for noisy failures). The API launch already breaks on common types (see `exceptionOptions` in launch.json).
6. **Verify data** — Inspect variables: DTOs, `HttpContext`, EF entities. For API-from-client issues, confirm base URL in browser **Network** (host, path, CORS).
7. **Log** — If breakpoints are noisy, add a temporary `ILogger` line or raise log level (see **Debug with Exception Insights** env in launch.json). Remove before merge.
8. **Prove fix** — Same reproduction path; run targeted `dotnet test` or Playwright smoke if UI changed.

## 4. Cursor-specific tips

- **Run and Debug** — Use **Command Palette** → **`View: Show Run and Debug`** if **Ctrl+Shift+D** opens Agent in Cursor (see §1). Choose configuration, then **F5**. **Shift+F5** stops.
- **Compound configs** start multiple debuggers; **stopAll** stops both when you stop debugging.
- **Terminal** — **Debug Console** shows program output when debugging .NET; **Output** → **C#** for extension logs.
- **Ports in use** — If F5 says address in use, run task **🛑 Kill Local Debug Web Stack** (frees `5230`/`5231` on Windows) or stop manual `dotnet run` terminals.
- **Ask the agent** — Paste stack trace + which launch config you used + file you suspect; reference this doc so the agent stays aligned with your ports and projects.

## 5. Common pitfalls

- **F5 / Full stack “does nothing”** — Ensure the repo root is open and [.vscode/launch.json](../.vscode/launch.json) exists. This file is **tracked in git** (commit `launch.json` if you pulled an older clone that still ignored it). You need the **Blazor WASM** debug type: install **ms-dotnettools.blazorwasm-companion** (and **`anysphere.csharp`** or Microsoft C#) so `blazorwasm` and `coreclr` are registered. Watch **TERMINAL** (API) and the **browser** (Blazor); if the debugger never starts, check **View → Output → C#** / Debugger for errors.
- **Hollow breakpoints (WASM)** — Start debugging via **Blazor** launch, not only opening the browser manually.
- **API not hit** — Client still pointing at production App Runner; use local base address or `wwwroot/appsettings.Workspace.local.json` per [README.md](../README.md).
- **preLaunchTask failed** — Run `dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj` (or solution) in terminal; fix compile errors first.
- **Syncfusion / static assets** — Licensing and `wwwroot` issues often show as **browser Console** errors, not C# breakpoints.

## 6. Release evidence (skill alignment)

Per `wiley-widget-completion` skill: prefer a **test** or **Playwright smoke** repro for regressions after you fix a council-facing bug. Debugging in the IDE proves _cause_; automated tests prove _stays fixed_.
