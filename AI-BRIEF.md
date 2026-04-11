# WileyCoWeb — AI Briefing
> Generated: 2026-04-10 11:52  |  Branch: `main`  |  Commit: `7882052736`

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
- `Components/QuickBooksImportPanel.razor` — Panel — priority 100
- `Components/JarvisChatPanel.razor` — Panel — priority 100
- `Components/JarvisChatPanel.razor.cs` — Panel — priority 100
- `WileyCoWeb.Api/Program.cs` — Program — priority 100
- `Services/QuickBooksImportApiService.cs` — Service — priority 95
- `Services/BrowserDownloadService.cs` — Service — priority 95
- `Services/WorkspaceAiApiService.cs` — Service — priority 95
- `Services/WorkspaceBootstrapService.cs` — Service — priority 95
- `Services/WorkspaceDocumentExportService.cs` — Service — priority 95
- `Services/WorkspaceSnapshotApiService.cs` — Service — priority 95
- `Services/WorkspacePersistenceService.cs` — Service — priority 95
- `src/WileyWidget.Abstractions/ILazyLoadViewModel.cs` — ViewModel — priority 95
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_JarvisChatPanel_razor.g.cs` — Panel — priority 92
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_QuickBooksImportPanel_razor.g.cs` — Panel — priority 92
- `src/WileyWidget.Models/Models/PanelItem.cs` — Panel — priority 92
- `Program.cs` — Program — priority 90
- `bin/Debug/net9.0/System.IO.FileSystem.AccessControl.dll` — Control — priority 88
- `bin/Debug/net9.0/System.IO.Pipes.AccessControl.dll` — Control — priority 88
- `bin/Debug/net9.0/System.Security.AccessControl.dll` — Control — priority 88
- `bin/Debug/net9.0/wwwroot/_framework/System.IO.FileSystem.AccessControl.qj6vwhotmp.wasm` — Control — priority 88

## Recommended Reading Order
1. `Components/QuickBooksImportPanel.razor`
2. `Components/JarvisChatPanel.razor`
3. `Components/JarvisChatPanel.razor.cs`
4. `WileyCoWeb.Api/Program.cs`
5. `Services/QuickBooksImportApiService.cs`
6. `Services/BrowserDownloadService.cs`
7. `Services/WorkspaceAiApiService.cs`
8. `Services/WorkspaceBootstrapService.cs`
9. `Services/WorkspaceDocumentExportService.cs`
10. `Services/WorkspaceSnapshotApiService.cs`
11. `Services/WorkspacePersistenceService.cs`
12. `src/WileyWidget.Abstractions/ILazyLoadViewModel.cs`
13. `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_JarvisChatPanel_razor.g.cs`
14. `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_QuickBooksImportPanel_razor.g.cs`
15. `src/WileyWidget.Models/Models/PanelItem.cs`
16. `Program.cs`

## Architecture Summary
| Component | Count |
|-----------|-------|
| Views | 0 |
| Viewmodels | 1 |
| Panels | 4 |
| Services | 85 |
| Controls | 0 |
| Repositories | 24 |
| Factories | 3 |

## Key Base Classes
### ComponentBase
- `Components/JarvisChatPanel.razor.cs`
- `Components/Pages/WileyWorkspaceBase.cs`
### OwningComponentBase
### ServiceBase

## Syncfusion Packages
- `Syncfusion.Blazor.Buttons`
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
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_JarvisChatPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_QuickBooksImportPanel_razor.g.cs`
- `src/WileyWidget.Models/Models/PanelItem.cs`

## Services
- `Services/QuickBooksImportApiService.cs`
- `Services/BrowserDownloadService.cs`
- `Services/WorkspaceAiApiService.cs`
- `Services/WorkspaceBootstrapService.cs`
- `Services/WorkspaceDocumentExportService.cs`
- `Services/WorkspaceSnapshotApiService.cs`
- `Services/WorkspacePersistenceService.cs`
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
- `src/WileyWidget.Services/AdaptiveTimeoutService.cs`
- `src/WileyWidget.Services/ActivityFallbackDataService.cs`
- `src/WileyWidget.Services/AnalyticsService.cs`
- `src/WileyWidget.Services/AnomalyDetectionService.cs`
- `src/WileyWidget.Services/ApplicationMetricsService.cs`
- `src/WileyWidget.Services/AuditService.cs`
- `src/WileyWidget.Services/CacheServiceCollectionExtensions.cs`
- `src/WileyWidget.Services/ChatBridgeService.cs`
- `src/WileyWidget.Services/CorrelationIdService.cs`
- `src/WileyWidget.Services/CsvExcelImportService.cs`
- `src/WileyWidget.Services/DashboardService.cs`
- `src/WileyWidget.Services/DataAnonymizerService.cs`
- `src/WileyWidget.Services/DiValidationService.cs`

## Controls

## Key NuGet Dependencies

## Manifest Stats
- Total files indexed: **4550**
- Files with embedded content: **4535**
- Total source size: **929,680 KB**
- Manifest mode: **full-context**

---
> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.