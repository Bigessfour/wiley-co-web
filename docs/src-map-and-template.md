# Src Map and Prebuilt Template

## Purpose

This file maps the current `src` folder in `wiley-co-web` and turns it into a reusable template for the pieces that are already built.

## Top-Level `src` Map

- `WileyWidget.Abstractions/` - cross-cutting contracts and shared result types.
- `WileyWidget.Business/` - core calculation and domain services.
- `WileyWidget.Data/` - EF Core context, repositories, migrations, and persistence infrastructure.
- `WileyWidget.Models/` - POCOs, DTOs, validators, attributes, and domain models.
- `WileyWidget.Services/` - implementation layer for AI, QuickBooks, reporting, telemetry, import, and app orchestration.
- `WileyWidget.Services.Abstractions/` - service contracts and AI / QuickBooks / telemetry options.

## Project Breakdown

### WileyWidget.Abstractions

Reusable primitives used across the solution.

- `IApplicationStateService`
- `IAsyncInitializable`
- `ICacheService`
- `IErrorHandler`
- `IExceptionHandler`
- `ILazyLoadViewModel`
- `IResourceLoader`
- `IStartupProgressReporter`
- `IViewRegistrationService`
- `Result`

### WileyWidget.Business

Domain and workflow logic.

- `Configuration/` for business settings and validation support.
- `Interfaces/` for repository and service contracts.
- `Models/` for aggregates such as `MonthlyRevenueAggregate`.
- `Services/` for calculation engines and workflow services.
- Notable services include `QuickBooksBudgetSyncService`, `GrokRecommendationService`, `DepartmentExpenseService`, and `AuditService`.

### WileyWidget.Data

Persistence and database access.

- `AppDbContext` and factory helpers.
- Repository layer for `Budget`, `Analytics`, `Accounts`, `Departments`, `Enterprises`, `MunicipalAccount`, `Payments`, `UtilityBill`, `UtilityCustomer`, `Vendor`, and `ScenarioSnapshot`.
- `Interceptors/` for audit and resilience.
- `Migrations/` for the current EF Core schema history.
- `Resilience/` for database retry and policy logic.

### WileyWidget.Models

Shared domain types and DTOs.

- `Models/` contains the bulk of the POCOs for budgets, finance, chat, telemetry, navigation, and QuickBooks support.
- `Validators/` contains model validation rules.
- `DTOs/` contains data-transfer shapes.
- `Entities/` and `Interfaces/` contain shared entity contracts.
- `Attributes/` holds display metadata used across the app.

### WileyWidget.Services

Implementation layer for runtime behavior.

- AI and chat support: `XAIService`, `GrokSupercomputer`, `JARVISPersonalityService`, `ChatBridgeService`, `SemanticSearchService`, `NullAIService`, `NullGrokSupercomputer`.
- QuickBooks support: `QuickBooksService`, `QuickBooksDesktopImportService`, `QuickBooksDesktopIifParser`, `QuickBooksAuthService`, `QuickBooksApiClient`, `QuickBooksAccountService`, `QuickBooksChartOfAccountsService`, `QuickBooksCompanyInfoService`, `QuickBooksSandboxSeederService`.
- Reporting and exports: `FastReportService`, `FastReportBudgetExtensions`, `ReportExportService`, `Export/ExcelExportService`.
- Import and file handling: `BudgetImporter`, `CsvExcelImportService`, `FileSecurityHelper`, `Excel/ExcelReaderService`.
- Telemetry and ops: `TelemetryLogService`, `TelemetryStartupService`, `ApplicationMetricsService`, `SigNozTelemetryService`, `HealthCheckService`, `HealthBasedRoutingService`, `ErrorReportingService`.
- UI support and orchestration: `DashboardService`, `SettingsService`, `UserContext`, `LocalizationService`, `WileyWidgetContextService`, `DataPrefetchService`, `FallbackDataService`.
- DI and infrastructure: `DependencyInjection/`, `CacheServiceCollectionExtensions`, `DistributedCacheService`, `MemoryCacheService`, `InMemoryCacheService`, `EncryptedLocalSecretVaultService`, `LocalSecretVaultService`, `CorrelationIdService`.

### WileyWidget.Services.Abstractions

Contracts used by the runtime service layer.

- AI and model contracts: `IAIService`, `IXaiModelDiscoveryService`, `IGrokSupercomputer`, `IJARVISPersonalityService`, `XaiModelDescriptor`, `TokenResult`.
- QuickBooks contracts: `IQuickBooksService`, `IQuickBooksDesktopImportService`, `IQuickBooksAuthService`, `IQuickBooksApiClient`, `IQuickBooksAccountService`, `IQuickBooksChartOfAccountsService`, `IQuickBooksCompanyInfoService`, `IQuickBooksDataService`, `IQuickBooksSandboxSeederService`.
- Budget and analytics contracts: `IAnalyticsService`, `IAnalyticsRepository`, `IAnalyticsPipeline`, `IBudgetAnalyticsRepository`, `IBudgetImporter`, `IChargeCalculatorService`, `IDashboardService`.
- Platform and infrastructure contracts: `IAppEventBus`, `ICircuitBreakerService`, `IDataAnonymizerService`, `IDiValidationService`, `IDispatcherHelper`, `IFileImportService`, `IMemoryProfiler`, `IReportService`, `IReportExportService`, `ISecretVaultService`, `ISemanticSearchService`, `ISettingsService`, `ITelemetryService`, `ITelemetryLogService`, `IUserContext`.
- QuickBooks auth options: `QuickBooksOAuthOptions`, `QuickBooksOAuthToken`, `QuickBooksTokenResponse`.

Standalone webhook host.

- `Program.cs`
- `wwwroot/pages/` for static pages like privacy, launch, EULA, disconnect, and index.
- `appsettings.json`

## Prebuilt Template Summary

### Already reusable

- Domain and finance logic is already split out into `Business`.
- Data access is already isolated in `Data` with EF Core and migrations.
- Shared contracts already exist in `Abstractions` and `Services.Abstractions`.
- The app already has AI, Grok, QuickBooks, telemetry, reporting, and budgeting services.
- The models layer already covers most of the finance, chat, and dashboard data shapes needed for a Blazor migration.

### Best candidates to carry forward first

1. `WileyWidget.Models/`
2. `WileyWidget.Business/`
3. `WileyWidget.Services.Abstractions/`
4. `WileyWidget.Abstractions/`
5. `WileyWidget.Data/` after database target alignment
6. Select services from `WileyWidget.Services/` once UI dependencies are removed

### Likely rewrite areas

- WinForms UI and docking shells are not inside `src` here, but they should be treated as separate migration work.
- Reporting is still tied to `FastReport` and needs a web-friendly replacement path.
- Chat and Grok integration should be surfaced through a web API or server-backed service boundary for the Blazor app.

## Suggested Blazor Migration Template

- Shared domain and contract library
- Data access library or API-backed persistence layer
- Syncfusion component library for reusable dashboards, forms, charts, and grids
- Blazor Web App shell with layout, navigation, and page-level routes
- Server-side service layer for QuickBooks, reporting, and AI calls

## Notes

- This map intentionally ignores generated folders such as `bin`, `obj`, `bin_validate`, and `obj_validate`.
- The root Blazor app lives outside `src` in the repository root under `Components/`, `Program.cs`, and `wwwroot/`.
