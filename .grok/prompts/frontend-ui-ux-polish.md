# Frontend UI/UX Polish — Wiley Widget (Grok CLI)

Review and polish the **entire Blazor WebAssembly front-end** for municipal finance council/clerk workflows.

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`, `docs/wileyco-ui-rebuild-plan.md` (Phase 6), `docs/playwright-ui-test-strategy.md`, `docs/wiley-widget-functional-breakdown.md`

**Mandatory workflow:**

1. **Plan first** — use Grok Plan Mode / `plan` sub-agent to produce a detailed slice plan (panels, layout, theme, a11y, perf) before coding.
2. **Implement slice-by-slice** — run focused tests after each slice. Do not ask for confirmation between todos.
3. **Use `read_file` before every `search_replace`.**

## Scope (user request)

- **Syncfusion 33.x** — modern, clean control usage; prefer theme classes and built-in states over bespoke wrappers
- **Layout & responsiveness** — consistent shell, laptop-friendly panels, mobile nav/backdrop patterns in `MainLayout`
- **Accessibility** — labels, roles, keyboard focus, contrast, grid/chart aria where applicable
- **Municipal finance user flow** — clearer paths for rates, break-even, scenarios, QuickBooks import, customer directory, trends, Jarvis
- **Performance** — faster first panel render, lighter re-renders, efficient `SfGrid` paging/virtualization where appropriate, avoid blocking UI on snapshot load
- **Dark/light theme** — end-to-end Fluent2 (or current canonical theme) with persisted user preference; fix `MainLayout.razor.css` hardcoded light-only gaps
- **Visual polish** — spacing, typography, badges, loading states, empty states, toast consistency

## Canonical patterns (do not regress — AGENTS.md)

| Area                 | Keep                                                                                                               |
| -------------------- | ------------------------------------------------------------------------------------------------------------------ |
| Customer export      | XlsIO via `#export-customer-grid-excel-button` → `ExportCustomerDirectoryAsync` — **not** SfGrid toolbar Excel     |
| Workspace navigation | `gotoWorkspacePanel` in Playwright; panel IDs and shell in `WileyWorkspace.razor`                                  |
| Jarvis               | `SfAIAssistView` secure path; routes `/api/ai/*`                                                                   |
| Rate display         | Council-facing rates from snapshot / `EnterpriseRateService` — do not expose wrong scenario KPIs in Jarvis plugins |
| Syncfusion license   | Registration in client `Program.cs` + `wwwroot/index.html` theme bootstrap                                         |

## Primary files to review

- Shell: `Components/Pages/WileyWorkspace.razor`, `Components/Pages/WileyWorkspaceBase.cs`, `Components/Layout/MainLayout.razor`, `NavMenu.razor`
- Panels: `Components/Panels/*.razor`, `Components/QuickBooksImportPanel.razor`, `Components/JarvisChatPanel.razor`
- State: `State/WorkspaceState.cs`
- Styles: `wwwroot/app.css`, `Components/Layout/*.razor.css`, panel-scoped CSS
- Theme: `wwwroot/index.html`, any theme toggle service/wiring in client `Program.cs`

## Suggested implementation slices

### Slice 0 — Audit & plan (deliverable: markdown plan in `.grok/logs/` or update `docs/wileyco-ui-rebuild-plan.md` Phase 6 checklist)

Inventory each panel: control types, load path, a11y gaps, responsive breakpoints, theme token usage. Prioritize P0 (shell/theme/a11y) vs P1 (panel polish) vs P2 (perf micro-opts).

### Slice 1 — Theme system (dark/light)

- Persist theme choice (localStorage pattern consistent with `MainLayout` layout persistence)
- Wire Syncfusion theme CSS swap (Fluent2 light/dark) without breaking license bootstrap
- Remove/adjust `color-scheme: light only` and hardcoded light backgrounds where they block dark mode
- Theme toggle in shell (discoverable, keyboard accessible)

### Slice 2 — Shell & navigation UX

- Left nav collapse, panel titles, breadcrumb/context (enterprise + fiscal year)
- Loading/skeleton states during snapshot bootstrap
- Mobile: sidebar backdrop, focus trap, touch targets
- Consistent page headers and action button placement across panels

### Slice 3 — Panel polish (batch by domain)

**Rates / break-even / scenarios / capital / debt / reserve / affordability / trends / data dashboard**

- Align chart palettes to theme tokens
- Numeric formatting consistency (currency, rates)
- Empty/error states when snapshot slice missing

**QuickBooks import**

- Stepper clarity, preview table readability, validation error surfacing
- Responsive upload + preview on laptop widths

**Customer viewer**

- Grid filter UX, export button prominence (`#export-customer-grid-excel-button`)
- Virtualization/paging if row counts are large

**Jarvis**

- Chat rail theming, conversation labels, degraded/unavailable health messaging

### Slice 4 — Accessibility pass

- Fix missing `aria-label` / `for` associations on forms
- Ensure keyboard navigation for nav and primary actions
- Color contrast in both themes (WCAG AA target for body text)

### Slice 5 — Performance pass

- Reduce unnecessary `StateHasChanged` / re-fetch in `WorkspaceState`
- Lazy panel render where shell already supports it — extend if safe
- Defer heavy chart/grid init until panel visible
- Document any intentional tradeoffs in `docs/wiley-widget-functional-breakdown.md`

### Slice 6 — Tests & docs

- Add/update **bUnit** tests in `tests/WileyCoWeb.ComponentTests/` for theme toggle, layout persistence, critical panel smoke
- Run Playwright smoke/high-risk subset for touched flows:
  ```powershell
  npm run playwright:test:ci:highrisk
  ```
  (Fix failing specs only if your UI change caused regression; do not scope-creep backend.)
- Update `docs/wileyco-ui-rebuild-plan.md` Phase 6 items you complete

## Verification gate (all must pass before claiming done)

```powershell
dotnet build WileyCoWeb.csproj
dotnet test tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

Optional broader proof:

```powershell
dotnet test tests/WileyCoWeb.ComponentTests/WileyCoWeb.ComponentTests.csproj
npx playwright test tests/playwright/workspace-syncfusion-controls.spec.ts --project=chromium
```

## Constraints

- .NET SDK **9.0.313** (`global.json` — do not bump)
- Syncfusion **33.x** — license required for builds
- Minimal diffs — match existing naming and partial-class patterns
- Do not push to `main`; do not commit `temp/`
- Split PR recommendation: theme/shell vs panel batch vs a11y/perf

Summarize: plan artifact path, files changed per slice, tests run with counts, before/after notes for council-facing UX.
