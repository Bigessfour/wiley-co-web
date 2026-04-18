# Legacy Service Cull List

This document tracks the dead-service batches already removed from the post-WinForms Blazor/AWS codebase and the smaller leftovers still worth reviewing.

## Removed Earlier

- `src/WileyWidget.Services/ActivityFallbackDataService.cs`
- `src/WileyWidget.Services/ChatBridgeService.cs`
- `src/WileyWidget.Services/DashboardService.cs`
- `src/WileyWidget.Services/DataPrefetchService.cs`
- `src/WileyWidget.Services/FallbackDataService.cs`
- `src/WileyWidget.Services/HealthBasedRoutingService.cs`
- `src/WileyWidget.Services/HealthCheckService.cs`
- `src/WileyWidget.Services/LocalizationService.cs`
- `src/WileyWidget.Services/SemanticSearchService.cs`
- `src/WileyWidget.Services.Abstractions/IChatBridgeService.cs`
- `src/WileyWidget.Services.Abstractions/IDashboardService.cs`
- `src/WileyWidget.Services.Abstractions/ISemanticSearchService.cs`
- `src/WileyWidget.Services/GlobalSearchService.cs`

## Removed In This Pass

- Legacy DI scaffolding:
  - `src/WileyWidget.Services/DependencyInjection/WileyWidgetServicesExtensions.cs`
  - `src/WileyWidget.Services/DiValidationService.cs`
  - `src/WileyWidget.Services.Abstractions/IDiValidationService.cs`
  - `src/WileyWidget.Services/Services/AppEventBus.cs`
  - `src/WileyWidget.Services.Abstractions/IAppEventBus.cs`
  - `src/WileyWidget.Services/Services/FileImportService.cs`
  - `src/WileyWidget.Services.Abstractions/IFileImportService.cs`
  - `src/WileyWidget.Services/Services/UserPreferencesService.cs`
  - `tests/WileyWidget.Tests/AppEventBusTests.cs`
  - `tests/WileyWidget.Tests/FileImportServiceTests.cs`
  - `tests/WileyWidget.Tests/FileImportServiceEdgeCaseTests.cs`
  - `tests/WileyWidget.Tests/DiValidationTests.cs`

- Test-only or isolated utility services:
  - `src/WileyWidget.Services/SettingsService.cs`
  - `src/WileyWidget.Services.Abstractions/ISettingsService.cs`
  - `src/WileyWidget.Services/ReportExportService.cs`
  - `src/WileyWidget.Services.Abstractions/IReportExportService.cs`
  - `src/WileyWidget.Services/CsvExcelImportService.cs`
  - `src/WileyWidget.Services/BudgetImporter.cs`
  - `src/WileyWidget.Services.Abstractions/IBudgetImporter.cs`
  - `src/WileyWidget.Services/Excel/ExcelReaderService.cs`
  - `src/WileyWidget.Services/Excel/IExcelReaderService.cs`
  - `tests/WileyWidget.Tests/SettingsServiceTests.cs`
  - `tests/WileyWidget.Tests/ReportExportServiceTests.cs`
  - `tests/WileyWidget.Tests/CsvExcelImportServiceTests.cs`

- Legacy AI stack and AI-only contracts:
  - `src/WileyWidget.Services/AICacheWarmingService.cs`
  - `src/WileyWidget.Services/AILoggingService.cs`
  - `src/WileyWidget.Services/AnalyticsPipeline.cs`
  - `src/WileyWidget.Services/CorrelationIdService.cs`
  - `src/WileyWidget.Services/GrokSupercomputer.cs`
  - `src/WileyWidget.Services/JARVISPersonalityService.cs`
  - `src/WileyWidget.Services/NullAIService.cs`
  - `src/WileyWidget.Services/NullGrokSupercomputer.cs`
  - `src/WileyWidget.Services/XAIService.cs`
  - `src/WileyWidget.Services/XaiModelDiscoveryService.cs`
  - `src/WileyWidget.Services.Abstractions/IAIService.cs`
  - `src/WileyWidget.Services.Abstractions/IAILoggingService.cs`
  - `src/WileyWidget.Services.Abstractions/IAnalyticsPipeline.cs`
  - `src/WileyWidget.Services.Abstractions/IGrokSupercomputer.cs`
  - `src/WileyWidget.Services.Abstractions/IJARVISPersonalityService.cs`
  - `src/WileyWidget.Services.Abstractions/IXaiModelDiscoveryService.cs`
  - `src/WileyWidget.Services.Abstractions/XaiModelDescriptor.cs`

- Cache, local-secret, and telemetry leftovers:
  - `src/WileyWidget.Services/CacheServiceCollectionExtensions.cs`
  - `src/WileyWidget.Services/MemoryCacheService.cs`
  - `src/WileyWidget.Services/InMemoryCacheService.cs`
  - `src/WileyWidget.Services/DistributedCacheService.cs`
  - `src/WileyWidget.Services/LocalSecretVaultService.cs`
  - `src/WileyWidget.Services/EncryptedLocalSecretVaultService.cs`
  - `src/WileyWidget.Services/TelemetryStartupService.cs`
  - `src/WileyWidget.Services/SigNozTelemetryService.cs`
  - `src/WileyWidget.Services/TelemetryLogService.cs`
  - `src/WileyWidget.Services/ApplicationMetricsService.cs`
  - `src/WileyWidget.Services/Telemetry/ApplicationMetricsService.cs`
  - `src/WileyWidget.Abstractions/ICacheService.cs`
  - `src/WileyWidget.Services.Abstractions/ISecretVaultService.cs`
  - `src/WileyWidget.Services.Abstractions/ITelemetryLogService.cs`
  - `tests/WileyWidget.Tests/MemoryCacheServiceTests.cs`
  - `tests/WileyWidget.Tests/TelemetryLogServiceTests.cs`

Why this pass was safe:

- The legacy DI scaffold was only rooted by `WileyWidgetServicesExtensions.cs` and the now-deleted tests; the shipped Blazor client and API do not call that extension.
- The settings, export, CSV/Excel import, budget importer, and workbook-reader services were only referenced by their own implementations or isolated tests.
- The legacy AI graph stayed inside deleted implementations and AI-only abstractions; active runtime chat and knowledge flows are handled by `WorkspaceAiAssistantService`, `WorkspaceKnowledgeService`, and the current API/browser services instead.
- `AIServiceInterfaces.cs` stays because it still contains active conversation-history and Grok API-key contracts used by the shipped assistant path.
- The cache and local-secret implementations were fully self-contained: `ICacheService` only pointed at the deleted cache wrappers and registration helper, and `ISecretVaultService` only pointed at the deleted vault implementations.
- The telemetry startup cluster was also self-contained: `TelemetryStartupService`, both `ApplicationMetricsService` variants, `TelemetryLogService`, and `SigNozTelemetryService` had no production callers or registrations, while the live API only keeps the lightweight `ITelemetryService` seam on `BudgetRepository`.

## Remaining Candidates

- None currently verified. `PasswordBoxHelper.cs` was an empty file already excluded from compilation by `src/WileyWidget.Services/WileyWidget.Services.csproj`, so it has now been removed as dead residue.

## Notes

- The VS Code dead-code tasks in `.vscode/tasks.json` point at missing scripts under `tools/`, so this cull list was produced from direct symbol-usage analysis instead of the missing helper tasks.
- The current shipped runtime is centered on the Blazor client service set in `Program.cs` and the API registrations in `WileyCoWeb.Api/Program.cs`; anything not rooted there needs explicit proof to stay.