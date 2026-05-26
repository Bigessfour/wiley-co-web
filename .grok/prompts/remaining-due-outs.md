# Wiley Widget — Remaining Due-Outs (Grok CLI)

Complete **actionable remaining items** from the stabilization report, PR #9 follow-ups, and open ui-rebuild plan slices. PR #9 is open on `fix/workspace-api-endpoints-and-tailwind-cdn` — extend this branch; do not push to `main`.

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`, `.grok/logs/wiley-widget-stabilization-report.md` (section 7), `docs/release-notes-stabilization-2026-05.md`, `docs/post-production-operations-handbook.md`, `docs/wileyco-ui-rebuild-plan.md`

**Mandatory:** Plan first (Plan Mode). `read_file` before every `search_replace`. Fix failures before polish. Run gates after each slice. Do not ask for confirmation. Write final summary to `.grok/logs/remaining-due-outs-report.md`.

---

## In scope (implement in this session)

### Slice 1 — Test hygiene (P1, quick win)

- Remove dead/unused code in `tests/WileyCoWeb.IntegrationTests/GlobalExceptionHandlerTests.cs` (`ProductionExceptionTestFactory`, unused `PostImportAsync` helpers if truly unused) OR wire them into meaningful tests — prefer cleanup if redundant with existing coverage.
- Re-run Integration HighRisk after changes.

### Slice 2 — SnapshotComposer perf (P1)

- Review `WileyCoWeb.Api/WorkspaceSnapshotComposer.cs` for SQL-side filtering (UtilityCustomers, rate snapshots) per prior p4-snapshot-perf intent.
- Ensure queries use indexes from migration `20260525204607_SchemaAlignmentProductionReadiness` (AsNoTracking, filtered projections).
- Add or extend HighRisk test in `WorkspaceSnapshotComposerTests` if behavior changes.
- Document perf notes briefly in `docs/wiley-widget-functional-breakdown.md` if query shape changes.

### Slice 3 — Fluent2 dark theme tokens (P1)

- Close gaps in dark mode: `wwwroot/app.css`, `Components/Layout/MainLayout.razor.css`, Syncfusion overrides for panels using hardcoded light colors.
- Ensure `data-wiley-theme` + theme toggle in MainLayout swap Fluent2 dark CSS link in `wwwroot/index.html` (if not already complete).
- Verify contrast on primary panels (nav, hero, grids) in both themes.
- Add minimal bUnit test for theme persistence/toggle if missing.

### Slice 4 — Accessibility pass (P2, high-traffic panels only)

Target **3–4 council-critical panels** (not all 13+): QuickBooks import, Customer viewer, Rates/break-even, Jarvis chat.

- Add/fix `aria-label`, `aria-live` for status toasts, keyboard focus on primary actions.
- Do not break Syncfusion control behavior.

### Slice 5 — Documentation due-outs (P1)

- Copy/adapt stabilization report → `docs/wiley-widget-stabilization-report-2026-05.md` (committed, not only `.grok/logs/`).
- Document `UsedFallback` (internal/chat) vs `latestUsedFallback` (health API) in `docs/wiley-widget-functional-breakdown.md` Jarvis section.
- Update `docs/post-production-operations-handbook.md`: Amplify cutover checklist (env `WILEY_WORKSPACE_API_BASE_ADDRESS` → App Runner), secret rotation pointers, council smoke commands — **ops steps, no secrets**.
- Update `docs/release-notes-stabilization-2026-05.md` with completed due-outs.

### Slice 6 — Playwright coverage gap (P2, minimal)

- Add or fix one HighRisk spec if a council path lacks coverage (check `tests/playwright/` vs stabilization 8-step workflow).
- Prefer extending existing specs over new files.

---

## Out of scope (document only, do not implement)

- AWS console Amplify deploy / App Runner deploy (ops manual)
- Full `src/` archive cull
- OpenTelemetry / X-Ray full wiring
- EF shared model promotion / large migrations
- Pushing to `main`

---

## Verification gate (all must pass)

```powershell
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

If PR #9 CI is failing on GitHub, inspect failure logs (`gh pr checks 9`) and fix blockers on this branch.

---

## Git discipline

- Logical commits per slice (or one commit if small).
- Do not commit `temp/`.
- Summarize in `.grok/logs/remaining-due-outs-report.md`: done vs deferred, test counts, suggested PR comment for #9.
