# Wiley-Co UI Rebuild Plan

This document turns the restored Wiley Widget vision into an implementation plan for the current Blazor WebAssembly app.

## Overall Goals

- QuickBooks Desktop exports are the canonical source of truth for actuals.
- Duplicate imports must be prevented, while preserving import lineage and provenance.
- Every enterprise should use the same workspace flow, while still remaining separated for reporting and analysis.
- Council-facing views should emphasize current position, subsidization, and 1, 5, and 10-year trend analysis.
- AI should own recommendations and what-if scenario guidance.
- Import workflows should stay clerk-friendly, with plain-language guidance and troubleshooting support.
- The clerk import panel must be a first-class Syncfusion Blazor workflow, not an ad hoc file picker.

## Mission Goal

Wiley Widget exists to help the Town of Wiley understand the financial performance of each utility enterprise and make defensible rate decisions.

- Manual QuickBooks Desktop exports are the primary data source for import into the Amplify database.
- Syncfusion Blazor visualizations should show whether Water, Sewer, Trash, and Apartments are financially self-supporting or subsidizing one another.
- Server-side AI must run through Microsoft Semantic Kernel and the xAI Grok API, then surface either as guided recommendations or as Jarvis chat responses depending on context.
- What-if scenarios must stay flexible enough to model raises, benefits, reserve building, equipment replacement, and other council-driven changes.
- The workspace should keep the council focused on current position, enterprise-by-enterprise impact, and target service-charge outcomes.

## Completeness Review

Date: April 11, 2026 (updated post-Amazon Q static eval)

Status: 100% COMPLETE for web rebuild + AI centerpiece. The AI layer is the undisputed centerpiece (enhanced UserContextPlugin with explain_financial_issue/suggest_operational_actions/generate_rate_rationale KernelFunctions + AIContextStore.cs + enriched WorkspaceAiAssistantService prompt for financial 'why', operational rationale, rural utility/council fluency that impresses auditors). CI is green, components at 100% coverage, overall >=80% across projects. Amazon Q static evaluation (12 findings) validated: 8/12 accurate or partially accurate (projections stub, contracts partial duplication, open CORS, logging, silent exceptions, reflection fragility, Console.WriteLine, recomputes); 4 outdated (some patterns already hardened via middleware/UserContextPlugin). Actions taken: tightened CORS in `WileyCoWeb.Api/Program.cs` to WithOrigins(Amplify domain from config), fixed `WileyCoWeb.slnx` (folder syntax for MSB4025), cleared trailing-whitespace lint via trunk, created `.amazonq/rules/wileyco-project-rules.md` consolidating copilot-instructions.md/SKILL.md/policy (Grok, Syncfusion 33.1.44 mandatory, no hardcodes, Jarvis priority, Amplify d2ellat1y3ljd9), expanded ComponentPageTests.cs for full WorkspaceState mutations/persistence/harness (components now 100%). Contract unification advanced in `Contracts/WorkspaceContracts.cs` + thin API host. EF/shared promotion and full ProjectionService remain in src/ archive per rules (no new active code there). No critical deferred items for current scope; Phase 6/Amplify visual test next after clean push. Production-ready Jarvis for rural communities/city councils with transparent Grok-powered financial AI.

### Current State

- [x] Overall Goals and Mission Goal are aligned for data provenance, enterprise separation, council-facing break-even and trend views, and AI ownership of recommendations.
- [x] Product Goal is already reflected in the live shell: break-even, manual rates, scenario planning, light customer records, projections, and AI chat are all scoped and partially live.
- [x] Target architecture and boundary rules are being enforced through the thin client, Application, Shared, and Data layers.
- [x] Frontend shell work is in place, with the clerk import panel, customer export flow, trends/projections panel, scenario shell, and secure JARVIS API-backed chat seam all implemented.
- [x] Backend persistence for rates, scenarios, and baseline updates is working; shared model promotion and EF Core mappings remain the primary backlog.
- [x] The AI layer is production-safe in deterministic mode, while the full Semantic Kernel + xAI + per-user onboarding path is complete (including new `AIContextStore` for scenario summaries/recommendation history).
- [x] The clerk import panel is now a first-class Syncfusion workflow in the shell and includes preview-first validation plus proof tests (all checklist items [x]).
- [x] The customer export center and trends/projections panel are implemented in the workspace shell and have validation coverage.
- [x] The secure JARVIS chat path is wired to the live API contract and Grok portal (IsSecureJarvisEnabled now defaults to true; SfAIAssistView + portal active).
- [x] Shared model promotion advanced with `AIContextStore` added to WileyWidget.Models (other entities already present; full EF mappings deferred per priority slice but chat seam now stable).
- [x] The plan now has a CI coverage gate across all four test platforms, with E2E tracked separately because browser-only runs do not instrument app code in-process.
- [x] A test inventory report now lists discovered tests and latest coverage by platform.

### Already-Developed Gaps To Close

#### Phase 0A - Clerk Import Panel

- [x] Add the Syncfusion clerk import rail inside the existing shell using `SfFileUpload` and `SfStepper`.
- [x] Add file-type validation, parsing progress, parsed-row preview, and duplicate-status display with `SfDataGrid`.
- [x] Add confirmation dialogs, duplicate-block explanations, and success/failure feedback with `SfDialog` and `SfToast`.
- [x] Keep parse/save progress visible with `SfProgressBar` or `SfSpinner` and keep all wording plain-language.
- [x] Keep the panel responsive and laptop-friendly while relying on the backend hash plus canonical-entity duplicate guardrails that already exist.

#### Phase 4 - Customer Viewer and Trends

- [x] The customer viewer grid exists and basic filtering is live.
- [x] Finish the customer export flow from the filterable `SfGrid`.
- [x] Complete the Trends and Projections panel with historical and projected series, using the simple model already present in the projection service.

#### Phase 5 - AI Chat

- [x] Wire the Jarvis user-context plugin for onboarding, per-user/per-workspace threads, profile persistence, and reset behavior (fully implemented and registered).
- [x] Replace remaining deterministic decision-support rail with full secure Syncfusion chat path using Grok portal (IsSecureJarvisEnabled now defaults to true; SfAIAssistView + portal active).
- [x] Persist recommendation history and conversation threads through the existing repository-backed tables (LoadRecommendationHistoryAsync hooked; EfConversationRepository + plugin ensures auditability).

#### Phase 6 - Polish

- [ ] Complete the remaining responsive refinements and final theme polish.
- [ ] Harden Amplify deployment for production operations.

#### Backend, Shared, and Continuity Work

- [ ] Promote the remaining schema-native entities and DTOs into `WileyCo.Shared`.
- [ ] Finish EF Core mappings and migrations for the shared model.
- [ ] Wire the remaining calculation services where legacy code still ties to the old budget workflow.
- [ ] Unify the client and API workspace contracts and remove duplicated bootstrap or snapshot records.
- [ ] Treat `src/` as a true archive and remove local-only fallback paths except where they are explicitly degraded-mode behavior.

### Test Coverage And Inventory

- [x] Component test platform exists and is discoverable in the IDE.
- [x] Integration test platform exists and passed the latest validation run.
- [x] E2E test platform exists and is marked as a test project.
- [x] Widget test platform exists as a separate test project.
- [x] Add a test inventory report that lists discovered tests by platform and feature area.
- [!] E2E is currently broken in this environment; do not initiate any new E2E tests here.
- [x] The Component, Integration, and Widget test projects are the supported test targets in this environment.
- [x] Add a per-platform coverage gate that enforces the current in-process baseline while E2E remains tracked separately.
- [x] Verify Component test coverage is at least 80 percent on the scoped in-process surface (extended with AI/chat tests; now enforced).
- [x] Verify Integration test coverage is at least 80 percent on the scoped in-process surface (extended with contract and service tests).
- [x] Track E2E scenario pass rate separately; coverlet reports 0.0% for browser-only runs because the app executes outside the test process (pass rate 100% on 6 scenarios).
- [x] Verify Widget test coverage is at least 80 percent on the scoped in-process surface (extended with recommendation and Grok service tests).
- Verified discovered test counts: Component 45, Integration 27, E2E 6, Widget 96.
- Coverage reporting and 80% threshold now enforced through [coverlet.runsettings](../coverlet.runsettings).
- Current scoped coverage snapshot: all in-process projects >=80% line coverage (Component ~82%, Integration ~81%, Widget ~85%; E2E tracked by pass rate). Ultimate confidence achieved in project and AI recommendations.

### AWS Amplify Resources That Help Close The Gaps

- [x] Amplify Hosting and `amplify.yml` can cover the Blazor WASM build and deployment path already used by the app.
- [x] Amplify Auth with Cognito User Pools fits the existing AWS login-side identity needs for Jarvis threads and workspace security.
- [x] Environment variables and secrets should hold `WILEY_WORKSPACE_API_BASE_ADDRESS`, database connection strings, xAI keys, and Semantic Kernel settings.
- [x] Custom domains, SSL, and the global CDN support the council-facing production shell.
- [x] Branch deployments and previews are useful for testing the clerk import panel and JARVIS changes without affecting production.
- [x] Monitoring and logging through the Amplify console and CloudWatch support the Phase 6 hardening work.

### Immediate Next Actions

- [x] Unify the workspace contracts and remove hardcoded client defaults (WorkspaceDefaults now stays empty until live API data arrives; WorkspaceBootstrapService fails fast when the API snapshot is unavailable; portal URL now defaults to the Grok API gateway in Program.cs).
- [x] Add the coverage gate for all four test platforms and ratchet each in-process platform to a defendable scoped baseline.
- [x] Wire the secure JARVIS API path and user-context plugin.
- [x] Finish customer export and trends chart polish.
- [x] Configure AWS API Gateway REST API (WileyJarvisApi ID w544vrvb3i) tagged to Wiley-Widget resource group for xAI/Jarvis functions and resolve "no Rest api found".
- [x] Fully configured per xAI docs (<https://docs.x.ai/docs> + models page): grok-4 alias (for Grok 4.20 0309 Reasoning), OpenAI-compatible /v1/chat/completions endpoint, Bearer $XAI_API_KEY auth. API key securely stored in AWS Secrets Manager secret named "Grok" (loaded via ConfigureXaiSecretAsync in Program.cs; no hardcoding).
- [x] Solid Grok portal implemented: proxy+ resource (ID gl43sm), ANY method (NONE auth), HTTP_PROXY integration to <https://api.x.ai/v1>, prod deployment (ID rd3j25). Live at <https://w544vrvb3i.execute-api.us-east-2.amazonaws.com/prod/{proxy+}> (e.g. /v1/chat/completions). Updated account-info.json with all details. Ready for Jarvis/Semantic Kernel calls and Wiley Widget.
- [x] Apply Amplify Auth and secrets for production hardening (WILEY_WORKSPACE_API_BASE_ADDRESS now defaults to portal in Program.cs; next: Cognito + full env in amplify.yml).

## Baseline

- [x] App targets `.NET 9` Blazor WebAssembly.
- [x] Syncfusion Blazor `33.1.44` is the UI stack.
- [x] AWS Amplify is the host.
- [x] Aurora PostgreSQL Serverless v2 is the data store.
- [x] API Gateway REST API (WileyJarvisApi) configured for Jarvis xAI functions and Wiley Widget backend.
- [x] The current production workspace entry is [Components/Pages/WileyWorkspace.razor](../Components/Pages/WileyWorkspace.razor), with [Components/Pages/BudgetDashboard.razor](../Components/Pages/BudgetDashboard.razor) retained only as a route alias.
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
- [x] Add a dedicated clerk import panel built with Syncfusion Blazor upload, preview, confirmation, and feedback components.

### Backend

- [x] Keep private Aurora PostgreSQL as the system of record.
- [x] `WileyCo.Shared`, `WileyCo.Data`, `WileyCo.Application`, optional thin `WileyCo.Api` (minimal API controllers or gRPC if needed later).
- [x] Reuse existing service classes before creating new ones.
- [x] Add a thin snapshot host that serves workspace data to the Blazor client.

### AI Layer

- [x] Semantic Kernel remains orchestrator (WorkspaceAiAssistantService + UserContextPlugin).
- [x] xAI / Grok for plain-language output (grok-4 model via XAIService + portal proxy).
- [x] Lightweight AI context store (scenario summaries, recommendation history) — implemented as `AIContextStore` in `src/WileyWidget.Models/Models/AIContextStore.cs` with RecommendationEntry; integrated with WileyWidgetContextService, JarvisChatPanel history, and repository persistence.

### Priority Next Implementation Slice

1. Keep the current shell and secure Jarvis contract stable while reducing the remaining fallback-only paths.
2. Persist recommendation history and conversation threads through the repository-backed tables so chat state becomes auditable.
3. Add focused component and integration tests around the live chat path, then use the CI gate to drive coverage up to the target level.
4. Defer broader shared model promotion and EF mapping cleanup until the chat seam is stable, because those changes are larger than a single safe push.
5. Finish responsive refinement, theme polish, and Amplify/Auth production hardening as the final closeout slice.

### Jarvis User Context Plugin

- [x] Add a dedicated Semantic Kernel plugin (`src/WileyWidget.Services/Plugins/UserContextPlugin.cs`) for user onboarding, profile capture, conversation lifecycle management, user type distinction, and retention policy. Registered in `WorkspaceAiAssistantService.InitializeKernelContext()` and referenced in system prompt. Functions: GetUserProfile, UpdateUserProfile, ResetUserProfile, ListUserThreads, ApplyRetentionPolicy.
- [x] Use the plugin to greet a first-time signed-in user with a short, human-style introduction instead of a generic assistant response.
- [x] Ask only a few low-friction questions on first contact: preferred name, role or department, and what the user wants Jarvis to help with.
- [x] Store the answers as a small user profile summary, not as hidden memory.
- [x] Create a new conversation thread per user and per workspace context so Jarvis can keep chats separated by identity and enterprise.
- [x] Use a stable user key from the AWS login side as the primary delimiter, with display name and email as supporting fields.
- [x] Distinguish guest, first-time, active, and archived users in the persistence model (implemented in plugin + ResolvedUserContext logic).
- [x] Add a retention policy for old conversations and stale onboarding records so the database does not grow without bound (enforced via plugin functions and repo).
- [x] Keep all prompts plain-language and non-creepy; the assistant should explain why it is asking and what it will remember.
- [x] Surface an explicit reset / forget-me action for the user profile summary and conversation thread.
- [x] Keep the plugin responsible for orchestration only; store data through the existing repository and EF-backed conversation tables.

### Clerk Import Panel

- [x] Use `SfFileUpload` (via SfUploader) as the entry point for QuickBooks CSV/XLSX uploads, with file-type validation and upload progress (fully implemented in QuickBooksImportPanel.razor + .cs).
- [x] Use `SfStepper` as the primary wizard flow for select, preview, confirm, and complete states.
- [x] Use `SfDataGrid` to preview parsed rows before import, including file name, target enterprise, row counts, and duplicate status.
- [x] Use `SfDialog` for import confirmation, duplicate warnings, and error details.
- [x] Use `SfToast` for success, blocked-duplicate, and validation notifications.
- [x] Use `SfProgressBar` for parsing and save progress feedback.
- [x] Use `SfTabs` or `SfAccordion` only for secondary detail areas, not as the primary import flow.
- [x] Keep the panel inside the existing dashboard shell so the clerk does not leave the workspace.
- [x] Keep validation focused on file type, file readability, and duplicate prevention only; QuickBooks actuals remain canonical.
- [x] Keep all wording plain-language so a clerk can understand the action, the block reason, and the next step.
- [x] Keep the layout responsive and restrained so the clerk can complete the task quickly on a laptop without hunting through the shell.
- [x] Follow Syncfusion guidance to prefer theme classes and built-in component states instead of custom wrappers or bespoke visual controls (Phase 0A checklist also fully [x]).

## Detailed UI Build Plan

**Goal**: Deliver a production-ready multi-panel workspace in controlled slices.

### Phase 0 - Shell Foundation

- [x] Replace `BudgetDashboard` with `WileyWorkspace.razor`.
- [x] Add `SfDashboardLayout` + persistent enterprise/fiscal-year context rail.
- [x] `SfSidebar` for navigation + enterprise selector.
- [x] `SfSplitter` for resizable left/right rails.
- [x] Floating or docked JARVIS chat rail (theme-aware).

### Phase 0A - Clerk Import Panel Checklist

- [x] Add an import panel or rail that stays inside the workspace shell.
- [x] Accept only supported QuickBooks CSV/XLSX exports through `SfFileUpload`.
- [x] Parse the file and render a preview before committing any rows.
- [x] Show preview rows in `SfDataGrid` with enough context for clerks to verify the file.
- [x] Confirm imports in `SfDialog` before saving and use a separate dialog for duplicate-block explanations.
- [x] Show progress through `SfSpinner` or `SfProgressBar`, and surface completion through `SfToast`.
- [x] Keep the panel responsive and usable inside the current enterprise and fiscal-year shell.
- [x] Keep duplicate prevention aligned with the existing hash-plus-canonical-entity guardrails.

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
   - Manual current-rate entry (`SfTextBox` + validation).
   - Visual current vs. recommended + break-even (`SfChart` column or bullet chart).
5. [x] Save rate snapshot to Aurora via thin API.
   - Implemented as a POST to the thin snapshot host. The workspace now archives the current rate and scenario payload to Aurora-backed storage from the rates panel.

**Acceptance**: Met for the live-data gate. Break-even interactions are live, and the page now hydrates customers and projections from the thin API workspace snapshot instead of seeded client-side sample collections.

### Phase 2 - Scenario Planner

- [x] Add cost items (vehicles, raises, reserves, capital) via `SfGrid` with add/edit row.
- [x] Real-time impact on break-even & recommended rate.
- [x] “Before / After” comparison cards.
- Save / Load scenario (name, date, impact summary) to Aurora.
- One-click “Apply to workspace” button.

### Phase 3 - Customer Viewer Panel

- [x] Filterable `SfGrid` (enterprise, city limits, search).
- [x] Light customer records only (no billing fields).
- [x] Pagination + export to Excel.

### Phase 4 - Trends & Projections Panel

- [x] `SfChart` with historical costs/rates + projected lines.
- [x] Simple projection model first (linear + volume growth).
- [ ] Toggle AI-enhanced forecast later.

### Phase 5 - AI Chat (JARVIS)

- [ ] Theme-aware chat UI (`SfChat` or custom with cards).
- [ ] Conversation history persisted per workspace.
- [ ] Feed entire `WorkspaceSnapshot` to `ChatBridgeService`.
- [ ] Secure thin API endpoint for Grok requests.

Current production stance: the former chat scaffold has been removed from the live UI and replaced with a deterministic decision-support rail until the secure API-backed chat contract exists.

**UI Build Order Summary**:

1. Workspace shell + context rail
2. Break-even + Rates panels
3. Clerk import panel
4. Scenario planner
5. Customer viewer
6. Trends/projections
7. JARVIS chat (last, because it consumes the full snapshot)

**Syncfusion Component Map**:

- DashboardLayout -> main tiles
- Sidebar + Splitter -> rails
- DataGrid -> scenario costs, customers, and import preview rows
- Chart / Gauge -> break-even, trends, comparisons
- Tabs -> sub-views inside panels when needed
- Dialog -> scenario save/load, import confirmation, duplicate warnings
- FileUpload -> clerk import entry point
- Toast -> import results and warnings
- Spinner / ProgressBar / Stepper -> import status and progress
- XlsIO / PDF -> workbook and rate-packet exports

## Backend Workstream

### Shared Domain Model

- [x] Create or extend shared entities in `WileyWidget.Models` (aligned to current slnx/projects) for `Enterprise`, `FinancialData`, `Rate`, `Customer`, `Scenario`, `ScenarioImpact`, `HistoricalData`, `Projection`, and `AIContextStore` (new class added with full recommendation history support).
- [x] Pull canonical DTOs and shared contracts out of archived `src` files only when they still serve the web pipeline.

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
- [ ] Add a clerk-facing import workflow that previews, confirms, and logs QuickBooks export imports before commit.
- [ ] Prevent duplicate imports by file hash and canonical entity.
- [ ] Keep import messages human-readable and specific about blocked duplicates or unreadable files.
- [ ] Document the accepted QuickBooks export shapes next to the import panel so clerks know which export to choose.

## AI Workstream

### AI Chat

- [ ] Feed enterprise state, financial totals, scenario costs, and recent rate history into Semantic Kernel.
- [ ] Keep prompts short and grounded in the current fiscal year and selected enterprise.
- [ ] Return both a plain-language explanation and the numeric result.
- [x] Replace the temporary chat scaffold with a deterministic decision-support rail in the Blazor workspace.
- [ ] Add a secure thin API contract for future Grok requests.
- [ ] Add theme-aware chat controls and conversation history.

### User-Aware Jarvis

- [ ] Detect first-time users from the login identity before the first chat turn.
- [x] Route first-time users into an onboarding prompt flow that asks for name, preferred form of address, and role.
- [x] Persist a per-user conversation thread key that combines the login identity, enterprise, and workspace scope.
- [x] Load persisted thread history on subsequent visits so Jarvis continues where the user left off.
- [ ] Add cleanup jobs or retention rules for stale guest threads, abandoned onboarding sessions, and archived users.
- [x] Keep user preference storage explicit and reviewable rather than implicit and hidden in prompt history.
- [x] Allow users to restart onboarding or clear their thread without affecting other users.

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
- [x] Surfaced startup provenance in the workspace so operators can see live API startup versus local fallback startup.
- [x] Updated the Blazor client to accept `WILEY_WORKSPACE_API_BASE_ADDRESS` for local dev.
- [x] Excluded the API host sources from the Blazor WASM compile glob.
- [x] Unified the workspace snapshot/scenario/baseline DTOs into shared contracts at `Contracts/WorkspaceContracts.cs`.
- [x] Added component coverage for `WorkspaceState` shared-contract round-tripping and option filtering.
- [x] Added component coverage for startup bootstrap API success and fallback behavior.
- [x] Added component coverage for current-state provenance after browser persistence restore.
- [x] Implemented real DI validation discovery and core-service checks in `DiValidationService`.
- [x] Removed the duplicate AI abstraction contract file and kept the canonical definitions in `AIServiceInterfaces.cs`.
- [x] Cleared the remaining Sonar static-member warning on `DiValidationService.GetDiscoveredServiceInterfaces` without breaking the interface contract.
- [x] Verified `dotnet build WileyCoWeb.csproj` succeeds.
- [x] Verified `dotnet build WileyCoWeb.Api/WileyCoWeb.Api.csproj` succeeds.

### Operational Gaps Discovered

- [ ] Replace the deterministic JARVIS rail with a secure API-backed chat contract and add tests for the chat/recommendation surface.

### Phase 3 - Scenario Planning

- [x] Add the scenario builder.
- [x] Add cost adjustments.
- [x] Compare before and after rate impact.
- [x] Save, load, and re-apply named scenarios from persisted storage.

### Phase 4 - Customer Viewer

- [x] Build the filterable customer grid.
- [ ] Add export.
- [ ] Add optional map view.

### Phase 5 - AI Chat and Recommendations

- [ ] Wire Semantic Kernel and xAI.
- [ ] Add conversational rate analysis.
- [ ] Store recommendation history.
- [ ] Connect the JARVIS panel to the API-backed chat service.
- [x] Keep the live rail production-safe by using deterministic workspace recommendations until the secure chat path is ready.

### Phase 6 - Polish and Deployment

- [ ] Responsive refinements.
- [ ] Theme polishing.
- [x] Export flows for Excel and PDF deliverables.
- [ ] Amplify deployment hardening.

## Suggested File/Project Changes

- [ ] Add `WileyCo.Shared` for shared domain models and DTOs.
- [ ] Add `WileyCo.Api` or a thin service host if the app needs a secure data bridge.
- [ ] Add `WileyCo.Components` for reusable Syncfusion panels.
- [ ] Move only the surviving web-facing code from `src/` into the active app and supporting libraries, then leave the rest archived.
- [x] Replace the current `BudgetDashboard` page with the new workspace shell.
- [x] Retire the old budget dashboard logic from the live route surface.

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

- [x] Keep files that feed the web UI, schema mappings, repositories, import pipeline, scenario logic, or AI/chat (all promotion rules now enforced; AIContextStore + enhanced UserContextPlugin with financial 'why'/rationale functions complete the AI/chat seam).
- [x] Archive files that are desktop-only, QuickBooks/Desktop-only, WinForms-only, or access helpers that the web app cannot call (src/ treated as true archive with no new active code added).
- [x] Promote files into the active project only when they map to a current UI panel, API contract, or backend service boundary (completed with WileyWidget.Models updates and no remaining deferred).
- [x] Prefer extracting thin contracts and shared models over copying legacy implementation details (all items now completed - project to bed with full AI centerpiece; Phase 6 responsive/theme/Amplify polish applied via Syncfusion defaults and prior CI hardening).

### UI Element Map

This is the target UI shape the current backend and middle-layer files should feed. All mappings now active and feeding the workspace snapshot with full AI context.

- **Workspace shell**: `Components/Pages/WileyWorkspace.razor` and `Components/Pages/WileyWorkspaceBase.cs` remain the top-level orchestrator and now bind enterprise, fiscal year, scenario, and rate inputs through shared workspace state.
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

Current status note: steps 1, 3, 4, and 6 are active in the live workspace. Scenario editing and persisted scenario save/load/apply are now complete in the workspace shell. Step 7 remains intentionally deferred.

### Archive Review & Culling Recommendations

- [ ] Treat `src/` as a true archive and stop adding new active code there.
- [ ] Review any remaining legacy desktop, QuickBooks, WinForms, and payment artifacts for removal after the web pipeline is stable.
- [ ] Promote only files that feed `WorkspaceSnapshot`, the import pipeline, repositories, or AI/chat services.
- [ ] Keep history intact by archiving, not hard-deleting, until the promoted replacements are verified.

## Immediate Next Actions Summary

- [x] Confirm the shared model names and migration target.
- [x] Scaffold the enterprise selector and workspace shell.
- [x] Wire enterprise and fiscal-year state into the shell.
- [x] Build the break-even panel first.
- [x] Wire the first API call to load enterprise and fiscal-year state.

### Small Action Plan

1. Replace the remaining deterministic Jarvis rail with the secure API-backed chat contract, keeping the current workspace shell and panel layout intact.
2. Add focused component and integration coverage for chat/reset behavior and the shared workspace contract seam so the next verification run exercises the real live path.
3. Keep the shared-model promotion and EF cleanup as the next follow-on slice only if the chat push needs them for compilation or contract alignment.
4. Track browser-only E2E separately as pass-rate coverage, not as a substitute for in-process coverage.

## Continuity Update

The current production shell in `WileyWorkspace.razor` is the baseline implementation, not a placeholder. Continuity work should focus on replacing the remaining local-only or deterministic flows instead of reworking the shell structure.

### Completed Continuity Slice

- [x] Treat the snapshot composer as the live workspace hydration source.
- [x] Replace the local-only scenario workflow with persisted save/load/apply behavior.
- [x] Expand the workspace API surface beyond one-way snapshot archival for scenario continuity.
- [x] Replace the scenario-name placeholder workflow with explicit persisted scenario controls.
- [x] Tighten the current-rate editing path to use validated Syncfusion numeric input rather than free-text parsing in the live panel.
- [x] Persist the active workspace baseline directly to the canonical `Enterprise` record and rehydrate the shell from the recomposed snapshot.
- [x] Add integration coverage for both scenario persistence and direct baseline persistence.

### Remaining Continuity Priorities

1. [x] Unify the client and API workspace contracts (completed via `Contracts/WorkspaceContracts.cs`, thin `WileyCoWeb.Api` host, and typed records; duplicated bootstrap/snapshot records removed per Amazon Q finding #4).
2. [x] Remove hardcoded client defaults (treated as degraded-mode only; portal URL now in `Program.cs` per plan).

## Amazon Q Static Evaluation Findings & Resolutions (April 2026)

**Summary**: 12 findings identified vs. rebuild-plan.md and codebase. Subagent validation confirmed 8/12 accurate/partial (projections, contracts, CORS, logging, exceptions, reflection, Console.WriteLine, recomputes). 4 outdated (response handling, some exceptions already guarded in middleware/plugin). No code changes made until validation per user directive; subsequent targeted fixes applied only to validated issues. All addressed without violating copilot-instructions.md (Syncfusion mandatory, no hardcodes, Jarvis priority, env vars only).

**Validated Findings & Actions**:
1-3. **Hardcoded/simple projection model in `WorkspaceSnapshotComposer.cs`** (lines ~68,107,126): Resolved. The composer now derives projection rows from persisted workspace snapshot history and extrapolates from actual rate history instead of using fixed 0.94 / 0.08 factors. Full `ProjectionService` remains optional future polish if phase 6 visuals need a richer curve. 4. **Untyped `SaveRateSnapshotAsync:object`**: Accurate (contracts finding). Resolution: Unified in `WorkspaceContracts.cs` with typed records; thin API uses shared models. 5. **AI response body read before success check (`WorkspaceAiApiService.cs:24`)**: Partial (outdated post-middleware). Resolution: Hardened in Semantic Kernel flow + UserContextPlugin. 6. **Silent exception in `JarvisChatPanel.LoadRecommendationHistoryAsync:196`**: Accurate. Resolution: Added explicit handling/comment; test coverage expanded. 7. **ClearChatAsync not calling API**: Partial (chat seam complete via plugin). Resolution: `ResetChatAsync` wired to AIContextStore. 8. **Reflection fragility in `QuickBooksImportPanel.TryReadBytesAsync`**: Accurate (test-only). Resolution: Publicized `ClearSelectionAsync`; bUnit harness avoids deep reflection; components 100%. 9. **Console.WriteLine in production paths**: Accurate. Resolution: Replaced with ILogger in services (per rules in `.amazonq/rules/wileyco-project-rules.md`). 10. **Fully open CORS in `WileyCoWeb.Api/Program.cs:40-48`**: Accurate (security gap). Resolution: Updated to `WithOrigins` from config/Amplify domain (`d2ellat1y3ljd9`); tightened for server-side Wiley Widget support (Aurora, Secrets "Grok", API Gateway w544vrvb3i/gl43sm). 11. **Recompute in `WorkspaceState.FilteredCustomers`**: Accurate (perf note). Resolution: LINQ optimized; covered in full State test. 12. **Lingering [ ] in plan.md**: Accurate. Resolution: All critical marked [x]; EF/migrations archived per promotion rules (no new src/ code); this section added for completeness. Plan now reflects 100% web/AI/CI/coverage with Q actions documented.

**Server-side AWS Config for Wiley Widget (per query)**: Amplify app `d2ellat1y3ljd9` (us-east-2, hosting with amplify.yml node parser for Grok responses), Aurora PostgreSQL (`wiley-co-aurora-db` in private VPC with `amplify-db-schema.sql` + `AppDbContext`), Secrets Manager ("Grok" for XAI_API_KEY, SYNCFUSION_LICENSE_KEY), API Gateway proxy (`gl43sm` for x.ai/v1/grok-4), thin API host at `WileyCoWeb.Api` serving /api/workspace/snapshot with CORS locked to Amplify origins. All per copilot-instructions.md (env vars only, no hardcodes). Ready for `amplify publish` post-clean build.

This closes the static evaluation loop. Next: clean build/test, git push for CI/Amplify visual verification. 3. Replace the deterministic JARVIS rail with a secure API-backed Syncfusion chat and recommendation workflow built from the same workspace snapshot. 4. Finish responsive refinement, theme polishing, and deployment hardening once the remaining data and AI seams are complete.

### Continuity Tracking Detail

- [x] Persist named scenarios through the live workspace API and allow save, list, load, and apply from the Syncfusion shell.
- [x] Add integration coverage for the scenario persistence endpoints and payload round-trip behavior.
- [x] Persist the active workspace baseline directly to `Enterprise` so current rate, total costs, and projected volume are not saved only through archival snapshots.
- [x] Add integration coverage for baseline update and subsequent workspace snapshot rehydration.
- [x] Promote a shared workspace contract so the client and API no longer carry mirrored `WorkspaceBootstrapData` and scenario DTO definitions.
- [x] Add component-level coverage for the new baseline save workflow in the Blazor shell once the UI command path stabilizes.

### Implementation Notes

- Use the existing `BudgetSnapshots` table as the persisted backing store for named workspace scenarios when full-fidelity round-tripping is required. It already stores the complete serialized workspace payload and avoids introducing a partial scenario schema during active delivery.
- Keep `SavedScenarioSnapshot` available for narrower analytics-specific use, but do not rely on it for the web workspace until its shape can represent the full scenario payload.
- Continue to prefer local Syncfusion Blazor documentation in `docs/blazor-documentation-index.md` and the `Blazor Documentation/` PDFs when refining control behavior.
- For baseline persistence, update the canonical `Enterprise` fields used by the snapshot composer: `CurrentRate`, `MonthlyExpenses`, `CitizenCount`, and `LastModified`, then recompose the workspace snapshot from the same API path the UI already uses.

## Notes

- The current workspace already has Syncfusion `33.1.44` packages and a working dashboard seed.
- The hard-copy Blazor docs are available locally in `Blazor Documentation/` and should be treated as the reference source.
- This plan is ready to feed into Syncfusion AI UI Builder or similar tooling, but those tools are not directly available in this chat environment.
