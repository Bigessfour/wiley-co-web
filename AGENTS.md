# Agent instructions — Wiley Co / Wiley Widget (wiley-co-web)

This file applies to **Grok CLI**, **Cursor**, and any other coding agent working in this repository.

## What this project is

- **Blazor WebAssembly client** (`WileyCoWeb.csproj`) + **ASP.NET Core API** (`WileyCoWeb.Api/`). The live path is the office Mac (`./Scripts/start-clerk-local.sh`) against Synology Postgres on `mr-storage:5433`. Amplify, App Runner, and Aurora for Widget were removed in November 2026. Do not recreate them. Definition: `docs/production-completion-plan.md`.
- **Not** a WinForms desktop app. Do not assume desktop UI patterns.
- Solution entry: `WileyCoWeb.slnx`. Shared domain libraries under `src/WileyWidget.*`.
- Council-facing municipal finance / utility rate studies for the **Town of Wiley**.

## Toolchain (pinned)

- **.NET SDK 9.0.313** — see `global.json` (`rollForward: disable`). Do not bump SDK without updating CI and Dockerfiles.
- **Syncfusion Blazor 33.x** — license required for builds; see `README.md`.
- Local dev: client `http://localhost:5230`, API `http://localhost:5231`.

## Non-negotiable principles

1. **Protect existing behavior** — prove changes with focused tests, not assumptions.
2. **High-impact areas need extra rigor** — workspace shell, `WorkspaceState`, Syncfusion wiring, snapshot composition, QuickBooks import, Jarvis, exports.
3. **Documentation proximity** — if behavior or ops change, update `docs/` or `specs/` in the same PR.
4. **Do not push to `main`** — branch-protected; open PRs and wait for CI.
5. **Run local CI before every `git push`** — `./Scripts/run-ci-local.sh` (default pre-push gate: HighRisk + publish + Playwright CI smoke/highrisk). Cursor blocks `git push` until `.local-ci-ok` matches `HEAD`. Bypass only with `SKIP_LOCAL_CI=1`. Prefer this over waiting for a red GitHub Actions run.
6. **Do not commit secrets** — `.env`, credentials, local Syncfusion keys.
7. **Minimal diffs** — match existing naming, patterns, and abstractions; no drive-by refactors.

## Canonical implementations (do not regress)

These differ from older council briefs — **keep the repo version**:

| Area                       | Canonical pattern                                                                                                                                                   |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Customer export            | XlsIO via `#export-customer-grid-excel-button` → `ExportCustomerDirectoryAsync` / `CreateUtilityCustomerDirectoryWorkbook`. **Not** SfGrid toolbar Excel export.    |
| QuickBooks duplicate guard | Key is `(canonical_entity, file_hash)` — renaming the file does **not** bypass.                                                                                     |
| Rate math                  | Static `EnterpriseRateService` in `src/WileyWidget.Abstractions/`. No `growthRate` parameter in current domain model. `RateCalculator.cs` is a thin forwarder only. |
| Jarvis                     | Routes under `/api/ai/*` (health alias: `/api/jarvis/health`). Response uses `latestUsedFallback`, not `usedFallback`.                                              |
| Playwright helpers         | `gotoWorkspacePanel` from `tests/playwright/support/workspace.ts` — not `workspaceLogin`.                                                                           |

## Where to work

| Concern                  | Location                                                                               |
| ------------------------ | -------------------------------------------------------------------------------------- |
| Workspace shell / panels | `Components/Pages/WileyWorkspace.razor`, `Components/Panels/*.razor`                   |
| Client state             | `State/WorkspaceState.cs`                                                              |
| HTTP clients             | `Services/`, DTOs in `Contracts/`                                                      |
| API host                 | `WileyCoWeb.Api/Program.cs`, `WorkspaceSnapshotComposer.cs`                            |
| Domain services          | `src/WileyWidget.Services/`                                                            |
| Shared calculations      | `src/WileyWidget.Abstractions/EnterpriseRateService.cs`                                |
| Jarvis                   | `WorkspaceAiAssistantService`, `JarvisHealthState`, `Components/JarvisChatPanel.razor` |

Extended guidance: `.cursor/skills/wiley-widget-completion/SKILL.md`.

## Testing workflow

**Before push (required):**

```bash
./Scripts/run-ci-local.sh          # ~2–5 min: HighRisk + publish + Playwright CI specs
# ./Scripts/run-ci-local.sh --full # optional GH-length full matrix
```

For a smaller blast-radius loop while editing:

```powershell
# HighRisk gate (matches CI)
dotnet test tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests/WileyCoWeb.IntegrationTests.csproj --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests/WileyWidget.Tests.csproj --filter "Category=HighRisk"

# Playwright high-risk (Chromium)
npm run playwright:test:ci:highrisk

# Builds
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
```

Key HighRisk tests: `CustomerDirectoryExportTests`, `QuickBooksImport_RejectsDuplicateFileHash`, `EnterpriseRateServiceTests`, `JarvisHealthApiTests`.

## PR discipline

- Split high-risk slices into **separate PRs** (e.g. CI gates vs rate service vs Jarvis health).
- Do not combine unrelated slices in one merge.
- Include test evidence in PR description.

## Prohibited

- Copy-pasting external spec snippets that conflict with the table above.
- Treating stale logs or skipped tests as release evidence.
- Changing workspace shell, Jarvis, QuickBooks, or export paths without proportionate regression proof.
- Committing under `temp/` (gitignored).

## Key docs

- `docs/wiley-widget-functional-breakdown.md` — feature map and test evidence
- `docs/post-production-operations-handbook.md` — ops validation (incl. `/api/ai/health`)
- `docs/council-readiness-may-2026.md` — release readiness
- `docs/playwright-ui-test-strategy.md` — browser test strategy
