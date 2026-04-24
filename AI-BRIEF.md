# WileyCoWeb — AI Briefing

> Generated: 2026-04-24 11:20 | Branch: `feature/break-even-4-enterprises-dataviz` | Commit: `73c2f0bbd2`

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
- `Components/Panels/AffordabilityDashboardPanel.razor` — Panel — priority 100
- `Components/Panels/ApartmentConfigPanel.razor` — Panel — priority 100
- `Components/Panels/BreakEvenPanel.razor` — Panel — priority 100
- `Components/Panels/BreakEvenPanel.razor.cs` — Panel — priority 100
- `Components/Panels/CapitalGapPanel.razor` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.Bindings.cs` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.Helpers.cs` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.Shared.cs` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.razor` — Panel — priority 100
- `Components/Panels/DataDashboardPanel.razor` — Panel — priority 100
- `Components/Panels/CustomerViewerPanel.razor.cs` — Panel — priority 100
- `Components/Panels/DebtCoveragePanel.razor` — Panel — priority 100
- `Components/Panels/DecisionSupportPanel.razor` — Panel — priority 100
- `Components/Panels/RatesPanel.razor` — Panel — priority 100
- `Components/Panels/QuickBooksImportPanelWrapper.razor` — Panel — priority 100
- `Components/Panels/ReserveTrajectoryPanel.razor` — Panel — priority 100
- `Components/Panels/ScenarioPlannerPanel.razor` — Panel — priority 100

## Recommended Reading Order

1. `Components/JarvisChatPanel.razor`
2. `Components/JarvisChatPanel.razor.cs`
3. `Components/QuickBooksImportPanel.razor`
4. `Components/Panels/AffordabilityDashboardPanel.razor`
5. `Components/Panels/ApartmentConfigPanel.razor`
6. `Components/Panels/BreakEvenPanel.razor`
7. `Components/Panels/BreakEvenPanel.razor.cs`
8. `Components/Panels/CapitalGapPanel.razor`
9. `Components/Panels/CustomerViewerPanel.Bindings.cs`
10. `Components/Panels/CustomerViewerPanel.Helpers.cs`
11. `Components/Panels/CustomerViewerPanel.Shared.cs`
12. `Components/Panels/CustomerViewerPanel.razor`
13. `Components/Panels/DataDashboardPanel.razor`
14. `Components/Panels/CustomerViewerPanel.razor.cs`
15. `Components/Panels/DebtCoveragePanel.razor`
16. `Components/Panels/DecisionSupportPanel.razor`
17. `Components/Panels/RatesPanel.razor`
18. `Components/Panels/QuickBooksImportPanelWrapper.razor`
19. `Components/Panels/ReserveTrajectoryPanel.razor`
20. `Components/Panels/ScenarioPlannerPanel.razor`

## Architecture Summary

| Component    | Count |
| ------------ | ----- |
| Views        | 0     |
| Viewmodels   | 1     |
| Panels       | 7     |
| Services     | 107   |
| Controls     | 0     |
| Repositories | 24    |
| Factories    | 4     |

## Key Base Classes

### ComponentBase

- `Components/JarvisChatPanel.razor.cs`
- `Components/Panels/BreakEvenPanel.razor.cs`
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
- `Components/Panels/BreakEvenPanel.razor.cs`
- `Components/Panels/CustomerViewerPanel.Bindings.cs`
- `Components/Panels/CustomerViewerPanel.Helpers.cs`
- `Components/Panels/CustomerViewerPanel.Shared.cs`
- `Components/Panels/CustomerViewerPanel.razor.cs`
- `src/WileyWidget.Models/Models/PanelItem.cs`

## Services

- `Services/AffordabilityAnalysisService.cs`
- `Services/CapitalGapApiService.cs`
- `Services/BrowserDownloadService.cs`
- `Services/DebtCoverageApiService.cs`
- `Services/QuickBooksImportApiService.cs`
- `Services/UtilityCustomerApiService.cs`
- `Services/WorkspaceAiApiService.cs`
- `Services/WorkspaceBootstrapService.cs`
- `Services/WorkspaceDocumentExportService.cs`
- `Services/WorkspaceKnowledgeApiService.cs`
- `Services/WorkspacePersistenceService.cs`
- `Services/WorkspaceLocalBootstrapService.cs`
- `Services/WorkspaceSnapshotApiService.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Keywords.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Orchestration.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Overrides.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.Patterns.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.cs`
- `WileyCoWeb.Api/Configuration/StartupConfigurationService.cs`
- `State/CustomerFilterService.cs`
- `src/WileyWidget.Abstractions/IApplicationStateService.cs`
- `src/WileyWidget.Abstractions/ICacheService.cs`
- `src/WileyWidget.Abstractions/IViewRegistrationService.cs`
- `src/WileyWidget.Business/Interfaces/IDepartmentExpenseService.cs`
- `src/WileyWidget.Business/Interfaces/IGrokRecommendationService.cs`
- `src/WileyWidget.Business/Services/AuditService.cs`
- `src/WileyWidget.Business/Services/GrokRecommendationService.cs`
- `src/WileyWidget.Models/ServiceChargeRecommendation.cs`
- `src/WileyWidget.Services/AICacheWarmingService.cs`
- `src/WileyWidget.Services/AILoggingService.cs`

## Controls

## Key NuGet Dependencies

## Manifest Stats

- Total files indexed: **425**
- Files with embedded content: **400**
- Total source size: **26,190 KB**
- Manifest mode: **full-context**

---

> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.
