# WileyCoWeb — AI Briefing

> Generated: 2026-04-22 18:21 | Branch: `main` | Commit: `57043775e3`

## Project Purpose

WileyCoWeb is a Blazor WebAssembly application built with Syncfusion Blazor component suite and MVVM-inspired with dependency injection.

**Important Context**: Files in the `src/` folder are archived legacy code from a previous WinForms version and are **not participating** in the current Blazor application. The active codebase consists of `Components/`, `Services/`, `WileyCoWeb.Api/`, and root-level files.

## Architecture Patterns

- **MVVM-inspired** — MVVM-inspired with dependency injection
- **Syncfusion Blazor component suite** — Syncfusion theme provider
- **Syncfusion dashboard layout and panels** — Syncfusion dashboard layout and panels
- **DI** — Microsoft.Extensions.DependencyInjection
- **Async init** — IAsyncDisposable and initialization patterns

## How to Navigate the Active Codebase

1. `Components/` — Blazor components and pages (entry point: `WileyWorkspace.razor`)
2. `Services/` — Application services and business logic
3. `WileyCoWeb.Api/` — ASP.NET Core API controllers and configuration
4. Root-level files — `Program.cs`, project files, configuration
5. `src/` — **ARCHIVED** (WinForms legacy code, not active)

## Critical Files (read these first)

- `Components/JarvisChatPanel.razor` — Panel — priority 100
- `Components/JarvisChatPanel.razor.cs` — Panel — priority 100
- `Components/QuickBooksImportPanel.razor` — Panel — priority 100
- `Components/Panels/BreakEvenPanel.razor` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.Bindings.cs` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.Helpers.cs` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.Shared.cs` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.razor` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.razor.cs` — Panel — priority 100
- `Components/Panels/DataDashboardPanel.razor` — Panel — priority 100
- `Components/Panels/DecisionSupportPanel.razor` — Panel — priority 100
- `Components/Panels/QuickBooksImportPanelWrapper.razor` — Panel — priority 100
- `Components/Panels/RatesPanel.razor` — Panel — priority 100
- `Components/Panels/ScenarioPlannerPanel.razor` — Panel — priority 100
- `Components/Panels/TrendsPanel.razor` — Panel — priority 100
- `WileyCoWeb.Api/Program.cs` — Program — priority 100
- `Services/BrowserDownloadService.cs` — Service — priority 95
- `Services/QuickBooksImportApiService.cs` — Service — priority 95
- `Services/UtilityCustomerApiService.cs` — Service — priority 95
- `Services/WorkspaceAiApiService.cs` — Service — priority 95

## Recommended Reading Order

1. `Components/JarvisChatPanel.razor`
2. `Components/JarvisChatPanel.razor.cs`
3. `Components/QuickBooksImportPanel.razor`
4. `Components/Panels/BreakEvenPanel.razor`
5. `Components/Panels/CustomerViewerPanel.Bindings.cs`
6. `Components/Panels/CustomerViewerPanel.Helpers.cs`
7. `Components/Panels/CustomerViewerPanel.Shared.cs`
8. `Components/Panels/CustomerViewerPanel.razor`
9. `Components/Panels/CustomerViewerPanel.razor.cs`
10. `Components/Panels/DataDashboardPanel.razor`
11. `Components/Panels/DecisionSupportPanel.razor`
12. `Components/Panels/QuickBooksImportPanelWrapper.razor`
13. `Components/Panels/RatesPanel.razor`
14. `Components/Panels/ScenarioPlannerPanel.razor`
15. `Components/Panels/TrendsPanel.razor`
16. `WileyCoWeb.Api/Program.cs`
17. `Services/BrowserDownloadService.cs`
18. `Services/QuickBooksImportApiService.cs`
19. `Services/UtilityCustomerApiService.cs`
20. `Services/WorkspaceAiApiService.cs`

## Architecture Summary

| Component    | Count |
| ------------ | ----- |
| Views        | 0     |
| Viewmodels   | 1     |
| Panels       | 6     |
| Services     | 47    |
| Controls     | 0     |
| Repositories | 24    |
| Factories    | 3     |

## Key Base Classes

### ComponentBase

- `Components/JarvisChatPanel.razor.cs`
- `Components/Panels/CustomerViewerPanel.razor.cs`
- `Components/Pages/WileyWorkspaceBase.cs`

### OwningComponentBase

### ServiceBase

## Syncfusion Packages

- `Syncfusion.Blazor.Buttons`
- `Syncfusion.Blazor.Calendars`
- `Syncfusion.Blazor.Cards`
- `Syncfusion.Blazor.Charts`
- `Syncfusion.Blazor.CircularGauge`
- `Syncfusion.Blazor.Core`
- `Syncfusion.Blazor.DropDowns`
- `Syncfusion.Blazor.Grid`
- `Syncfusion.Blazor.Inputs`
- `Syncfusion.Blazor.InteractiveChat`
- `Syncfusion.Blazor.Layouts`
- `Syncfusion.Blazor.Navigations`
- `Syncfusion.Blazor.Notifications`
- `Syncfusion.Blazor.Popups`
- `Syncfusion.Blazor.ProgressBar`
- `Syncfusion.Blazor.SfPdfViewer`
- `Syncfusion.Blazor.Spreadsheet`
- `Syncfusion.Blazor.Themes`
- `Syncfusion.Blazor.WordProcessor`
- `Syncfusion.Pdf.Net.Core`
- `Syncfusion.XlsIO.Net.Core`

## ViewModels

- `src/WileyWidget.Abstractions/ILazyLoadViewModel.cs`

## Panels

- `Components/JarvisChatPanel.razor.cs`
- `Components/Panels/CustomerViewerPanel.Bindings.cs`
- `Components/Panels/CustomerViewerPanel.Helpers.cs`
- `Components/Panels/CustomerViewerPanel.Shared.cs`
- `Components/Panels/CustomerViewerPanel.razor.cs`
- `src/WileyWidget.Models/Models/PanelItem.cs`

## Services

- `Services/BrowserDownloadService.cs`
- `Services/QuickBooksImportApiService.cs`
- `Services/UtilityCustomerApiService.cs`
- `Services/WorkspaceAiApiService.cs`
- `Services/WorkspaceBootstrapService.cs`
- `Services/WorkspaceDocumentExportService.cs`
- `Services/WorkspaceKnowledgeApiService.cs`
- `Services/WorkspaceLocalBootstrapService.cs`
- `Services/WorkspacePersistenceService.cs`
- `Services/WorkspaceSnapshotApiService.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Keywords.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Orchestration.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Overrides.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Patterns.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.cs`
- `WileyCoWeb.Api/Configuration/StartupConfigurationService.cs`
- `State/CustomerFilterService.cs`
- `src/WileyWidget.Abstractions/IApplicationStateService.cs`
- `src/WileyWidget.Abstractions/IViewRegistrationService.cs`
- `src/WileyWidget.Business/Interfaces/IDepartmentExpenseService.cs`
- `src/WileyWidget.Business/Interfaces/IGrokRecommendationService.cs`
- `src/WileyWidget.Business/Services/AuditService.cs`
- `src/WileyWidget.Business/Services/GrokRecommendationService.cs`
- `src/WileyWidget.Models/ServiceChargeRecommendation.cs`
- `src/WileyWidget.Services/AdaptiveTimeoutService.cs`
- `src/WileyWidget.Services/AnalyticsService.cs`
- `src/WileyWidget.Services/AnomalyDetectionService.cs`
- `src/WileyWidget.Services/AuditService.cs`
- `src/WileyWidget.Services/DataAnonymizerService.cs`
- `src/WileyWidget.Services/ErrorReportingService.cs`

## Controls

## Key NuGet Dependencies

## Manifest Stats

- Total files indexed: **356**
- Files with embedded content: **355**
- Total source size: **38,484 KB**
- Manifest mode: **full-context**

---

> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.
