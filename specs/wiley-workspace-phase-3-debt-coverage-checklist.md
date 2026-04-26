# Wiley Workspace Phase 3 Checklist: DSCR & Bond Covenant Dashboard

## Scope

Phase 3 adds a new workspace panel for Debt Service Coverage Ratio and bond covenant monitoring. The panel must be routeable from the Wiley workspace shell, render correctly with Syncfusion components, compute DSCR from existing budget/revenue data, and be proved by deterministic Playwright tests.

## Authoritative Guidance Sources

- Use [Wiley Syncfusion Expert](../.github/agents/wiley-syncfusion-expert.agent.md) for control selection, responsive composition, accessibility, and Syncfusion-specific constraints.
- Use [playwright-test-planner](../.github/agents/playwright-test-planner.agent.md) to define browser proofs before writing tests.
- Use [playwright-test-generator](../.github/agents/playwright-test-generator.agent.md) to generate the proof spec from the plan.
- Use [playwright-test-healer](../.github/agents/playwright-test-healer.agent.md) only if a real Playwright failure appears.

## Phase 3 Implementation Checklist

### 1. Lock the data contract first

- [ ] Define the DSCR service contract around existing data sources only.
- [ ] Confirm which existing aggregates supply revenue, principal, interest, and reserve contribution.
- [ ] Keep the contract small and explicit: current DSCR, covenant threshold, covenant status, revenue total, debt service total, reserve contribution, and any headroom or breach message.
- [ ] Add or confirm a small DTO set in the contracts layer if the panel needs typed waterfall points or covenant summaries.
- [ ] Add unit tests for the math before any UI work.

### 2. Build the service behind the panel

- [ ] Create `DebtCoverageService` in the existing services layer.
- [ ] Reuse current budget and revenue analytics inputs rather than introducing a new debt ledger.
- [ ] Compute DSCR deterministically from the live aggregates.
- [ ] Classify covenant state with clear thresholds, at minimum below covenant, near covenant, and healthy coverage.
- [ ] Return waterfall-friendly values for revenue, debt service, and reserves.
- [ ] Add service tests for happy path, covenant breach, exact-threshold boundary, and missing-data fallback.

### 3. Wire the route into the workspace shell

- [ ] Add `debt-coverage` to the shell panel registry in [Components/Pages/WileyWorkspaceBase.cs](../Components/Pages/WileyWorkspaceBase.cs).
- [ ] Update `NormalizePanelKey` so the route resolves deterministically.
- [ ] Add the panel case to the `switch` that renders workspace panels.
- [ ] Add a nav item or overview launch tile only if it materially improves discoverability.
- [ ] Verify the route deep-links cleanly at `/wiley-workspace/debt-coverage`.

### 4. Design the panel with Syncfusion expert guidance

- [ ] Ask [Wiley Syncfusion Expert](../.github/agents/wiley-syncfusion-expert.agent.md) for the final control composition before implementing markup.
- [ ] Use `SfCircularGauge` for the DSCR needle and make the covenant threshold visually obvious.
- [ ] Use `SfChart` with `ChartSeriesType.Waterfall` for the revenue → debt service → reserves story.
- [ ] Keep the gauge and waterfall readable on both desktop and narrow desktop layouts.
- [ ] Add stable ids to the panel root, gauge, waterfall chart, covenant summary, and any input or threshold control.
- [ ] Include accessible labels and descriptions so the panel can be proven without brittle selectors.

### 5. Implement the panel markup

- [ ] Create [Components/Panels/DebtCoveragePanel.razor](../Components/Panels/DebtCoveragePanel.razor).
- [ ] Add a clear header explaining the DSCR and covenant purpose.
- [ ] Render the gauge as the primary visual and place the waterfall in a secondary but still prominent region.
- [ ] Show a concise covenant summary card or status strip with the DSCR value and threshold.
- [ ] Include empty and fallback states so the panel never renders as a blank shell.
- [ ] Keep the style consistent with [Components/Panels/DataDashboardPanel.razor](../Components/Panels/DataDashboardPanel.razor) and [Components/Panels/ReserveTrajectoryPanel.razor](../Components/Panels/ReserveTrajectoryPanel.razor).

### 6. Register services and dependencies

- [ ] Register `DebtCoverageService` in the client or shared service container where the panel is resolved.
- [ ] If the service needs repository access, wire the smallest interface possible.
- [ ] Keep the data flow aligned with existing budget and analytics registrations.
- [ ] Add any needed DTOs or helper records to the existing contracts namespace instead of embedding anonymous shapes in the component.

### 7. Add component-level proof

- [ ] Add a deterministic component test for `DebtCoveragePanel`.
- [ ] Assert the panel root, DSCR gauge, waterfall chart, and covenant summary render with seeded data.
- [ ] Assert the threshold/breach copy changes when the DSCR input or service result changes.
- [ ] Assert fallback or empty state behavior if data is missing.

### 8. Plan the browser proof with the Playwright planner agent

- [ ] Use [playwright-test-planner](../.github/agents/playwright-test-planner.agent.md) to create the browser proof plan.
- [ ] Include a direct route proof for `/wiley-workspace/debt-coverage`.
- [ ] Include a render proof for `#debt-coverage-panel`, `#debt-coverage-dscr-gauge`, and `#debt-coverage-waterfall-chart`.
- [ ] Include a threshold-change proof if the panel exposes a threshold input or covenant selector.
- [ ] Include a refresh proof so the panel does not fall back to overview or a blank shell.

### 9. Generate the Playwright test with the generator agent

- [ ] Use [playwright-test-generator](../.github/agents/playwright-test-generator.agent.md) to create one focused spec per scenario in the plan.
- [ ] Reuse `gotoWorkspacePanel` and `waitForWorkspaceShell` from [tests/playwright/support/workspace.ts](../tests/playwright/support/workspace.ts).
- [ ] Prefer role selectors and stable ids over raw CSS traversal.
- [ ] Keep the test assertions user-visible: route, gauge, waterfall, covenant state, and refresh stability.

### 10. Heal failures only with the healer agent

- [ ] If Playwright fails, use [playwright-test-healer](../.github/agents/playwright-test-healer.agent.md).
- [ ] Diagnose the actual failing selector, route, timing, or data issue.
- [ ] Fix the root cause in the app or the spec, then rerun the same focused test.
- [ ] Do not hide failures with skips, fixmes, or broad wait inflation.

## Detailed Done Criteria

- [ ] The phase-3 route exists and deep-links cleanly.
- [ ] The panel renders with stable ids and readable Syncfusion controls.
- [ ] DSCR is computed from the existing data path, not a stub.
- [ ] The waterfall story is visible and understandable in the browser.
- [ ] The covenant status is displayed and responds to the underlying values.
- [ ] The component test passes.
- [ ] The Playwright spec passes in the configured browser matrix.
- [ ] Any browser failure has been healed at the root cause.

## Recommended Execution Order

1. Review the Syncfusion expert guidance and lock the panel layout.
2. Implement the service contract and math.
3. Wire the route and shell entry.
4. Build the panel and add empty/error states.
5. Add component tests.
6. Ask the Playwright planner agent for the browser proof plan.
7. Generate the Playwright spec.
8. Heal any real failures with the Playwright healer agent.
