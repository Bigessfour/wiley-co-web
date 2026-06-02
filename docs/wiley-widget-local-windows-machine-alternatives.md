# Wiley Widget: Local Windows 11 Machine-Based Deployment & AWS Decoupling Guide

**Status**: Analysis + recommendations (2026-05 context). Created in response to full AWS cost decoupling requirement. Follows AGENTS.md strictly: protects existing behavior, focuses on minimal-diffs paths first, high-impact notes for workspace/AI/QB/exports/Jarvis.

This document reviews the current cloud dependencies, required tech stack, persistence and service needs, and concrete alternatives to run the full Wiley Widget (Blazor WASM client + thin ASP.NET Core API + domain logic) as a self-contained or easily-hosted application on a single Windows 11 machine (laptop, desktop, or town hall PC) with **no ongoing AWS billing**.

It is **not** a desktop rewrite. The canonical architecture (rich WASM client + thin API for calculations, imports, AI orchestration, and persistence) is preserved where possible.

## Executive Summary of Current Architecture (from codebase review)

- **Client**: `WileyCoWeb.csproj` — Blazor WebAssembly (.NET 9). Pure SPA. Runs entirely in browser. Dev server on `http://localhost:5230`. Published output is static files (`wwwroot` + `_framework` WASM assets).
- **API**: `WileyCoWeb.Api/WileyCoWeb.Api.csproj` — ASP.NET Core 9 "thin" backend (minimal APIs in `Program.cs`, services from `src/WileyWidget.*`). Handles:
  - Workspace snapshot composition, knowledge, rate math (delegates to `EnterpriseRateService`).
  - Jarvis/AI chat orchestration (Semantic Kernel + plugins + history).
  - QuickBooks Desktop file imports (Excel/CSV parsing, routing rules, dedup by `(canonical_entity, file_hash)` — rename does not bypass).
  - CRUD for utility customers, scenarios, exports (artifacts stored as `byte[]`).
  - Health, reference data bootstrap.
- **Shared**: `src/WileyWidget.Abstractions|Business|Data|Models|Services*` — domain models, EF repos, rate service (static, no growthRate param), QB parsers, export (Syncfusion XlsIO/Pdf), Jarvis personality + plugins.
- **Data**: EF Core 9 + Npgsql (PostgreSQL/Aurora). ~30+ entities (Enterprises, Budgets, UtilityCustomers, BudgetSnapshots + Artifacts with jsonb/bytea payloads, ConversationHistory, imported QB Ledger/ChartOfAccount/Amplify* tables, routing rules, audits, etc.). Full persistence required for dedup, scenarios, Jarvis memory, customer directory, exports.
- **External calls (non-AWS)**:
  - xAI Grok (`https://api.x.ai/v1`, OpenAI-compatible + Responses API) for Jarvis full function. Key required for non-deterministic responses.
  - None for QuickBooks (file-based Desktop exports only).
  - All exports/rate calcs/imports local (Syncfusion libs + in-proc services).
- **Current "local" support**: Already strong for development.
  - `appsettings.Development.json` + `*.local.json` (gitignored) + env + User Secrets.
  - Degraded InMemory mode + `AppDbStartupState` when DB unreachable (dev + tests).
  - Client auto-defaults API to `localhost:5231` on loopback.
  - `EncryptedLocalSecretVaultService` (DPAPI LocalMachine, Windows-native encrypted vault) + obsolete plaintext fallback.
  - Local dev scripts (`Scripts/debug-local-runtime.ps1`, `kill-dotnet.ps1`, `sync-local-api-database-url.ps1`).
  - Playwright config supports local `dotnet run` stack.
  - `dotnet ef` + DesignTime factory defaults to local postgres.
- **AWS surface area (the parts to remove/replace for cost decoupling)**: Isolated and small. **No S3, no Bedrock, no Lambda in runtime path, no full AWS SDK**. Confirmed via exhaustive grep on `**/*.cs`, `*.csproj`, `Directory.Packages.props`.

### Exact AWS Dependencies Identified (runtime + build/deploy)

1. **Hosting (Amplify + App Runner)**:
   - Amplify: Static hosting + build-time secret injection (via `amplify.yml` + node parser for `SYNCFUSION_LICENSE_KEY`, `WILEY_WORKSPACE_API_BASE_ADDRESS` → `wwwroot/appsettings.*.local.json`). Also CORS origin whitelists (`.amplifyapp.com`, town domains).
   - App Runner: Container host for API (Dockerfile pins SDK 9.0.313). Runtime env/secret injection for `DATABASE_URL`, `XAI_API_KEY`, `SYNCFUSION_LICENSE_KEY`.
   - Related: `apprunner-update.json`, ECR references in scripts, VPC connector notes (TLS revocation workaround in named HttpClient for xAI).

2. **Database (Aurora PostgreSQL)**:
   - Npgsql + EF migrations (Postgres-specific: `timestamp with time zone`, `bytea`, `jsonb`, `GENERATED ... IDENTITY`, `UseIdentityByDefaultColumns`).
   - Connection via `ConnectionStrings:DefaultConnection` or `DATABASE_URL`.
   - Raw SQL in `Program.cs` (QuickBooks schema guard) and some seed.
   - No Dapper; pure EF + factories for lifetime control.

3. **Secrets / Config (Secrets Manager + SSM Parameter Store)**:
   - `AWSSDK.SecretsManager`, `AWSSDK.SimpleSystemsManagement` only in API.
   - `SecretResolver` (and Helpers): env/config first → SSM (if `XAI:ParameterName`) → Secrets Manager (`XAI:SecretName` default "Grok").
   - `NormalizeAppRunnerInjectedXaiApiKey` (JSON secret unwrap for App Runner).
   - `WILEY_AWS_REGION` / `AWS:Region` for clients.
   - X-Ray: `AWSXRayRecorder.Handlers.AspNetCore` + `TracingBootstrapper` (optional distributed tracing).
   - Amplify build also pulls xAI key (for portal?).

4. **Other / Build**:
   - `amplify.yml`, IAM policies (`iam/`), trust-policy, update-config, various `*-apprunner*` / `aurora-*` scripts and docs.
   - Origin checks in `Program.cs` (`IsAllowedAmplifyPreviewOrigin`, etc.) and CORS tests.
   - Testcontainers.PostgreSql (test infra only; runs local Docker).
   - No runtime impact from Amazon.Lambda.* (transitive in bins from past).

**CloudWatch / SES**: Logging via Serilog (file/console) + OTEL/SigNoz (optional, local endpoint default). SES only for external town email forwarding (not in app code paths).

**Impact**: Removing AWS is low-risk for core function. The app was always designed with local dev paths and fallbacks. High-risk areas (per AGENTS): workspace shell/panels, `WorkspaceState`, Jarvis (`/api/ai/*`, `latestUsedFallback`, health), QuickBooks duplicate guard + import, snapshot composition/exports, `EnterpriseRateService`.

## Tech Stack Required Today (what must be satisfied locally)

- **.NET**: Exact SDK `9.0.313` (global.json `rollForward: disable`). Runtime 9.0.x sufficient for published self-contained apps. Client has `<RuntimeFrameworkVersion>9.0.14</RuntimeFrameworkVersion>`.
- **Syncfusion Blazor 33.x** (33.2.3 central): License key mandatory for production builds / to suppress popup + for server-side XlsIO/Pdf in some flows. Injected at client build (wwwroot json) and API runtime (LicenseBootstrapper).
- **Database**: PostgreSQL  (currently). EF model has Postgres annotations + column types. InMemory fallback exists but is non-persistent + skips some relational guards.
- **AI**: xAI account + `XAI_API_KEY` (or config). Calls are direct HTTPS (no AWS proxy needed locally). Falls back gracefully (NullAIService / deterministic Jarvis responses) if missing.
- **Secrets (local)**: Env vars (preferred for `XAI_API_KEY`), `*.local.json` (ignored), .NET User Secrets, or `EncryptedLocalSecretVaultService` (Windows DPAPI machine-bound).
- **Other runtime**: Standard Windows 11 (pwsh, Edge/Chrome for WASM debug). No Node required for runtime (only Playwright/MCP tooling + historical Amplify builds).
- **Optional but used in dev**: Docker Desktop (for Testcontainers + easy Postgres).

**Persistence needs** (must be local):
- All operational data: enterprises/budgets/transactions/depts, utility customers + bills, imported QB data (for dedup/routing), saved scenarios + export artifacts (binaries in DB), Jarvis conversation/recommendation history, audits, AppSettings (for AI config promotion).
- File hashes + parsed rows for QuickBooks (canonical guard lives in DB).
- "Import Data/" folder (xlsx samples) for bootstrap/reference import (explicit path in prod config).

**No other external services** required at runtime beyond optional xAI (and user's Syncfusion license).

## Alternatives to Replace AWS Components (ranked by fidelity / minimal change)

### 1. Database (Aurora → Local)
**Primary recommended (highest fidelity, minimal code change)**: Local PostgreSQL via Docker Compose.
- `docker-compose.yml` (new, tracked): `postgres:16-alpine` (or 15 matching Aurora), named volume for persistence, healthcheck, port 5432, init scripts if needed.
- Connection: `Host=localhost;Port=5432;Database=wileyco_local;Username=postgres;Password=...` (put in `WileyCoWeb.Api/appsettings.Development.local.json` or env `DATABASE_URL`).
- Startup: Existing `AllowDegradedStartup` + seed flags work. `dotnet ef database update` (or code in startup) for migrations.
- Pros: Exact same engine/behavior as Aurora. All migrations, jsonb, bytea, raw SQL, Testcontainers parity, snapshot/audit/QB dedup work unchanged.
- Cons: Requires Docker Desktop on Win11 (free Personal use; ~few hundred MB RAM when running).
- Scripted bootstrap: Extend `Scripts/` with `start-local-db.ps1` (docker compose up -d, wait healthy, ensure db/user, migrate).

**Zero-dependency alternative**: SQLite (via `Microsoft.EntityFrameworkCore.Sqlite`).
- Add provider package.
- Extend `AppDbContextFactory` + config (`Database:Provider=SQLite|PostgreSQL|InMemory`).
- In `AppDbContext.OnModelCreating`: make `HasColumnType` conditional (or remove for SQLite — EF defaults: `byte[]`→BLOB, `DateTime`→TEXT/INTEGER, `jsonb`→TEXT or use `json` + value converter for the Payloads).
- Raw SQL (QB guard in Program.cs): make provider-aware or use `EnsureCreated` + `ExecuteSqlRaw` only for Postgres, or universal "IF NOT EXISTS" patterns + SQLite syntax.
- Migrations: Keep Npgsql migrations for Postgres path. For SQLite use `EnsureCreatedAsync()` on first run (or separate migration set / code-first init). Avoid running Npgsql migrations against SQLite.
- Connection: file path e.g. `%LOCALAPPDATA%\WileyWidget\wiley-widget.db` (or user-configurable). Use `Microsoft.Data.Sqlite`.
- Pros: Truly single-machine, no external process/service. Self-contained in published app dir or AppData. Good for "installable" feel. InMemory is already close.
- Cons: Loses some Postgres features (full-text, certain concurrency, exact jsonb queries if used). Requires code + test changes in high-risk areas (imports, snapshots, history). New migration story. Data migration path from Postgres needed if switching existing installs. Some `IsRelational()` guards already exist — expand them.
- When to use: For a packaged "WileyWidget.exe" desktop distribution where user should not install anything else.
- Evidence path: Add `Category=HighRisk` test for SQLite roundtrip (CRUD, import dedup, snapshot save/restore, conversation persist) using `:memory:` or temp file.

**Degraded fallback (already present)**: InMemory — fine for demos/smoke without data, but **not** for council use (no persistence across restarts, limited query support).

**Native Postgres (no Docker)**: User installs PostgreSQL 16 for Windows (EDB installer or `winget install --id PostgreSQL.PostgreSQL`). Create DB/user via psql or script. Update connection. Viable, but Docker is more reproducible across machines.

Recommendation order: Docker Postgres (default local dev) → SQLite (for pure machine packaging) → native Postgres.

Update `DesignTimeDbContextFactory`, dev appsettings, and add provider switch before any PR.

### 2. API Hosting (App Runner → Local Process / Service)
- **Dev**: `dotnet run --project WileyCoWeb.Api/WileyCoWeb.Api.csproj --urls http://localhost:5231` (or launch profile). Already works.
- **"Machine app" packaging**:
  - `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true` → single-folder `WileyCoWeb.Api.exe` (or true single-file). Includes all deps. No SDK needed on target Win11.
  - Run as console (visible) or background.
  - As Windows Service: Use `Microsoft.Extensions.Hosting.WindowsServices` + `UseWindowsService()`. Install via `sc create` or a small installer (WiX / MSDeploy / simple .ps1).
  - Tray app (optional): Small WinForms/WPF host that starts Kestrel in-proc (host the WebApplication inside the tray process) + shows icon + "Open Workspace" (launches browser).
- **Same-origin simplification** (nice for single-machine): Publish client static assets, configure API `app.UseStaticFiles(...)` + `app.UseBlazorFrameworkFiles()` (or manual), serve from same port. Client sets base to same origin (no CORS, no second port). Update `ResolveLocalApiBaseAddress` / default logic. Low risk if CORS stays for backward.
- Health endpoint `/health` remains the ops surface.
- No container required locally (Dockerfile is for past App Runner only).

### 3. Client Hosting (Amplify → Local Dev Server or Bundled Static)
- Dev: `dotnet run` (WileyCoWeb.csproj) — already Kestrel + Blazor dev server.
- Packaged: `dotnet publish -c Release` → drop `publish/wwwroot` folder next to API exe. Instruct user to open `index.html`? (often fails for WASM due to MIME/fetch). Better: serve from the API process (see above) or a tiny companion `dotnet serve` / custom static host in the launcher.
- Config injection: Manual `wwwroot/appsettings.Workspace.local.json` + `appsettings.Syncfusion.local.json` (or build-time target already supports env + local root files). No Amplify build needed.
- CORS: Keep Amplify origins for hosted history, but default dev allows loopback. Make origin list fully configurable (already largely is).

**True "desktop app" feel alternatives (higher effort, post-decouple)**:
- Blazor Hybrid (MAUI Windows or custom WinUI3 + WebView2 hosting the published WASM assets + in-process API server). One .exe, native window, offline-capable shell.
- WPF/WinUI + Syncfusion WPF controls (not Blazor) — major UI port, loses WASM investment. Only if "no browser" hard requirement.
- Electron wrapper around published client + spawned API — works but heavy (Node + .NET).
Recommendation: Stay with browser-launched local web stack first (minimal diff). Add hybrid only if user feedback demands "no browser, single taskbar icon".

### 4. Secrets / Config (Secrets Manager + SSM → Local Windows-native)
- Already excellent support. Prefer:
  1. Machine-scoped env: `XAI_API_KEY=...` (PowerShell: `[Environment]::SetEnvironmentVariable("XAI_API_KEY", $key, "Machine")`; restart processes). Docs already call this out as canonical for local pwsh.
  2. `WileyCoWeb.Api/appsettings.Development.local.json` (or Production.local for a "prod-like local" service).
  3. .NET User Secrets for dev machines.
  4. `EncryptedLocalSecretVaultService` (implementations exist; register in DI for any secret needs beyond xAI). Uses `ProtectedData` DPAPI LocalMachine — secrets survive only on that Windows machine/user profile.
- Remove/replace `SecretResolver` AWS paths:
  - Make AWSSDK packages optional or remove from `WileyCoWeb.Api.csproj` + Directory.Packages.props.
  - Simplify resolver to configured/env + vault only. Keep result shape for logging/health.
  - Delete or `#if !LOCAL` the AWS using + client creation.
- `NormalizeAppRunner...` and SSM-specific code can be removed or gated.
- X-Ray: Remove entirely (or keep OTEL path which is already wired as alternative). Serilog file sink + console is sufficient for machine ops.
- Amplify build secrets: No longer relevant. Local builds use the csproj target + manual local json files.
- Update `EnforceXaiApiKeyStartupGuards` (prod throws without key when AI enabled — keep, or relax for "local prod" service installs).

Result: Zero AWS calls at startup or runtime for secrets.

### 5. Observability / Misc (X-Ray / CloudWatch → Local)
- Serilog (file + console + enrichers) + existing `TelemetryStartupService` (gated) + OTEL/SigNoz (configure local collector or disable).
- Health checks (Syncfusion license, etc.) stay.
- Logs: Default to `%APPDATA%\WileyWidget\logs\` or current dir via config.
- Alarms: N/A for local; use Windows Event Log or simple file watchers / PowerShell monitoring script if desired.

### 6. Build / Deploy Artifacts to Clean or Keep
- Keep `Dockerfile` (useful for other container hosts) and `amplify.yml` (historical + if anyone wants to re-host).
- New: `docker-compose.yml`, publish scripts (`Scripts/publish-local-machine.ps1`), `start-wiley-widget.ps1` (starts DB if needed, API as service or exe, waits, launches browser to 5230/5231).
- Remove or comment AWS IAM / update-*.json / aurora scripts? Move to `docs/archive/aws/` or keep under `Scripts/aws-legacy/`.
- Update `global.json` comment if needed (pinned for reproducibility, not AWS).
- CORS: Keep Amplify origins in default `appsettings.json` (harmless) but document they are legacy.

### 7. AI / Jarvis (xAI remains external)
- Unchanged. User pays xAI per token for live Grok responses. Local machine calls `api.x.ai` directly (firewall permitting; same as before).
- Fallbacks ensure app is usable without key (deterministic answers based on local snapshot/knowledge).
- No change to `/api/ai/health`, `latestUsedFallback`, plugins, `WorkspaceAi*` services.
- Persisted config (AppSettings.Xai*) works with local DB.

QuickBooks, exports, rate math, workspace panels, snapshot composer: **100% unchanged** — all local.

## Recommended Local Windows 11 "Machine App" Architecture (Minimal-Diff Path)

1. **Default local runtime**: Two (or one) `dotnet` processes + local Postgres (Docker Compose recommended).
   - Or single published self-contained `WileyCoWeb.Api.exe` (self-hosts everything if we add static client serving + same-origin).
2. **Data**: Docker Postgres (volume persisted) or (future) SQLite file in AppData.
3. **Secrets**: Machine env var for XAI + (optional) Encrypted vault. No AWS.
4. **Launch experience**:
   - Dev: Existing VS/Cursor launch + compound "Full Stack".
   - Operator machine: `pwsh -File Scripts\start-wiley-widget-local.ps1` (or installed shortcut) → starts DB (if docker), API (background or service), opens Edge to workspace.
   - "Installed app": Publish self-contained API (with bundled client static), `sc create` service or tray launcher, browser or WebView2 shell.
5. **Offline**: Core (customers, rates, QB import, snapshots, exports, local calcs) work without net. Jarvis full AI requires net + key.
6. **Updates**: `dotnet publish` or xcopy new exe + `docker compose pull` (or just replace files). Re-run migrations on DB if schema changed.
7. **Multi-user / town clerk**: Run the stack on a shared Windows PC or small always-on box. Data lives in local DB. Backups = copy the postgres volume or SQLite .db + log files. (No cloud sync unless added later.)

**High-risk validation required for any change** (per AGENTS + HighRisk gates):
- `CustomerDirectoryExportTests`
- `QuickBooksImport_RejectsDuplicateFileHash`
- `EnterpriseRateServiceTests`
- `JarvisHealthApiTests`
- Playwright high-risk (customer-export, QB duplicate, decision-support, workspace flows)
- Full `dotnet test ... --filter "Category=HighRisk"` + `npm run playwright:test:ci:highrisk`
- Builds: both projects.

## Migration / Adoption Steps (Proposed PR Slices — Do Not Combine)

1. **Docs + scripts only (low risk)**: Add this doc + `docker-compose.yml` + `Scripts/start-wiley-widget-local.ps1` (or similar) + update README/AGENTS.md with "Local Windows Machine" section + local Postgres setup. Update dev appsettings example. No code behavior change.
2. **Remove AWS runtime deps (medium)**: Delete/replace SecretResolver AWS paths + packages, remove XRay bootstrap + using, simplify AppRunner normalize, make Amplify origin checks generic/config-driven. Update tests (stub origins). Keep behavior for env/config keys. Add local vault registration example. Run full HighRisk + build gates.
3. **SQLite provider support (high impact — separate PR)**: If desired for pure no-Docker desktop. Add package, conditional config, update factory/context/raw SQL, new/updated tests, docs for file location + backup. Prove with HighRisk import/snapshot/Jarvis history tests against SQLite.
4. **Packaging + launcher (medium)**: Publish profiles, self-contained scripts, optional static serving in API for same-origin, tray/launcher prototype (or just docs + pwsh). Update launchSettings/CORS if ports change.
5. **Cleanup / archive**: Move AWS-specific scripts/docs to `archive/`, update handbook, remove Amplify references from primary README where obsolete. (Do after 1-2.)
6. **Optional later**: Hybrid desktop shell, auto-update, local-only AI (Ollama/LM Studio via Semantic Kernel local connector — would need IAIService abstraction extension).

**Never**:
- Change rate math, QB duplicate key, Jarvis response contract (`latestUsedFallback`), canonical export path, or workspace panel behavior without proportionate tests + evidence.
- Commit secrets or local .json with keys.
- Push directly to main.

## Local Run Instructions (Immediate, No Code Change Needed Today)

1. Install .NET SDK **exactly 9.0.313** (global.json has `"rollForward": "disable"` — see AGENTS.md). `winget` or direct download from Microsoft. If your machine has a nearby 9.0.x (e.g. 9.0.314), builds will fail the SDK resolution check until the exact minor is present or you temporarily edit global.json (do not commit the edit). The starter script prints the detected SDK for awareness.
2. (Recommended) Install Docker Desktop. `docker --version`.
3. Set machine env (pwsh as admin or current user):  
   `[Environment]::SetEnvironmentVariable("XAI_API_KEY", "xai-...", "User")` (or "Machine"). Re-open terminals.
4. (Optional but recommended for full UI) Set `SYNCFUSION_LICENSE_KEY` same way.
5. Create `WileyCoWeb.Api/appsettings.Development.local.json` (ignored):  
   ```json
   {
     "ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=5432;Database=wileyco_local;Username=postgres;Password=yourpw" },
     "XAI": { "Enabled": true },
     "Database": { "AllowDegradedStartup": true, "SeedDevelopmentData": true, "EnsureWorkspacePanelBudgetWhenEmpty": true }
   }
   ```
6. Start DB (once): Add docker-compose (see below) → `docker compose up -d`.
7. Migrate (if needed): `dotnet ef database update --project src/WileyWidget.Data --startup-project WileyCoWeb.Api`.
8. Start API: `dotnet run --project WileyCoWeb.Api/WileyCoWeb.Api.csproj` (or F5 "Debug API Host").
9. Start client (new terminal): `dotnet run` (from root) or F5 "Debug Blazor Client". Set `WILEY_WORKSPACE_API_BASE_ADDRESS=http://localhost:5231` if needed.
10. Open `http://localhost:5230`. Use QuickBooks panel with files from `Import Data/`. Jarvis works if key valid.

**Minimal docker-compose.yml skeleton** (add to repo root):
```yaml
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: wileyco_local
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: yourpw
    ports:
      - "5432:5432"
    volumes:
      - wiley_pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
volumes:
  wiley_pgdata:
```
(Expand with init SQL if desired. Existing reset scripts can be adapted.)

For pure no-docker: Install Postgres, create DB, use the connection string above.

## Risks & Open Questions for Follow-up

- **Data portability/backup**: For local Postgres volume or SQLite .db — document simple copy/restore. (Add to handbook.)
- **Multi-machine / clerk handoff**: Today single-machine. Future: export/import snapshot bundles or file-based QB re-import.
- **License distribution**: Syncfusion key still needed per machine/build. User (town) owns license.
- **Performance on low-end Win11 PC**: API is thin; Aurora was overkill. Monitor during imports (50MB uploads allowed).
- **Service hardening for "always on" machine**: Windows Service + log rotation + auto-start + firewall (inbound 5230/5231 local only).
- **If full offline AI required**: Extend `IAIService` + add local LLM connector (Ollama via Semantic Kernel). Out of scope for initial decouple.
- **Tests on pure local SQLite**: Will surface any hidden Npgsql assumptions.

## References (codebase)

- `AGENTS.md` (canonical patterns, HighRisk gates, locations).
- `WileyCoWeb.Api/Program.cs` (startup, secrets, DB ensure, CORS, health, seeding).
- `src/WileyWidget.Data/AppDbContextFactory.cs`, `AppDbStartupState.cs`, `AppDbContext.cs`.
- `src/WileyWidget.Services/EncryptedLocalSecretVaultService.cs`, `WorkspaceAiAssistantService.cs`, `QuickBooksImportService.cs`.
- `ClientStartup.cs` + `ClientStartup.LocalSettings.cs`.
- `docs/aws-server-side-closure-plan.md`, `app-runner-successor-review.md`, `post-production-operations-handbook.md`, `quickbooks-desktop-import-guide.md`, `wiley-widget-functional-breakdown.md`.
- `Directory.Packages.props`, `WileyCoWeb.Api/WileyCoWeb.Api.csproj`, global.json.
- Playwright config + Scripts/ for local automation.

**Next action recommendation**: Implement slice 1 (docs + docker + start script) + verify `dotnet build` + one HighRisk run + manual local smoke (import + snapshot + Jarvis health). Open PR. Do not push main.

This converts the system to a fully machine-resident app while preserving every council-facing behavior and test gate.

(End of analysis document. Update as implementation proceeds.)
