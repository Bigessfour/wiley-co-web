# Left Nav + Fluent2 UI Polish (Grok CLI)

Fix **every visual issue** with the Wiley Widget workspace shell: left navigation, control backgrounds, Syncfusion Fluent2 theme inheritance, spacing/box model, and dark/light consistency.

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`, `docs/wileyco-ui-rebuild-plan.md` (Phase 6)

**Branch:** `fix/workspace-api-endpoints-and-tailwind-cdn` (PR #9). Do not push to `main`.

**Mandatory:** Plan first. `read_file` before every `search_replace`. Run focused tests after changes. Update release notes. Do not ask for confirmation.

---

## Scope — review and fix ALL attached UI files

### Shell & navigation (priority P0)

- `Components/Pages/WileyWorkspace.razor`
- `Components/Layout/MainLayout.razor` + `MainLayout.razor.css`
- `Components/Layout/NavMenu.razor` + `NavMenu.razor.css`
- `wwwroot/index.html` (Fluent2 CSS link swap for dark/light)
- `wwwroot/app.css` (theme tokens, overrides)

### Panels (fix control backgrounds + spacing where shell theme bleeds)

- `Components/Panels/*.razor` (all panels)
- `Components/QuickBooksImportPanel.razor`
- `Components/JarvisChatPanel.razor`

---

## Requirements

### Left navigation panel

- Fix **every** visual defect: width, padding, active/hover states, icon alignment, scroll overflow, z-index, mobile backdrop
- **Smooth collapse/expand** (SfSidebar dock/push/over + nav toggle); no layout jump or clipped content
- Make left nav look **native** to Fluent2 (not bolted-on Bootstrap/slate)

### Theme (dark/light)

- Use **modern Fluent2 tokens** via CSS variables / `data-wiley-theme` / `data-bs-theme`
- **Remove hard-coded slate/lightyellow/light-only colors** where possible (especially `MainLayout.razor.css`, panel wrappers)
- Ensure **dark/light mode consistency** across nav, hero, panel chrome, Syncfusion controls (SfGrid, SfChart, SfSidebar, SfAppBar, inputs)
- Theme toggle in MainLayout must swap Fluent2 stylesheet correctly

### Control backgrounds

- Fix mismatched backgrounds **behind all controls** (inputs, grids, cards, steppers, chat rail)
- Syncfusion controls must inherit Fluent2 surface colors in both themes — no white boxes in dark mode or gray slabs in light mode

### Spacing / box model

- Consistent padding, margins, border-radius, gap between nav items and panel headers
- Fix overflow, double borders, misaligned flex/grid in shell + high-traffic panels

---

## Canonical guardrails (do not regress)

| Area               | Keep                                                              |
| ------------------ | ----------------------------------------------------------------- |
| Customer export    | `#export-customer-grid-excel-button` + XlsIO                      |
| Playwright nav     | `gotoWorkspacePanel` from `tests/playwright/support/workspace.ts` |
| Syncfusion license | `wwwroot/index.html` + client `Program.cs` registration           |
| Panel IDs/routes   | Existing workspace panel routing in `WileyWorkspace.razor`        |

---

## Verification

```powershell
dotnet build WileyCoWeb.csproj
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

Manual: toggle dark/light; collapse/expand left nav on desktop + mobile widths; spot-check Rates, QuickBooks, Customer viewer panels.

---

## Documentation

Update `docs/release-notes-stabilization-2026-05.md` (or add `docs/release-notes-ui-polish-2026-05.md`) with UI changes summary.

Summarize files changed in `.grok/logs/left-nav-fluent2-ui-polish-report.md`.

Logical commits. No `temp/`.
