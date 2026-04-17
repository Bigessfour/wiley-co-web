# WileyCoWeb — AI Briefing
> Generated: 2026-04-16 17:02  |  Branch: `main`  |  Commit: `017c3e5013`

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
- `Components/Panels/CustomerViewerPanel.razor` — Panel — priority 100
- `Components/Panels/BreakEvenPanel.razor` — Panel — priority 100
- `Components/Panels/DataDashboardPanel.razor` — Panel — priority 100
- `Components/Panels/DecisionSupportPanel.razor` — Panel — priority 100
- `Components/Panels/RatesPanel.razor` — Panel — priority 100
- `Components/Panels/QuickBooksImportPanelWrapper.razor` — Panel — priority 100
- `Components/Panels/ScenarioPlannerPanel.razor` — Panel — priority 100
- `Components/Panels/TrendsPanel.razor` — Panel — priority 100
- `WileyCoWeb.Api/Program.cs` — Program — priority 100
- `Services/QuickBooksImportApiService.cs` — Service — priority 95
- `Services/BrowserDownloadService.cs` — Service — priority 95
- `Services/WorkspaceAiApiService.cs` — Service — priority 95
- `Services/WorkspaceBootstrapService.cs` — Service — priority 95
- `Services/WorkspaceDocumentExportService.cs` — Service — priority 95
- `Services/WorkspaceSnapshotApiService.cs` — Service — priority 95
- `Services/WorkspaceKnowledgeApiService.cs` — Service — priority 95
- `Services/WorkspacePersistenceService.cs` — Service — priority 95

## Recommended Reading Order
1. `Components/JarvisChatPanel.razor`
2. `Components/JarvisChatPanel.razor.cs`
3. `Components/QuickBooksImportPanel.razor`
4. `Components/Panels/CustomerViewerPanel.razor`
5. `Components/Panels/BreakEvenPanel.razor`
6. `Components/Panels/DataDashboardPanel.razor`
7. `Components/Panels/DecisionSupportPanel.razor`
8. `Components/Panels/RatesPanel.razor`
9. `Components/Panels/QuickBooksImportPanelWrapper.razor`
10. `Components/Panels/ScenarioPlannerPanel.razor`
11. `Components/Panels/TrendsPanel.razor`
12. `WileyCoWeb.Api/Program.cs`
13. `Services/QuickBooksImportApiService.cs`
14. `Services/BrowserDownloadService.cs`
15. `Services/WorkspaceAiApiService.cs`
16. `Services/WorkspaceBootstrapService.cs`
17. `Services/WorkspaceDocumentExportService.cs`
18. `Services/WorkspaceSnapshotApiService.cs`
19. `Services/WorkspaceKnowledgeApiService.cs`
20. `Services/WorkspacePersistenceService.cs`

## Architecture Summary
| Component | Count |
|-----------|-------|
| Views | 0 |
| Viewmodels | 0 |
| Panels | 11 |
| Services | 9 |
| Controls | 0 |
| Repositories | 0 |
| Factories | 0 |

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

## Panels
- `Components/JarvisChatPanel.razor.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_JarvisChatPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_CustomerViewerPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_BreakEvenPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_DecisionSupportPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_DataDashboardPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_QuickBooksImportPanelWrapper_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_RatesPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_ScenarioPlannerPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_Panels_TrendsPanel_razor.g.cs`
- `obj/Debug/net9.0/Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Components_QuickBooksImportPanel_razor.g.cs`

## Services
- `Services/QuickBooksImportApiService.cs`
- `Services/BrowserDownloadService.cs`
- `Services/WorkspaceAiApiService.cs`
- `Services/WorkspaceBootstrapService.cs`
- `Services/WorkspaceDocumentExportService.cs`
- `Services/WorkspaceSnapshotApiService.cs`
- `Services/WorkspaceKnowledgeApiService.cs`
- `Services/WorkspacePersistenceService.cs`
- `WileyCoWeb.Api/WorkspaceReferenceDataImportService.cs`

## Controls

## Key NuGet Dependencies

## Manifest Stats
- Total files indexed: **11995**
- Files with embedded content: **11972**
- Total source size: **1,532,286 KB**
- Manifest mode: **full-context**

---
> Auto-generated by `scripts/generate-ai-manifest.py`. Do not edit manually.