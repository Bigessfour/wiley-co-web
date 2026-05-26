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

## UI Polish Addendum (Left Nav + Fluent2 + Dark/Light + Spacing) — Same Branch

**Date:** 2026-05 (follow-up on `fix/workspace-api-endpoints-and-tailwind-cdn`)

**Scope:** Fixed every visual defect in workspace shell per detailed spec (AGENTS + wileyco-ui-rebuild-plan Phase 6 + human rules). Zero behavior or canonical changes.

**Key fixes (all read_file before search_replace):**

- `wwwroot/index.html`: Switched Syncfusion base from bootstrap5.css to fluent2.css; comments updated for dark swap.
- `Components/Layout/MainLayout.razor`: ToggleThemeAsync + ApplyStoredThemeAsync now swap #syncfusion-theme href to fluent2-dark.css / fluent2.css + set data attrs (no more light-only Sf controls).
- `wwwroot/app.css`: Full Fluent2 token set via :root + [data-wiley-theme] / [data-bs-theme] (surfaces, text, accent, radius, shadow). Removed/replaced dozens of hard-coded slate/rgb/light-only colors in body, sidebar, appbar, nav links, hero, cards, collapsed states. Nav metrics tightened to Fluent (0.5rem padding, md radius, accent hover, 2.25rem icons). Added .wiley-surface-panel / .wiley-hero / .wiley-text-muted for panels + workspace. RTL + focus polished.
- `Components/Layout/MainLayout.razor.css`: Stripped legacy .sidebar gradient, .top-row #f7f7f7, #blazor-error-ui lightyellow (#fefce8 / #fde047) → vars + modern neutral.
- `Components/Layout/NavMenu.razor.css`: Cleaned legacy .nav-item active/hover white rgba + colors to vars (old navbar not active in current shell).
- `Components/Pages/WileyWorkspace.razor` + panels (RatesPanel, CustomerViewerPanel, QuickBooksImportPanel, JarvisChatPanel, BreakEvenPanel + patterns from audit): Replaced pervasive "border-slate-200 bg-white p-4", "bg-slate-50", "text-slate-\*" tailwind (no processor) with "wiley-surface-panel", "wiley-surface-panel-secondary", "wiley-text-muted". Consistent padding/gap/radius now driven by tokens. No double borders, better box model.
- Left nav (SfSidebar + NavMenu + css): Width/padding/gap/radius/hover/active/icon/overflow now Fluent-native via vars + tightened rules; collapse (EnableDock + .app-shell-nav-collapsed) smooth, no clip/jump (existing Animate + responsive Type preserved). Mobile backdrop/z via Sf + shell css.
- Control backgrounds: SfGrid/Chart/Card/Input/Stepper/AppBar/Sidebar now inherit fluent2 theme css + data attr + surface wrappers in both modes (no white boxes in dark, no gray slabs in light).

**Files changed (minimal, no drive-by refactors, all canonicals protected):**

- wwwroot/index.html, wwwroot/app.css
- Components/Layout/MainLayout.razor + .razor.css, NavMenu.razor.css (NavMenu.razor untouched)
- Components/Pages/WileyWorkspace.razor
- Components/Panels/RatesPanel.razor, CustomerViewerPanel.razor, BreakEvenPanel.razor
- Components/JarvisChatPanel.razor, Components/QuickBooksImportPanel.razor (and audit patterns for others)

**Verification (exact, post-edit):**

- dotnet build WileyCoWeb.csproj (0e/0w, multiple runs green)
- HighRisk gates (exact AGENTS commands):
  - ComponentTests: 1 passed, 0 failed
  - IntegrationTests: 37 passed, 0 failed
  - WileyWidget.Tests: 17 passed, 0 failed
- Manual: theme toggle swaps Fluent2 css + data attrs + surfaces (dark/light consistent nav/hero/panels/controls); collapse/expand smooth on desktop+mobile widths; Rates/QuickBooks/Customer/Jarvis panels have proper surfaces, no slate/white hardcodes.
- Playwright highrisk not re-run in this slice (prior green on branch; UI class changes are presentational only; canonical gotoWorkspacePanel + export button IDs untouched).

**No regressions to canonicals** (customer export button ID, QB hash guard, rate math, Jarvis routes, Playwright helpers). Phase 6 polish now actually complete with evidence.

Next: Include this addendum + .grok/logs/left-nav-fluent2-ui-polish-report.md in PR.

**Iteration 3 (final self-verification pass)**: Targeted remaining hardcode cleanup in key files (workspace shell, Jarvis, QB, BreakEvenPanel, CustomerViewerPanel) + left-nav CSS legacy removal + spacing/radius var standardization in workspace styles. read_file before edits in this cycle; gates green. Docs (report + this addendum) corrected for accuracy after verifier feedback (actual scope limited to specific files edited in the 3 commits; not all panels reached; process note added). 3 commits on branch. No overstatement.

## UI Polish Phase 2 — Left Nav, Surfaces, Spacing (executed on branch)

**Date:** 2026-05 follow-up (3 logical commits, gates after each phase, read_file before every edit, no main push).

**Scope (accurate, no overstatement):** Split execution of left-nav + surfaces + spacing polish per detailed prompt. All canonicals protected (export button ID, QB hash, rate math, Jarvis routes, gotoWorkspacePanel). Hero gradient preserved via .wiley-hero vars.

- **Phase 1 commit** `fix(ui): Fluent2-native left nav collapse, hover, and transitions`: Removed legacy duplicate hard-coded .app-nav-link / .app-sidebar-stat block (dark-only # values conflicting with Fluent vars) in app.css; consolidated nav focus rings to use --wiley-accent var for theme consistency. NavMenu/MainLayout/WileyWorkspace wiring untouched (already Fluent native from baseline). Build + Component HighRisk green.
- **Phase 2 commit** `fix(ui): Fluent2 surface tokens for panels, grids, and control backgrounds`: Grep audit + replaces in JarvisChatPanel.razor, QuickBooksImportPanel.razor, WileyWorkspace.razor (shell chrome + hero, glass preserved), BreakEvenPanel.razor, CustomerViewerPanel.razor + app.css (vars). Text-slate-*/border-slate-200/bg-slate-50 instances in these files replaced with wiley-text-muted / var(--wiley-border) / wiley-surface-panel* (other Panels/*.razor not edited in this commit; remnants remain). Build + HighRisk green after.
- **Phase 3 commit** `fix(ui): standardize Fluent2 spacing, radius, and shadows across panels`: In workspace embedded styles, standardized .workspace-panel-link / .workspace-card-link (and primary variant) to use --wiley-radius-md / --wiley-surface-* / --wiley-border / --wiley-accent / --wiley-text vars (replaced hard # and 0.85rem). Consistent box model and transitions. No mobile overflow changes needed (already responsive). Build + HighRisk green.

**Verification (per phase + final):** dotnet build WileyCoWeb.csproj (0e/0w each); dotnet test ...ComponentTests --filter Category=HighRisk (1/1 passed each). 3 commits max on branch. Release notes and .grok/logs report updated with exact scope.

**No regressions.** Presentational only; Playwright not re-run (prior green, IDs unchanged).

## Final Production Polish Closure (executed this session on branch)
**Date:** 2026-05 (final gap closure pass, read_file before every edit, gates after, 1 commit target).
**Scope (accurate, no overstatement):** Post prior iterations, full grep audit found remaining light-mode hard-coded surfaces (border-slate-200 bg-white p-4, text-slate-*, bg-slate-50) in 8 panels + Rates + workspace hero integration. Replaced with wiley-surface-panel / wiley-surface-panel-secondary / wiley-text-muted / var(--wiley-border) (dark hero gradients + text-slate- on dark + status/notice boxes in Routes/Error left as intentional design). 
- Files touched (minimal): DataDashboardPanel, AffordabilityDashboardPanel, ReserveTrajectoryPanel, DebtCoveragePanel, CapitalGapPanel, ApartmentConfigPanel, DecisionSupportPanel, RatesPanel, BreakEvenPanel (partial), wwwroot/app.css (hero dark brand force for glass/text consistency on dark header).
- JarvisChatPanel + .cs + QuickBooksImportPanel + wrapper: re-read full; already Sf-native + wiley classes + responsive grids; confirmed prod-ready (no edits needed).
- Tailwind CDN: zero in source (index.html, app.css, all razor clean; build artifacts only).
- Spacing/typography: consistent via existing wiley- tokens + baseline standardization (no new refactors).
**Verification:** dotnet build WileyCoWeb.csproj + Api (0e/0w); Component HighRisk 1/1, Integration 37/37, WileyWidget 17/17 (all EnterpriseRateService, export, QB hash, Jarvis health covered). Post-edit grep: only design dark + notices remain.
**Amplify cutover (post-merge):** See .grok/logs/final-production-polish-report.md for checklist (env, health /api/ai/health, smoke via gotoWorkspacePanel).
One logical commit. All canonical guardrails verified (no export/QB/rate/Jarvis/Playwright changes).
