# Backend Production Readiness — Grok CLI implementation task

You are implementing the Wiley Widget backend production readiness plan in this repository.

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md` (if present). The full checklist is in this prompt file — do NOT edit any plan file.

**Toolchain:** .NET SDK 9.0.313 (`global.json`), Syncfusion Blazor 33.x, PostgreSQL/Aurora via EF Core.

**Canonical patterns (non-negotiable):**

- Rate math: `src/WileyWidget.Abstractions/EnterpriseRateService.cs` only; `State/RateCalculator.cs` is thin forwarder
- QuickBooks duplicate key: `(canonical_entity, file_hash)`
- Jarvis: `/api/ai/*`, health field `latestUsedFallback`
- Customer export: XlsIO via `#export-customer-grid-excel-button`, not SfGrid toolbar export

## Execution rules

1. Work through ALL todos below in order. Mark each complete only after tests pass for that slice.
2. Minimal diffs — match existing patterns; no drive-by refactors.
3. Do not edit the plan file itself.
4. Do not push to `main`. Do not commit secrets.
5. Update `docs/` only when behavior or ops change (documentation proximity rule).
6. After each phase, run the smallest HighRisk test set that covers your blast radius.

## Todo checklist (complete all)

### Phase 0 — CI stabilization

- [ ] **p0-playwright-green:** Fix failing Playwright HighRisk specs (`customer-export`, `decision-support-production-ready`, `quickbooks-import-duplicate-preview`). Verify:
  ```powershell
  dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
  dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
  dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
  npm run playwright:test:ci:highrisk
  ```

### Phase 1 — Municipal finance correctness

- [ ] **p1-rate-quadrant-fix:** Fix `WorkspaceState.RecalculateBreakEvenQuadrant` to use per-quadrant expenses/volume. Update `ComponentPageTests` (current test encodes bug). Add regression: editing Water costs leaves Sewer unchanged.
- [ ] **p1-rounding-policy:** Standardize `roundToCurrency` — stored baselines full precision; align `WorkspaceSnapshotComposer`, `WorkspaceKnowledgeService`, `WorkspaceSnapshotExportArchiveService`, `RateCalculator`. Document in `docs/wiley-widget-functional-breakdown.md`.
- [ ] **p1-scenario-kpis:** When scenario items active, dashboard `RateAdequacy` and net-position use `AdjustedRecommendedRate`. Add `DataDashboardPanelTests`.
- [ ] **p1-rate-consolidation:** Route or isolate `AnalyticsService.RunRateScenarioAsync`; redirect `Models/Enterprise.cs` rate helpers to `EnterpriseRateService`; fix `CalculateRateAdjustmentForTarget` to use `EffectiveCustomerCount`.
- [ ] Add cross-path tests in `EnterpriseRateServiceTests`: adjusted costs/delta, zero volume revenue, parity vectors.

### Phase 2 — Data layer hardening

- [ ] **p2-ef-fk-behavior:** Remove or narrow `AppDbContext` global FK Restrict loop (lines ~772–776). Add delete-behavior integration test.
- [ ] **p2-schema-migration:** Corrective migration for Charge dual FK, BudgetInteraction orphaned EnterpriseId, Transaction.MunicipalAccountId model alignment.
- [ ] **p2-audit-interceptor:** Fix nested SaveChanges in `AuditInterceptor` (separate DbContext or queue). Add integration test.
- [ ] **p2-budget-enterprise-scope:** Short-term (B): enterprise-scoped budget methods return 501 or throw NotSupported — stop ignoring `enterpriseId`.
- [ ] **p2-repo-lifecycle:** Fix undisposed DbContext in repositories; wire cache invalidation; remove debug `Console.WriteLine` in BudgetRepository.
- [ ] Add indexes: AuditEntries, LedgerEntries, BudgetEntries.SourceFilePath.

### Phase 3 — API security & resilience

- [ ] **p3-api-auth:** Add JWT auth scaffolding (config-driven Cognito/JWT); gate mutating routes and export downloads; stop trusting raw `X-Wiley-*` headers without verified claims.
- [ ] **p3-prod-fallbacks:** Disable synthetic fallbacks in production `appsettings.json`; return 503 ProblemDetails. Keep synthetic in Development + test factory. Update `WorkspacePanelFinancialEndpointsTests`.
- [ ] **p3-qb-atomic:** Wrap QuickBooks commit in transaction; add 50MB upload size limit.
- [ ] **p3-exception-handling:** Add global ProblemDetails + typed domain exception mapping.
- [ ] Extend `JarvisHealthState` for degraded/unavailable; update `JarvisHealthApiTests`.

### Phase 4 — Performance & tests

- [ ] **p4-snapshot-perf:** Filter customers/snapshots in SQL in `WorkspaceSnapshotComposer`; add EnterpriseId FK on UtilityCustomers if needed.
- [ ] **p4-highrisk-expansion:** Add HighRisk traits to snapshot, knowledge, financial endpoint tests; cross-path rate consistency test.
- [ ] **p4-postgres-tests:** Testcontainers PostgreSQL fixture for unique constraints and migration smoke; add CI job.
- [ ] Add `WorkspaceSnapshotComposer` unit tests (FY resolution, apartments count, empty enterprise error).

### Phase 5 — Ops evidence (repo artifacts)

- [ ] **p5-ops-signoff:** Update `docs/release-record-template.md` (`latestUsedFallback` not `UsedFallback`); add `docs/backend-production-signoff-checklist.md` with Jarvis prod validation steps and App Runner smoke script references. Do NOT commit live secrets or production credentials.

## Verification gate (run before finishing)

All must pass:

```powershell
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

When complete, summarize: files changed, tests run, remaining risks, and recommended PR split.
