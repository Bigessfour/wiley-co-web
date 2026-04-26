# Test Healing Plan

## Goal

Restore a stable, fully runnable test suite by fixing the launch path first, then the route-specific browser proofs, then the remaining component and CI gate failures.

## Current State

- The Playwright suite now has route-specific files for shell persistence, customer viewer, analytics, and risk/recovery coverage.
- The current blocker is not a specific route assertion; the worker starts, but Chromium does not spawn in the short validation runs.
- Earlier component work fixed the CustomerViewerPanel compile regression and the break-even state recomputation path.
- Remaining known component failures are:
  - `AffordabilityDashboardPanel_RendersManualMhiSummaryHeatmapAndCharts_WhenCustomersArePresent`
  - `WileyWorkspace_DecisionSupportRoute_RendersJarvisSyncfusionAssistSurface`
  - `WileyWorkspace_RendersCoreShellSections`

## Plan

### Phase 1. Stabilize the Playwright launch path

1. Clear stale browser and worker processes before every local browser run on Windows.
2. Verify the app and API ports are already listening before launching Playwright.
3. Run one seed test with the shortest possible timeout and debug logging enabled.
4. If Chromium still does not spawn, inspect the Playwright browser launch logs and the worker process tree.
5. Once the browser launch succeeds once, lock that command shape into the local validation workflow.

### Phase 2. Prove the shell route first

1. Run only the shell persistence spec.
2. Confirm the workspace shell reaches `#workspace-dashboard`, `#workspace-navigation-card`, and `#workspace-status-card`.
3. Confirm navigation and localStorage persistence survive a reload.
4. If the shell spec hangs, simplify `waitForWorkspaceShell` until it waits only on elements that are always present in the current runtime.

### Phase 3. Fix the customer viewer route

1. Validate the customer directory render path with a mocked or seeded customer list.
2. Confirm search, clear-filters, refresh, and add-customer dialogs all render without waiting on unrelated shell state.
3. If the customer viewer has render timing issues, split the current proof into separate load, search, and editor tests.
4. Keep the customer API route handler deterministic so the test does not depend on live data variance.

### Phase 4. Fix analytics and AI routes

1. Validate Decision Support first, because it has the most conditional rendering.
2. Treat the Jarvis surface as two acceptable states: assistant-enabled or fallback guidance.
3. Verify Data Dashboard and Trends separately so a failure in one chart container does not mask the others.
4. Keep the mocked AI responses deterministic and minimal.

### Phase 5. Fix risk and recovery routes

1. Validate Debt Coverage with a mocked API response that exercises the gauge and threshold input.
2. Validate Capital Gap with a mocked API response that renders the summary, chart, and breakdown.
3. Add a negative-path fallback check for each route so empty or failed API responses are covered explicitly.

### Phase 6. Restore the remaining component tests

1. Fix `AffordabilityDashboardPanel_RendersManualMhiSummaryHeatmapAndCharts_WhenCustomersArePresent` by checking the route data and the expected manual MHI state transitions.
2. Fix `WileyWorkspace_DecisionSupportRoute_RendersJarvisSyncfusionAssistSurface` by aligning the test with the actual direct-route rendering behavior and avoiding duplicate surface assumptions.
3. Fix `WileyWorkspace_RendersCoreShellSections` by verifying the shell waits on the same host elements that the current runtime actually renders.
4. Re-run the full component suite after each fix, not only the individual failing test, so regressions are caught early.

### Phase 7. Rebuild CI confidence

1. Re-run the component suite without coverage.
2. Re-run the component suite with CI-style coverage.
3. Re-run the other matrix legs that enforce the coverage baselines.
4. Compare local and remote coverage artifacts only after the test pass rate is stable.
5. Keep the coverage gates intact unless a route or file is proven to be impossible to exercise meaningfully.

## Execution Order

1. Launch-path debug on a single seed test.
2. Shell persistence spec.
3. Customer viewer spec.
4. Analytics spec.
5. Risk and recovery spec.
6. The three known failing component tests.
7. Full component suite.
8. CI coverage run.

## Acceptance Criteria

- A single seeded Playwright test starts Chromium reliably on demand.
- Each route-specific spec produces a pass or a real assertion failure instead of stalling in setup.
- The three known component failures are fixed.
- The component suite passes both with and without coverage.
- Coverage gates remain green in CI.

## Notes

- Keep the route-specific specs separate; do not merge them back into one monolithic file.
- Preserve the deterministic mocked route data already added for customer, AI, debt coverage, and capital gap tests.
- Prefer fixing launch and render timing issues over increasing timeouts alone.
