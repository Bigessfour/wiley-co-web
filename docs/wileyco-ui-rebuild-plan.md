# Wiley-Co UI Rebuild Plan

This document turns the restored Wiley Widget vision into an implementation plan for the current Blazor WebAssembly app.

## Baseline

- [x] App targets `.NET 9` Blazor WebAssembly.
- [x] Syncfusion Blazor `33.1.44` is the UI stack.
- [x] AWS Amplify is the host.
- [x] Aurora PostgreSQL Serverless v2 is the data store.
- [x] The current dashboard seed is [Components/Pages/BudgetDashboard.razor](../Components/Pages/BudgetDashboard.razor).
- [x] Existing service surface is available in `WileyWidget.Services` and `WileyWidget.Business`.
- [x] Treat `src/` as a legacy archive and promote only the files needed for the web app into the active project structure.

## Product Goal

Rebuild the app around a focused municipal utility rate-study workflow for Water, Sewer / Wiley Sanitation District, Trash, and Wiley Apartments.

- [x] No billing module.
- [x] Emphasize break-even analysis.
- [x] Emphasize manual current-rate entry.
- [x] Emphasize scenario planning.
- [x] Emphasize light customer records.
- [x] Emphasize projections and trend visuals.
- [x] Emphasize AI recommendations and plain-language chat.

## Architecture Direction

### Target Architecture

Build the web app as a thin Blazor client over a clean, layered backend. Each layer owns a single responsibility.

- **Presentation / Client Layer** - Blazor WebAssembly + Syncfusion components. Renders workspace, handles UI state and user gestures only.
- **Application Layer** - Orchestrates use-cases (workspace queries, scenario calculations, rate recommendations, chat requests). Lives in `WileyCo.Application`.
- **Domain / Shared Layer** - Pure C# models, DTOs, and contracts. Lives in `WileyCo.Shared`. No Blazor, EF Core, or infrastructure dependencies.
- **Data / Infrastructure Layer** - EF Core, repositories, import pipeline, persistence. Lives in `WileyCo.Data`.
- **AI Layer** - Semantic Kernel + xAI orchestration, prompt assembly, context store. Lives in `WileyCo.Application` (AI-specific services).

### Layer Responsibilities & State Management

- **Client**: Uses `SfDashboardLayout`, `SfSidebar`, `SfSplitter`, `SfTab`, charts, gauges, grids. All data comes from injected `WorkspaceService` or API calls returning snapshots.
- **Application**: Builds a single `WorkspaceSnapshot` DTO per enterprise + fiscal year. Contains everything panels need (financial totals, rates, scenario impacts, customer summary, trends, AI context). Uses MediatR-style handlers or explicit services.
- **Shared**: All DTOs, enums, validators (FluentValidation), and schema-native entities that cross boundaries.
- **Data**: EF Core `AppDbContext`, repositories (no UI-specific projections), migrations, import normalizers.
- **AI**: `XAIService`, `ChatBridgeService`, `AIMemoryService`. Prompts are built from the same `WorkspaceSnapshot` the UI sees -> consistent answers.

**State Management**:

- Central `WorkspaceState` service (singleton in Blazor WASM) holds the current enterprise, fiscal year, and active scenario.
- Cascading parameters or `StateHasChanged` triggered by `WorkspaceState.OnChange` for reactive panels.
- Scenario changes are saved via API -> snapshot is reloaded.

### Runtime Flow

1. Import pipeline normalizes source files -> canonical Aurora tables.
2. Repositories project canonical data into Shared DTOs.
3. Application services compose one `WorkspaceSnapshot`.
4. Blazor workspace injects the snapshot (or calls thin API endpoint `/api/workspace/{enterprise}/{year}`).
5. AI services receive the identical snapshot to generate explanations and recommendations.
6. User actions (rate edit, scenario add-cost) flow back through Application services -> persist -> new snapshot.

### Boundary Rules (expanded)

- UI components must not query EF Core or contain business logic.
- Repositories must not return Syncfusion objects or UI-only state.
- Shared models stay free of Blazor, EF, or infrastructure references.
- All calculations (break-even, scenario impact, projections) live in Application services.
- AI prompts are assembled exclusively from the workspace snapshot (no direct DB access).

### Frontend

- [x] Keep the web app in Blazor WebAssembly.
- [x] Use Syncfusion dashboard, sidebar, splitter, grid, chart, card, tab, gauge, and dialog components.
- [x] Rework the budget dashboard into a panel-first shell with persistent enterprise and fiscal year context.
- [x] Preserve a responsive desktop-first feel.
- [x] `WileyWorkspace.razor` becomes the single orchestrator page.

### Backend

- [x] Keep private Aurora PostgreSQL as the system of record.
- [x] `WileyCo.Shared`, `WileyCo.Data`, `WileyCo.Application`, optional thin `WileyCo.Api` (minimal API controllers or gRPC if needed later).
- [x] Reuse existing service classes before creating new ones.
- [x] Add a thin snapshot host that serves workspace data to the Blazor client.

### AI Layer

- [ ] Semantic Kernel remains orchestrator.
- [ ] xAI / Grok for plain-language output.
- [ ] Lightweight AI context store (scenario summaries, recommendation history).

## Detailed UI Build Plan

**Goal**: Deliver a production-ready multi-panel workspace in controlled slices.

### Phase 0 - Shell Foundation

- [x] Replace `BudgetDashboard` with `WileyWorkspace.razor`.
- [x] Add `SfDashboardLayout` + persistent enterprise/fiscal-year context rail.
- [x] `SfSidebar` for navigation + enterprise selector.
- [x] `SfSplitter` for resizable left/right rails.
- [x] Floating or docked JARVIS chat rail (theme-aware).

### Phase 1 - Enterprise Context + Break-Even Slice

1. [x] Wire enterprise selector & fiscal-year picker (dropdowns bound to `WorkspaceState`).
2. [x] Build **Break-Even Panel**:

   - Total costs, projected volume, current rate (editable), calculated break-even rate, delta.
   - Instant recalc on any input change (use `OnValueChange` + `WorkspaceState`).
   - Gauge + card layout with `SfGauge` and `SfChart`.
3. [x] Build **Rates Panel** (adjacent tile):

   - Manual current-rate entry (`SfTextBox` + validation).
   - Visual current vs. recommended + break-even (`SfChart` column or bullet chart).
4. [x] Save rate snapshot to Aurora via thin API.

   - Implemented as a POST to the thin snapshot host. The workspace now archives the current rate and scenario payload to Aurora-backed storage from the rates panel.

**Acceptance**: Met for the live-data gate. Break-even interactions are live, and the page now hydrates customers and projections from the thin API workspace snapshot instead of seeded client-side sample collections.

### Phase 2 - Scenario Planner

- [x] Add cost items (vehicles, raises, reserves, capital) via `SfGrid` with add/edit row.
- [x] Real-time impact on break-even & recommended rate.
- [x] “Before / After” comparison cards.
- Save / Load scenario (name, date, impact summary) to Aurora.
- One-click “Apply to workspace” button.

### Phase 3 - Customer Viewer Panel

- Filterable `SfGrid` (enterprise, city limits, search).
- Light customer records only (no billing fields).
- Pagination + export to Excel (reuse `ExcelExportService`).

### Phase 4 - Trends & Projections Panel

- `SfChart` with historical costs/rates + projected lines.
- Simple projection model first (linear + volume growth).
- Toggle AI-enhanced forecast later.

### Phase 5 - AI Chat (JARVIS)

- Theme-aware chat UI (`SfChat` or custom with cards).
- Conversation history persisted per workspace.
- Feed entire `WorkspaceSnapshot` to `ChatBridgeService`.
- Secure thin API endpoint for Grok requests.

**UI Build Order Summary**:

1. Workspace shell + context rail
2. Break-even + Rates panels
3. Scenario planner
4. Customer viewer
5. Trends/projections
6. JARVIS chat (last, because it consumes the full snapshot)

**Syncfusion Component Map**:

- DashboardLayout -> main tiles
- Sidebar + Splitter -> rails
- Grid -> scenario costs & customers
- Chart / Gauge -> break-even, trends, comparisons
- Tab -> sub-views inside panels when needed
- Dialog -> scenario save/load, import

## Backend Workstream

### Shared Domain Model

- [ ] Create or extend shared entities in `WileyCo.Shared` for `Enterprise`, `FinancialData`, `Rate`, `Customer`, `Scenario`, `ScenarioImpact`, `HistoricalData`, `Projection`, and `AIContextStore`.
- [ ] Pull canonical DTOs and shared contracts out of archived `src` files only when they still serve the web pipeline.

### Persistence

- [ ] Add EF Core mappings for Aurora PostgreSQL.
- [ ] Create migrations for the new shared model.
- [ ] Keep import lineage and versioning explicit.
- [ ] Use soft checks or unique constraints where duplication would be harmful.

### Calculation Services

Reuse and adapt existing services where possible.

- [ ] `ServiceChargeCalculatorService`
- [ ] `WhatIfScenarioEngine`
- [ ] `DashboardService`
- [ ] `ExcelExportService`
- [ ] `ReportExportService`
- [ ] `SemanticSearchService`
- [ ] `XAIService`
- [ ] `ChatBridgeService`

Add new services if the current ones are too tied to the old budget workflow.

- [ ] `EnterpriseRateService`
- [ ] `ScenarioRecommendationService`
- [ ] `CustomerSummaryService`
- [ ] `ProjectionService`
- [ ] `AIMemoryService`

### API Surface

- [x] Expose a thin snapshot API for the workspace shell.
- [ ] Expose the remaining write and recommendation endpoints for enterprise selection, financial data, rate updates, scenario compare, customer browsing, and AI requests.

## Data Import Workstream

- [ ] Keep the earlier import discipline, but narrow it to the new UI needs.
- [ ] Import enterprise financial inputs.
- [ ] Import light customer records.
- [ ] Import historical trends.
- [ ] Import scenario snapshots.
- [ ] Do not reintroduce billing logic.
- [ ] Use CSV or Excel for customer and financial inputs.
- [ ] Keep Aurora tables as the canonical store.
- [ ] Store imported file metadata for traceability.

## AI Workstream

### AI Chat

- [x] Feed enterprise state, financial totals, scenario costs, and recent rate history into Semantic Kernel.
- [x] Keep prompts short and grounded in the current fiscal year and selected enterprise.
- [x] Return both a plain-language explanation and the numeric result.
- [x] Scaffold the JARVIS chat panel in the Blazor workspace.
- [ ] Add a secure thin API contract for future Grok requests.
- [ ] Add theme-aware chat controls and conversation history.

### Recommendation History

- [ ] Store generated recommendations by scenario.
- [ ] Keep the prompt, response, and underlying inputs so recommendations are auditable.

## Delivery Phases

### Phase 1 - Foundation

- [x] Confirm shared model shapes.
- [ ] Add EF Core context and migrations.
- [x] Define the initial snapshot API contract.
- [x] Create the new shell layout and navigation.

### Phase 2 - Break-Even Slice

- [x] Implement break-even calculation.
- [x] Wire current rate input.
- [x] Render the first dashboard charts.
- [x] Save a rate snapshot to Aurora.

### Completed Since Last Update

- [x] Added a thin API host at `WileyCoWeb.Api` with `/api/workspace/snapshot`.
- [x] Hydrated the Blazor workspace from the live snapshot endpoint.
- [x] Added a local bootstrap fallback for offline or standalone runs.
- [x] Updated the Blazor client to accept `WILEY_WORKSPACE_API_BASE_ADDRESS` for local dev.
- [x] Excluded the API host sources from the Blazor WASM compile glob.
- [x] Verified `dotnet build WileyCoWeb.csproj` succeeds.
- [x] Verified `dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj` succeeds.

### Phase 3 - Scenario Planning

- [ ] Add the scenario builder.
- [ ] Add cost adjustments.
- [ ] Compare before and after rate impact.

### Phase 4 - Customer Viewer

- [x] Build the filterable customer grid.
- [ ] Add export.
- [ ] Add optional map view.

### Phase 5 - AI Chat and Recommendations

- [ ] Wire Semantic Kernel and xAI.
- [ ] Add conversational rate analysis.
- [ ] Store recommendation history.
- [ ] Connect the JARVIS panel to the API-backed chat service.

### Phase 6 - Polish and Deployment

- [ ] Responsive refinements.
- [ ] Theme polishing.
- [ ] Export flows.
- [ ] Amplify deployment hardening.

## Suggested File/Project Changes

- [ ] Add `WileyCo.Shared` for shared domain models and DTOs.
- [ ] Add `WileyCo.Api` or a thin service host if the app needs a secure data bridge.
- [ ] Add `WileyCo.Components` for reusable Syncfusion panels.
- [ ] Move only the surviving web-facing code from `src/` into the active app and supporting libraries, then leave the rest archived.
- [x] Replace the current `BudgetDashboard` page with the new workspace shell.
- [ ] Keep the old budget dashboard logic only if it still helps as a prototype.

### Concrete `src` Promotion Target

Use the current inventory to separate the archive into three active paths and one hold bucket:

- **Promote into `WileyCo.Shared`**: schema-native entities, API DTOs, core dashboard models, scenario snapshot models, and shared validators.
- **Promote into `WileyCo.Data` or `WileyCo.Application`**: `AppDbContext`, repositories, analytics pipeline pieces, import pipeline adapters, and calculation services that feed the web UI.
- **Keep in the active web host**: the workspace shell, `BudgetDashboard`/`WileyWorkspace` transition code, and any thin UI-facing view models.
- **Leave in `src` archive for now**: QuickBooks/Desktop code, WinForms helpers, billing/payment artifacts, and empty or duplicate stubs.
- **Hold for review**: overlapping analytics, chat persistence, anonymization, localization, timeout, and duplicate validator paths until the web pipeline is stable.

Practical target structure:

- `WileyCo.Shared` for contracts and schema-native models.
- `WileyCo.Data` for EF Core context, repositories, migrations, and persistence helpers.
- `WileyCo.Application` for orchestration, analytics, import, export, and scenario services.
- `WileyCo.Api` for the thin secure bridge.
- `Components/` for the Syncfusion workspace shell and panel implementations.

### Promotion Rules

- [ ] Keep files that feed the web UI, schema mappings, repositories, import pipeline, scenario logic, or AI/chat.
- [ ] Archive files that are desktop-only, QuickBooks/Desktop-only, WinForms-only, or access helpers that the web app cannot call.
- [ ] Promote files into the active project only when they map to a current UI panel, API contract, or backend service boundary.
- [ ] Prefer extracting thin contracts and shared models over copying legacy implementation details.

### UI Element Map

This is the target UI shape the current backend and middle-layer files should feed.

- **Workspace shell**: `Components/Pages/WileyWorkspace.razor` and `Components/Pages/WileyWorkspace.razor.cs` remain the top-level orchestrator and now bind enterprise, fiscal year, scenario, and rate inputs through shared workspace state while the snapshot layer is still pending.
- **Enterprise context rail**: `EnterpriseRepository`, `AppDbContext`, shared enterprise models, and the import lineage tables should drive enterprise selection, fiscal year selection, and active scenario context.
- **Break-even tile**: `DashboardService`, `AnalyticsService`, `BudgetAnalyticsRepository`, `AnalyticsPipeline`, and the rate-calculation services should feed total cost, projected volume, current rate, break-even rate, and delta.
- **Scenario planner tile**: `ScenarioSnapshotRepository`, `SavedScenarioSnapshot`, `BudgetRepository`, and the scenario engine should drive add-cost items, compare-before/after views, and save/load actions.
- **Customer viewer tile**: `AmplifySchemaEntities`, `AppDbContext`, and the customer-focused repository layer should provide light customer records only, with filters for enterprise, search, and city-limits flags.
- **Trends and projections tile**: `BudgetAnalyticsRepository`, `TownOfWileyBudget2026`, `MonthlyRevenue`-style models, and projection services should provide historical and forward-looking series for charts.
- **Rates tile**: `DashboardService`, manual rate input state, and scenario comparison models should keep current rate, recommended rate, and visual comparison in sync.
- **AI chat rail**: `ChatBridgeService`, `SemanticSearchService`, `XAIService`, and the AI context/logging services should provide explainable rate summaries and scenario answers.

Recommended data flow for the UI:

1. Import and normalize source data into the schema-native tables.
2. Map schema data into shared DTOs and workspace view models.
3. Build one workspace snapshot that contains enterprise context, financial totals, scenario results, customer rows, trend series, and chat context.
4. Bind each Syncfusion panel to the snapshot instead of querying separate services from the page.

### UI Build Order

1. Replace the sample data in `WileyWorkspace` with a workspace view model backed by real repository/service calls.
2. Add the enterprise selector and fiscal-year context as the first live inputs.
3. Wire the break-even and rates panels to the analytics pipeline.
4. Add the scenario planner with one add-cost action and scenario persistence.
5. Add the customer viewer with filtering and pagination.
6. Add trends and projections once the canonical series model is stable.
7. Connect the AI chat rail last so it can read the same workspace snapshot as the rest of the page.

### Archive Review & Culling Recommendations

- [ ] Treat `src/` as a true archive and stop adding new active code there.
- [ ] Review any remaining legacy desktop, QuickBooks, WinForms, and payment artifacts for removal after the web pipeline is stable.
- [ ] Promote only files that feed `WorkspaceSnapshot`, the import pipeline, repositories, or AI/chat services.
- [ ] Keep history intact by archiving, not hard-deleting, until the promoted replacements are verified.

## Immediate Next Actions

- [x] Confirm the shared model names and migration target.
- [x] Scaffold the enterprise selector and workspace shell.
- [x] Wire enterprise and fiscal-year state into the shell.
- [x] Build the break-even panel first.
- [ ] Wire the first API call to load enterprise and fiscal-year state.

## Notes

- The current workspace already has Syncfusion `33.1.44` packages and a working dashboard seed.
- The hard-copy Blazor docs are available locally in `Blazor Documentation/` and should be treated as the reference source.
- This plan is ready to feed into Syncfusion AI UI Builder or similar tooling, but those tools are not directly available in this chat environment.
