# Testing Gap Analysis - 2026-04-22

## Scope

This document summarizes the current measurable test-coverage gaps for the four quality bars in this repository:

- Unit: `tests/WileyWidget.Tests`
- Component: `tests/WileyCoWeb.ComponentTests`
- Integration: `tests/WileyCoWeb.IntegrationTests`
- Playwright: `tests/playwright`

The intent is not to list every uncovered line. The goal is to identify the smallest set of missing test surfaces most likely to move each suite past `80%`, while noting where the current gate scope may be too broad to reach `80%` efficiently.

## Current Baseline

### Coverage targets

- Component target: `80%`
- Integration target: `80%`
- Unit/widget target: `80%`
- Playwright target in repo policy: `100%` pass rate, with `>80%` effective execution coverage as the minimum intermediate bar

### Current measured state

| Suite       | Current line coverage / pass rate |                       Target |                  Gap | Notes                                                                                                        |
| ----------- | --------------------------------: | ---------------------------: | -------------------: | ------------------------------------------------------------------------------------------------------------ |
| Component   |                          `74.24%` |                        `80%` |           `5.76 pts` | `3252 / 4380` lines covered                                                                                  |
| Integration |                          `37.02%` |                        `80%` |          `42.98 pts` | Latest available Cobertura artifact is from the rerun output and may move slightly after a fully green rerun |
| Unit        |                          `58.07%` |                        `80%` |          `21.93 pts` | `5857 / 10086` lines covered                                                                                 |
| Playwright  |      `30.26%` effective pass rate | `>80%` interim, `100%` final | `49.74 pts` to `80%` | Latest strict report recorded `23 expected`, `50 skipped`, `3 unexpected` out of `76` tests                  |

### Approximate additional lines / passing tests needed

| Suite       |                                                    Additional lines or tests needed to clear `80%` |
| ----------- | -------------------------------------------------------------------------------------------------: |
| Component   |                                                                     about `252` more covered lines |
| Integration |                                                                    about `6714` more covered lines |
| Unit        |                                                                    about `2212` more covered lines |
| Playwright  | at least `38` more passing tests to exceed `80%`; `53` to reach `100%` from the last strict report |

## Executive Summary

### What is close

Component coverage is close enough that a focused pass on three surfaces should plausibly get it over `80%`:

- `Components/QuickBooksImportPanel.razor`
- `Components/Panels/CustomerViewerPanel.*`
- low-covered async branches in `Components/Pages/WileyWorkspaceBase.cs`

### What is materially under-covered

Unit and integration are not missing a few assertions. They are missing entire behavior families.

- Unit is thin in plugins, AI/recommendation services, routing logic, analytics edge cases, and model behavior for AI/health payloads.
- Integration is extremely broad in scope. The current aggregate includes API startup/configuration, repositories, services, models, and middleware. Reaching `80%` on that aggregate will require either a large expansion of integration coverage or a deliberate narrowing of what the integration gate is meant to measure.

### What is blocking Playwright

The Playwright problem is not breadth first. The suite already covers most major workspace routes. The blockers are:

- skipped tests counting against the strict gate
- real API-backed QuickBooks routing instability
- incomplete cross-browser parity

## Component Gap Analysis

### Current strength

The component suite already covers the main shell and several important behaviors:

- workspace shell and panel routing
- Jarvis chat panel basics
- data dashboard rendering
- QuickBooks import chrome
- startup/local-settings behavior

### Highest-yield missing component coverage

#### 1. QuickBooks import interaction branches

Current weak spots from Cobertura:

- `Components/QuickBooksImportPanel.razor` at about `55.6%`
- `BuildAssistantContextSummary`
- routing option calculation and parameter-set branches
- async save / assistant / validation branches that are only partially covered

Missing tests likely to move coverage quickly:

- routing rule add/edit/remove flows in component state, not only markup presence
- assistant prompt failure and empty-context paths
- invalid file handling beyond unsupported upload chrome
- duplicate detection, reset, and post-commit UI state transitions
- allocation-profile editing and empty-profile branches

#### 2. Customer Viewer save and filtering behavior

Current weak spots from Cobertura:

- `Components/Panels/CustomerViewerPanel.Bindings.cs` at about `40%`
- `Components/Panels/CustomerViewerPanel.razor.cs` at about `56.15%`
- `Components/Panels/CustomerViewerPanel.Shared.cs` at about `60.41%`
- `Components/Panels/CustomerViewerPanel.razor` at about `60%`

Missing tests likely to move coverage quickly:

- save validation failures and inline-error states
- edit dialog paths for partial / malformed addresses
- city-limits and service-filter combinations beyond the current basic flow
- save-cancel-reopen flows that validate state reset
- delete / load-failure / degraded directory branches in the component, not only in Playwright

#### 3. Wiley workspace async actions

Current weak spots from Cobertura:

- `ApplySelectedScenarioAsync` branch in `Components/Pages/WileyWorkspaceBase.cs` at about `18.51%`
- partial coverage in `RefreshScenarioCatalogAsync`, `ReloadWorkspaceAsync`, `SaveRateSnapshotAsync`, `SaveScenarioAsync`, `TrackNavigationClickAsync`

Missing tests likely to move coverage quickly:

- apply-selected-scenario success path with refreshed state
- scenario catalog refresh failure / no-selection / invalid-selection branches
- baseline save and rate-snapshot failure banners
- telemetry fire-and-forget path for navigation tracking
- workspace reload after changed fiscal year / enterprise selection

### Component recommendation

If the goal is to cross `80%` quickly, prioritize:

1. QuickBooks import component behavior
2. Customer viewer save and validation behavior
3. Wiley workspace async save / reload / apply-scenario branches

This is the most realistic suite to push over target in the next focused test pass.

## Unit Gap Analysis

### Current strength

The unit suite already exercises a meaningful set of services:

- `WorkspaceAiAssistantServiceTests`
- `WorkspaceKnowledgeServiceTests`
- `QuickBooksImportServiceTests`
- `AnalyticsServicePhase1Tests`
- `AdaptiveTimeoutServiceTests`
- `ErrorReportingServiceTests`

### Highest-yield missing unit coverage

#### 1. Plugin surfaces are mostly uncovered

Current weak spots from Cobertura:

- `src/WileyWidget.Services/Plugins/UserContextPlugin.cs` at about `6.97%`
- `src/WileyWidget.Services/Plugins/Development/CodebaseInsightPlugin.cs` at about `1.38%`

Missing tests:

- successful plugin invocation with realistic workspace context
- null / missing-dependency handling
- malformed prompt / malformed context handling
- output shape validation for plugin return values

#### 2. Recommendation and AI-adjacent services are under-covered

Current weak spots:

- `src/WileyWidget.Business/Services/GrokRecommendationService.cs` at about `23.33%`
- several low-covered branches in `src/WileyWidget.Services/WorkspaceAiAssistantService.cs`

Missing tests:

- prompt-construction branches for enterprise / fiscal-year variants
- transient failure, timeout, and fallback handling
- malformed xAI / legacy response parsing
- conversation persistence edge cases that do not surface through the existing happy-path tests

#### 3. Analytics and routing logic are still thin

Current weak spots:

- `src/WileyWidget.Services/AnalyticsService.cs` at about `39.53%`
- `src/WileyWidget.Services/QuickBooksRoutingService.cs` at about `41.03%`

Missing tests:

- reserve forecast dependency failures
- empty-sequence, negative-value, and mixed-scope analytics branches
- routing precedence, inactive rules, profile-assignment, and no-match fallbacks
- memo / account / enterprise alias collisions in routing selection

#### 4. DTO / model behavior is dragging aggregate unit coverage

Current weak spots include AI and health models such as:

- `AnalyticsData`
- `BudgetInsights`
- `ComplianceReport`
- `HealthCheckModels`
- `FiscalYearInfo` and `FiscalYearSettings`

Missing tests:

- validation behavior
- calculated members / formatting helpers
- equality, serialization, and default-construction behavior for contract-like models that carry logic

### Unit recommendation

If the goal is to cross `80%`, the highest-yield unit additions are:

1. `UserContextPlugin` and `CodebaseInsightPlugin`
2. `GrokRecommendationService`
3. `QuickBooksRoutingService`
4. `AnalyticsService` edge cases
5. model-behavior sweeps for AI and health structures that currently contain executable logic

Without plugin coverage, this suite will continue to lag badly.

## Integration Gap Analysis

### Current strength

The integration suite already covers several meaningful API slices:

- workspace snapshot
- workspace knowledge
- workspace AI chat / recommendation history
- QuickBooks import and routing APIs
- reference-data import
- health checks and smoke paths

### Core issue

The integration gate is aggregating coverage across a very wide surface:

- `WileyCoWeb.Api`
- data repositories
- service-layer classes
- models and contracts pulled into the hosted app

That is why `66` passing tests still produce only about `37%` line coverage.

### Highest-yield missing integration coverage

#### 1. Repository and data-layer branches

Very low-covered files include:

- `src/WileyWidget.Data/EnterpriseRepository.cs` at about `9.72%`
- `src/WileyWidget.Data/AuditRepository.cs` at about `25.71%`
- `src/WileyWidget.Data/BudgetRepository.cs` at about `37.03%`
- `src/WileyWidget.Data/BudgetAnalyticsRepository.cs` at about `61.84%`
- `src/WileyWidget.Data/AppDbContextFactory.cs` at about `45.71%`

Missing tests:

- enterprise query / alias / filter combinations
- audit-log write and retrieval behavior
- budget repository read / write / not-found branches
- reserve history and top-variance branches with imported actuals vs no actuals
- context factory behavior under development configuration overrides

#### 2. API startup, middleware, and secret/config resolution

Very low-covered files include:

- `WileyCoWeb.Api/Middleware/ExceptionLoggingMiddleware.cs` at about `22.72%`
- `WileyCoWeb.Api/Configuration/LicenseBootstrapper.cs` at about `44.64%`
- `WileyCoWeb.Api/Configuration/SecretResolver.Helpers.cs` at about `38.13%`
- `WileyCoWeb.Api/Configuration/StartupConfigurationService.cs` partial low branches

Missing tests:

- unhandled exception in request pipeline with log capture and preserved failure response
- Syncfusion key lookup precedence across local settings, env, and Windows machine/user env
- xAI / secret-name alias selection branches
- degraded-startup vs real-database validation branches

#### 3. AI and analytics API-backed service flows

Low-covered files include:

- `src/WileyWidget.Services/WorkspaceAiAssistantService.cs` at about `32.8%`
- `src/WileyWidget.Services/AnalyticsService.cs` at about `26.74%`
- `src/WileyWidget.Services/DataAnonymizerService.cs` at about `3.46%`
- `src/WileyWidget.Services/WorkspaceKnowledgeService.cs` dependency-loading branches

Missing tests:

- AI fallback / unavailable-runtime paths through the API layer
- analytics dependency failures and reserve-forecast branches from integrated data
- data anonymizer end-to-end behavior through whatever API or service-hosted path actually exercises it
- knowledge-building under alternate enterprise scopes and missing dependencies

#### 4. Reference-data import orchestration branches

Low-covered files include:

- enterprise-alias resolution in `WorkspaceReferenceDataImportService.cs`
- budget-actual refresh and orchestration branches in `WorkspaceReferenceDataImportService.Orchestration.cs`

Missing tests:

- alias collisions and enterprise-name normalization
- partial import sets with customer-only or ledger-only payloads
- duplicate / missing-file / malformed-file branches
- sample-ledger disabled vs enabled paths

### Integration recommendation

The shortest path to better integration coverage is not more smoke tests. It is deeper data-backed scenario tests around:

1. repositories
2. startup/configuration middleware
3. AI fallback and analytics service flows
4. reference-data orchestration edge cases

### Important realism note

An `80%` integration threshold on the current aggregate scope is likely not efficient. If the desired meaning of integration coverage is "API and persistence seams," consider narrowing the gate to API / repository / service assemblies that integration tests are supposed to own. If the current scope remains unchanged, expect a large amount of additional work before `80%` is realistic.

## Playwright Gap Analysis

### Current strength

The Playwright suite already spans the main workspace breadth:

- overview shell
- break-even
- rates
- scenario planner
- customer viewer
- trends
- decision support
- data dashboard
- QuickBooks import
- baseline save
- Syncfusion render / visual checks

There are currently `19` spec files covering `76` recorded tests in the latest strict report.

### Current blocker categories

#### 1. Skip-driven coverage loss

The latest strict report recorded:

- `23 expected`
- `50 skipped`
- `3 unexpected`

That means the main Playwright problem is execution coverage, not missing route inventory.

#### 2. Real API-backed QuickBooks instability

`tests/playwright/quickbooks-routing-real.spec.ts` was previously blocked by missing API startup on `127.0.0.1:5231`. The local Playwright config now starts both the client and API, but the real-routing path still needs a clean rerun to prove stability.

#### 3. Cross-browser parity gaps

The suite has historically leaned on Chromium-only behavior for visual tests and some route-specific proofs. Those paths need assertion-based fallbacks instead of skips so WebKit contributes to pass rate.

### Highest-yield missing Playwright work

#### 1. Stabilize real-routing and API-backed scenarios

Missing or not-yet-stable proofs:

- QuickBooks routing save + preview + commit with live API
- import-history refresh after real commit
- workspace shell boot when the API is actually reachable instead of fallback-only

#### 2. Remove all skip-based gaps

The suite needs:

- non-Chromium assertion fallbacks for visual tests
- fallback assertions when Jarvis secure surface is absent
- no project-based `skip` paths in strict mode

#### 3. Fill missing business-critical journeys, not more panel smoke

The best remaining Playwright additions are:

- exported document flows with download assertions
- enterprise/fiscal-year selection followed by reload and scenario refresh
- save baseline, apply scenario, and deep-link reload persistence across routes
- degraded API banner paths and reconnect / refresh UX

### Playwright recommendation

If the objective is `>80%` effective browser coverage quickly, do this first:

1. get `quickbooks-routing-real.spec.ts` stable under the managed local API + client setup
2. remove every remaining `skip` from strict-mode paths
3. add one real end-to-end proof for baseline save / reload / scenario apply persistence

After that, the suite can push toward the repo's stricter `100%` pass-rate requirement.

## Recommended Delivery Order

### Phase 1: Fastest path to visible improvement

1. Component: QuickBooks import + customer viewer + workspace async action branches
2. Playwright: remove skips and stabilize QuickBooks routing real flow

### Phase 2: Highest-return unit additions

1. plugins
2. routing service
3. analytics service edge cases
4. recommendation service branches

### Phase 3: Integration depth

1. repository scenarios
2. startup / middleware / secret resolution
3. reference-data orchestration edge cases
4. AI fallback and analytics dependency failures

## Recommended Reference Files

- `.github/coverage-baselines.json`
- `tests/TestResults/coverage/WileyCoWeb.ComponentTests/a61d233c-c6d1-4675-b12c-9352bad6a4ae/coverage.cobertura.xml`
- `tests/TestResults/coverage/WileyCoWeb.IntegrationTests-rerun/ce8b97fd-6a90-403e-9a28-96247cfae0ae/coverage.cobertura.xml`
- `tests/TestResults/coverage/WileyWidget.Tests-rerun/b9eb6779-f1bd-4794-afde-c98d8f39e587/coverage.cobertura.xml`
- `playwright-report/results.json`
- `tests/playwright/workspace-shell-production-ready.spec.ts`
- `tests/playwright/workspace-syncfusion-controls.spec.ts`
- `tests/playwright/quickbooks-routing-real.spec.ts`

## Bottom Line

- Component can likely be pushed over `80%` with a focused branch-coverage pass.
- Unit needs a deliberate push into plugins, routing, analytics, and AI service branches.
- Integration is the largest problem and may need either major expansion or a gate-scope decision.
- Playwright already has broad route coverage; the real gap is stable execution coverage and elimination of skips.
