# Backend Deep Refinement — Wiley Widget (Grok CLI)

You are completing a comprehensive backend reliability, security, and performance refinement pass.
**Read first:** `AGENTS.md`, `.cursor/skills/wiley-widget-completion/SKILL.md`, `.grok/prompts/backend-production-readiness-remaining.md`

**Mandatory:** Implement all slices below in order. Run the smallest relevant test gate after each slice. Do not ask for confirmation between todos. Verify the working tree before re-implementing anything marked "verify only".

## Baseline (already green — verify, do not regress)

- HighRisk unit: 17 passed (`WileyWidget.Tests`)
- HighRisk integration: 30 passed (`WileyCoWeb.IntegrationTests`)
- Rate consolidation in `EnterpriseRateService`; `RateCalculator` is forwarder-only
- QuickBooks duplicate guard: `(canonical_entity, file_hash)` + semantic overlap + DB unique index
- JWT mutating auth scaffolding exists (`JwtAuthenticationExtensions`, `WorkspaceEndpointAuthorizationExtensions`)
- `GlobalExceptionHandler` + `AddProblemDetails()` registered
- Schema alignment migration `20260525204607_SchemaAlignmentProductionReadiness`

---

## Slice 1 — Error handling & security (P0)

### 1a. Sanitize 500 ProblemDetails in production

- `GlobalExceptionHandler`: in non-Development, use generic `Detail` for unhandled exceptions (log full exception server-side only)
- Map `DbUpdateException` with Postgres unique violation (`23505`) → 409 Conflict with safe detail (QuickBooks concurrent duplicate commit)
- Prefer typed `DuplicateImportException` over string-matching `IsDuplicateImport` where feasible (minimal diff)

### 1b. GlobalExceptionHandler integration tests

Add `tests/WileyCoWeb.IntegrationTests/GlobalExceptionHandlerTests.cs` with `[Trait("Category", "HighRisk")]`:

- ConcurrencyConflictException → 409 + entityName extension
- ArgumentException → 400
- Duplicate import (InvalidOperationException or typed) → 409
- Unhandled exception → 500 with sanitized detail in Production test host config

### 1c. Read-path auth when JWT enabled

- Extend `WorkspaceEndpointAuthorizationExtensions` with `RequireWorkspaceReadAuth` policy
- When `Authentication:Jwt:Enabled`, protect reads: snapshot GET, knowledge POST, export list/download, QuickBooks routing/history GET
- Keep Development open when JWT disabled (existing pattern)
- Add integration tests proving 401 without token when JWT enabled in test factory
- Document in `docs/post-production-operations-handbook.md`

---

## Slice 2 — QuickBooks reliability (P1)

### 2a. Concurrent duplicate commit

- Ensure second concurrent commit on same file hash returns 409 (not 500) via DbUpdateException mapping
- Add HighRisk integration test: two sequential commits of identical bytes (different filenames) — second must be 409

### 2b. DB-side duplicate detection perf

- Refactor `AnalyzeRoutedDuplicatesAsync` to avoid loading all ledger entries into memory
- Use indexed DB query or signature hash column if migration is small; otherwise bounded query with documented tradeoff

### 2c. Structural validation

- Add proactive limits in QuickBooks preview: max row count, amount bounds, allowed enterprise names
- Return 400 ValidationProblem with field errors (consistent shape)

---

## Slice 3 — Rate calculations & reporting (P1–P2)

### 3a. Verify rate path isolation (verify only unless broken)

- Confirm `DataReportingPlugin` does NOT expose council break-even rate scenario to Jarvis
- Confirm `AnalyticsService.RunRateScenarioAsync` is portfolio what-if only
- Add test enforcing Jarvis plugin surface excludes rate scenario if missing

### 3b. Composer ↔ EnterpriseRateService parity

- Add HighRisk assertion in `WorkspaceSnapshotComposerTests`: seeded enterprise recommended rate matches `EnterpriseRateService` for known inputs (55.25 / 13250 / 240 Wiley demo)

### 3c. Export workbook rate parity

- Add unit test in `WileyWidget.Tests` for `WorkspaceSnapshotExportArchiveService`: scenario workbook rate cells match `EnterpriseRateService`

### 3d. Snapshot export performance guard

- Add configurable max artifact size / timeout guard on POST exports
- Return 413 or 503 with clear ProblemDetails when exceeded (don't block App Runner indefinitely)
- Document limit in ops handbook

---

## Slice 4 — Data validation & repository lifecycle (P2)

### 4a. Wire or remove FluentValidation

- Either register `EnterpriseValidator` / `BudgetDataValidator` in API pipeline OR document why inline rules suffice and remove dead validators (minimal diff preferred: wire for enterprise/budget writes if low cost)

### 4b. Repository lifecycle (verify/implement)

- Fix undisposed DbContext in `EnterpriseRepository`, `MunicipalAccountRepository` if still present
- Wire `BudgetAnalyticsRepository.InvalidateCache` on budget writes
- Remove debug `Console.WriteLine` in `BudgetRepository.GetTownOfWileyBudgetDataAsync`
- Ensure `EntityDeleteBehaviorIntegrationTests` is HighRisk and passes

### 4c. Baseline validation integration tests

- Add tests for PUT `/api/workspace/baseline` validation rules (negative rate, zero volume) → 400

---

## Slice 5 — Async/background cleanup (P2)

### 5a. Orphan hosted services

- `TelemetryStartupService`, `AICacheWarmingService`: either register behind config flags in `Program.cs` OR remove if unused (prefer register-with-flag if ops expects them)
- If keeping `AICacheWarmingService`, replace fire-and-forget `Task.Run` with proper `BackgroundService` + cancellation

### 5b. Document sync export path

- Add brief note in `docs/wiley-widget-functional-breakdown.md`: snapshot exports are synchronous in-request; async queue is future work

---

## Slice 6 — Remaining production-readiness todos

Complete any still-open items from `.grok/prompts/backend-production-readiness-remaining.md`:

- p2-schema-migration (if migration incomplete in working tree)
- p4-snapshot-perf (SQL filter in WorkspaceSnapshotComposer)
- p4-highrisk-expansion
- p4-postgres-tests (optional Testcontainers)
- p0-playwright-green (only if time permits after backend slices)

---

## Final verification gate (all must pass)

```powershell
dotnet build WileyCoWeb.csproj
dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj
dotnet test tests/WileyCoWeb.ComponentTests --filter "Category=HighRisk"
dotnet test tests/WileyCoWeb.IntegrationTests --filter "Category=HighRisk"
dotnet test tests/WileyWidget.Tests --filter "Category=HighRisk"
```

Summarize: files changed per slice, tests run, PR split recommendation (separate PRs for auth vs error-handling vs QuickBooks vs rates).
