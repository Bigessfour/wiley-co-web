# Backend Production Readiness — REMAINING work (Grok CLI)

You are completing the **remaining** Wiley Widget backend production readiness todos. Prior work may already exist in the working tree — verify before re-implementing.

**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`

**Mandatory:** Implement all items below. Run tests after each slice. Do not ask for confirmation.

## Already done (verify only — do not redo unless broken)

- p1-rate-quadrant-fix, p1-rounding-policy, p1-scenario-kpis (partial — add tests if missing)
- p2-ef-fk-behavior, p2-audit-interceptor, p2-budget-enterprise-scope (NotSupportedException)
- p3-prod-fallbacks, p3-qb-atomic (transaction + 50MB Kestrel limit), JarvisHealth degraded/unavailable
- p5-ops-signoff (release template + backend-production-signoff-checklist.md)

## Remaining todos — complete ALL

### 1. p0-playwright-green

Fix and green:

- `tests/playwright/customer-export.spec.ts`
- `tests/playwright/decision-support-production-ready.spec.ts`
- `tests/playwright/quickbooks-import-duplicate-preview.spec.ts`

Ensure CI publish path works: client `publish_output/wwwroot`, API `api_output/`. Run:

```powershell
npm run playwright:test:ci:highrisk
```

### 2. p1-rate-consolidation

- Route or isolate `AnalyticsService.RunRateScenarioAsync` from council-facing Jarvis guidance
- Redirect `Models/Enterprise.cs` rate helpers to `EnterpriseRateService`
- Fix `CalculateRateAdjustmentForTarget` to use `EffectiveCustomerCount`
- Add cross-path tests in `EnterpriseRateServiceTests` (adjusted costs/delta, zero volume, parity vectors)
- Document rounding policy in `docs/wiley-widget-functional-breakdown.md` if not present
- Add `DataDashboardPanelTests` for scenario-adjusted KPIs if missing

### 3. p2-schema-migration

Create corrective EF migration for:

- Charge: reconcile BillId vs UtilityBillId
- BudgetInteraction: orphaned EnterpriseId column
- Transaction.MunicipalAccountId model alignment
  Add indexes: AuditEntries (Timestamp), LedgerEntries (EntryDate, EntryScope), BudgetEntries (SourceFilePath)

### 4. p2-repo-lifecycle

- Fix undisposed DbContext in EnterpriseRepository, MunicipalAccountRepository
- Wire BudgetAnalyticsRepository.InvalidateCache on budget writes
- Remove debug Console.WriteLine in BudgetRepository.GetTownOfWileyBudgetDataAsync
- Add delete-behavior integration test for FK cascade (if AuditInterceptor registered in DI, ensure IDbContextFactory injected)

### 5. p3-api-auth

Add config-driven JWT auth scaffolding:

- `Authentication:Jwt` section in appsettings
- When enabled: `AddAuthentication().AddJwtBearer()`, `RequireAuthorization()` on mutating routes + export downloads
- Derive user identity from JWT claims; ignore raw `X-Wiley-*` unless auth disabled (Development)
- Document in ops handbook

### 6. p3-exception-handling

- Add `AddProblemDetails()` + global exception handler
- Map domain exceptions to typed HTTP responses
- Update ExceptionLoggingMiddleware to not only rethrow

### 7. p4-snapshot-perf

- Filter UtilityCustomers and rate snapshots in SQL in WorkspaceSnapshotComposer
- Add EnterpriseId FK on UtilityCustomers if missing

### 8. p4-highrisk-expansion

Add `[Trait("Category", "HighRisk")]` to key tests in:

- WorkspaceSnapshotApiTests
- WorkspaceKnowledgeApiTests
- WorkspacePanelFinancialEndpointsTests (503 when fallbacks disabled)

### 9. p4-postgres-tests

- Add Testcontainers PostgreSQL fixture (optional CI job)
- Migration smoke + QuickBooks unique constraint test

### 10. WorkspaceSnapshotComposer unit tests

Direct tests: FY resolution, apartments EffectiveCustomerCount, empty enterprise handling

## Final verification (all must pass)

```powershell
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
npm run playwright:test:ci:highrisk
```

Summarize: files changed, tests run, PR split recommendation.
