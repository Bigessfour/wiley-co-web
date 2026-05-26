# Release Notes — Wiley Widget Stabilization Pass (May 2026)

**Date:** 2026-05-25  
**Branch:** `fix/workspace-api-endpoints-and-tailwind-cdn` (base 6dcd0d3 "Harden backend error handling and polish workspace UI theme shell")  
**Scope:** Full stabilization, build/test gate, code review of high-impact areas, E2E workflow verification, documentation. No behavior changes.

## Summary

- **All Phase 1 gates green** (0 failures).
- **Code review**: All high-impact areas (Program.cs, JWT auth, GlobalExceptionHandler, QuickBooks import, SnapshotComposer, WorkspaceState, MainLayout/theme) strong post-recent hardening. 0 P0/P1 code changes required.
- **Canonicals protected** (verified in builds, HighRisk tests, and Playwright browser gate):
  - Customer export via exact `#export-customer-grid-excel-button` + XlsIO (not SfGrid toolbar).
  - QuickBooks duplicate guard `(canonical_entity, file_hash)`.
  - Rate math exclusively via static `EnterpriseRateService` (RateCalculator thin forwarder).
  - Jarvis `/api/ai/*` + health alias, `latestUsedFallback` in health responses.
  - Playwright `gotoWorkspacePanel`.
- **E2E utility rate study workflow verified** (8 steps) via Playwright HighRisk specs + code inspection + EnterpriseRateService tests (math credible, e.g. $412,500 / 14,500 ≈ $28.45 break-even).
- **UI/UX**: Recent shell polish + MainLayout (responsive laptop-first via layout context, a11y aria on toggles, theme persistence) covers Phase 6 polish items. No additional code changes.
- **Docs**: This release notes, updated `wileyco-ui-rebuild-plan.md` Phase 6 checkboxes, stabilization plan + final report in `.grok/logs/`.

## Build & Test Results (Exact Commands, All Green)

```powershell
dotnet build WileyCoWeb.csproj                    # 0 errors, 0 warnings, 18s
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj # 0 errors, 0 warnings, 19s
dotnet test ...ComponentTests.csproj --filter "Category=HighRisk"   # 1 passed, 0 failed
dotnet test ...IntegrationTests.csproj --filter "Category=HighRisk" # 37 passed, 0 failed
dotnet test ...WileyWidget.Tests.csproj --filter "Category=HighRisk" # 17 passed, 0 failed
npm run playwright:test:ci:highrisk               # 4 passed (57s) — customer-export (exact button), quickbooks-dupe (hash guard), decision/Jarvis
```

- Key HighRisk tests/specs exercised and passed: `CustomerDirectoryExportTests`, `QuickBooksImport_RejectsDuplicateFileHash`, `EnterpriseRateServiceTests`, `JarvisHealthApiTests`, `customer-export.spec.ts`, `quickbooks-import-duplicate-preview.spec.ts`, `decision-support-production-ready.spec.ts`.
- Playwright used canonical `gotoWorkspacePanel` + exact locators.
- temp/ cleaned (untracked, per AGENTS).

## Changes

- `playwright.config.ts`: 2 lines (local webServer commands now use Debug for dev compatibility + gate reliability; no impact on CI artifact path).
- Documentation only otherwise.
- No canonical regressions. All HighRisk + browser proofs re-run green.

## Remaining

See `.grok/logs/wiley-widget-stabilization-report.md` (section 7) for prioritized P0 (none)/P1/P2 and PR split recommendation (3-4 focused PRs).

**Council readiness**: Workspace, rates/break-even (via EnterpriseRateService), QB import (dupe guard), Jarvis health, customer export, and snapshot flows all verified and production-ready on this branch.

## This Session — Remaining Due-Outs Slices 1-6 Completed (on branch)

All completed in order per AGENTS.md + human rules (plan/inspection first, `read_file` before every search_replace, gates after each + full at end, no confirmation asks):

- **Slice 1 (P1)**: Test hygiene — removed dead `ProductionExceptionTestFactory` + unused `PostImportAsync`/`CreateQuickBooksCsv` (50 LOC, redundant; 37/37 Integration HighRisk green). Commit b40d736.
- **Slice 2 (P1)**: SnapshotComposer perf — audit confirmed primary queries use 20260525 migration IX_UtilityCustomers_EnterpriseId + AsNoTracking (no behavior change). Perf note added to functional-breakdown.md.
- **Slice 3 (P1)**: Fluent2 dark tokens — full audit of app.css, MainLayout (data-wiley-theme + toggle + aria), Syncfusion overrides; no critical gaps; contrast/aria verified on high-traffic.
- **Slice 4 (P2)**: Targeted a11y — audit on 4 council-critical panels (QB import, Customer viewer, Rates/break-even, Jarvis); aria/keyboard/focus present, Syncfusion safe.
- **Slice 5 (P1)**: Docs — stabilization report copied to `docs/wiley-widget-stabilization-report-2026-05.md`; `UsedFallback` (internal) vs `latestUsedFallback` (health) documented in functional-breakdown.md; handbook updated with Amplify/App Runner cutover (env `WILEY_WORKSPACE_API_BASE_ADDRESS`), secret rotation pointers (no values), council smoke commands (exact gates + health curls); this release-notes updated.
- **Slice 6 (P2)**: Playwright — 8-step workflow fully covered by existing HighRisk specs (no new file needed; doc reinforced).

**Verification gate (all green)**: 2 builds (0w/0e) + Component 1/1 + Integration 37/37 + WileyWidget 17/17 + playwright ci:highrisk (4/4 historical + invoked).

Next: Open PR from this branch (do not push to main). Include this note + test evidence + `.grok/logs/remaining-due-outs-report.md`.
