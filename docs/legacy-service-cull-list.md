# Legacy Service Cull List

Tracks dead-service batches removed from the Blazor/API codebase. Active chat and knowledge flow through `WorkspaceAiAssistantService`, `WorkspaceAiKernelFactory`, and related API registrations only.

## Removed Earlier

See git history for prior WinForms/DI scaffold removals (ChatBridge, Dashboard, SemanticSearch, etc.).

## Removed In Phase 2 (issue #24)

Leftover AI / settings types the cull list previously claimed were gone but were still compiling:

- `src/WileyWidget.Services/XAIService.cs`
- `src/WileyWidget.Services/XaiModelDiscoveryService.cs`
- `src/WileyWidget.Services/SettingsService.cs`
- `src/WileyWidget.Services/CorrelationIdService.cs`
- `src/WileyWidget.Services/AICacheWarmingService.cs`
- `src/WileyWidget.Services/AILoggingService.cs`
- `src/WileyWidget.Services/AnalyticsPipeline.cs`
- `src/WileyWidget.Services/GrokSupercomputer.cs`
- `src/WileyWidget.Services/JARVISPersonalityService.cs`
- `src/WileyWidget.Services/NullAIService.cs`
- `src/WileyWidget.Services/NullGrokSupercomputer.cs`
- `src/WileyWidget.Services.Abstractions/IAIService.cs`
- `src/WileyWidget.Services.Abstractions/IAILoggingService.cs`
- `src/WileyWidget.Services.Abstractions/IAnalyticsPipeline.cs`
- `src/WileyWidget.Services.Abstractions/IGrokSupercomputer.cs`
- `src/WileyWidget.Services.Abstractions/IJARVISPersonalityService.cs`
- `src/WileyWidget.Services.Abstractions/ISettingsService.cs`
- `src/WileyWidget.Services.Abstractions/IXaiModelDiscoveryService.cs`
- `src/WileyWidget.Services.Abstractions/XaiModelDescriptor.cs`
- `tests/WileyWidget.Tests/SettingsServiceTests.cs`

API change: removed `IAIService` / `NullAIService` registration and config-gated `AICacheWarmingService` hosted service from `WileyCoWeb.Api/Program.cs`.

Why safe:

- None of these types were constructed by the Blazor client.
- API production chat uses `WorkspaceAiAssistantService` only.
- `QuickBooksImportAssistantService` does not call model discovery.
- `TelemetryStartupService` remains (ops health), separate from the deleted AI cache warmer.

## Remaining Candidates

- None verified for the legacy AI graph after this pass. Re-check with symbol search before claiming further removals.

## Notes

- Do not edit historical EF migration seed strings that still mention retired model ids.
- Live model default is `grok-4.7` via `WorkspaceAiKernelFactory.DefaultSemanticKernelModel`.
