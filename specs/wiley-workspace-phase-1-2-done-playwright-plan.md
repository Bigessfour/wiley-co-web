# Wiley Workspace Phase 1 and Phase 2 Playwright Done Plan

## Application Overview

Focused Playwright coverage for the two delivery phases that matter now: phase 1 reserve trajectory and phase 2 affordability with manual MHI. The plan prioritizes stable route links, stable ids, visible UI state changes, and regression-proof route refreshes over speculative selectors or duplicate coverage.

## Test Scenarios

### 1. Workspace shell route proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. Expose and deep-link the two phase routes from the shell

**File:** `tests/playwright/wiley-workspace-shell-navigation.spec.ts`

**Steps:**

1. Open /wiley-workspace from a fresh browser session and wait for the shell to settle.
   - expect: The workspace dashboard, status card, and navigation rail are visible.
   - expect: The shell shows stable route links for Affordability and Reserve Trajectory.

2. Assert the shell navigation links for Affordability and Reserve Trajectory use the exact workspace hrefs.
   - expect: Affordability points to /wiley-workspace/affordability.
   - expect: Reserve Trajectory points to /wiley-workspace/reserve-trajectory.

3. Click Affordability, then click Reserve Trajectory from the same shell.
   - expect: The affordability panel loads first and the reserve trajectory panel loads second without a full reload.
   - expect: The breadcrumb or panel heading updates to match the selected route each time.

4. Return to the workspace overview and confirm the shell is still intact after both route changes.
   - expect: The overview dashboard is visible again.
   - expect: No loading placeholder or blank shell remains.

### 2. Phase 1 reserve trajectory proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 2.1. Reserve trajectory route renders the forecast slice and chart correctly

**File:** `tests/playwright/wiley-workspace-reserve-trajectory.spec.ts`

**Steps:**

1. Open /wiley-workspace/reserve-trajectory from a clean session.
   - expect: #reserve-trajectory-panel is visible.
   - expect: The panel title reads Reserve Trajectory.
   - expect: The shell breadcrumb or route title identifies the reserve trajectory surface.

2. Verify the core reserve trajectory UI render surfaces are present.
   - expect: Current Reserves, Current Reserve Months, and 5-Year Solvency Score are visible.
   - expect: #reserve-trajectory-chart is visible.
   - expect: The Stress band and Policy minimum copy is visible in the forecast section.
   - expect: The Export PDF and Export Excel buttons are visible via stable ids.

3. Reload the route once to prove the panel survives a direct route refresh.
   - expect: The same panel ids are still present after reload.
   - expect: The route remains /wiley-workspace/reserve-trajectory and does not fall back to the overview.

4. If the page is rendered at a narrower desktop width, re-check the chart and export controls.
   - expect: The chart still renders within the panel.
   - expect: The export controls remain visible and do not overlap the chart or KPI cards.

### 3. Phase 2 affordability with manual MHI proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 3.1. Affordability route proves the manual MHI workflow and derived visuals

**File:** `tests/playwright/wiley-workspace-affordability.spec.ts`

**Steps:**

1. Open /wiley-workspace/affordability from a clean session.
   - expect: #affordability-dashboard-panel is visible.
   - expect: The hero heading reads Monthly bill burden versus MHI.
   - expect: #affordability-monthly-mhi-input, #affordability-gauge, #affordability-class-impact-chart, and #affordability-heatmap-panel are visible.

2. Change Monthly MHI to a lower value that crosses the risk threshold, such as 3000 or 2000, then tab out.
   - expect: The input keeps currency formatting.
   - expect: #affordability-kpi-mhi-share updates to a new percentage.
   - expect: The Affordability Status card changes from the default band to a lower-income risk band such as Monitor or Elevated.
   - expect: The gauge and heatmap remain visible after the update.

3. Assert at least one class/scenario heatmap cell and one chart region still render after the MHI edit.
   - expect: The class impact chart remains visible.
   - expect: The heatmap shows multiple class rows and rate scenarios rather than collapsing into a blank state.
   - expect: The UI keeps the manual-input state without breaking the route.

4. Refresh the route once with the edited MHI state still in place if the app preserves it, or re-enter the same value after refresh if not.
   - expect: The panel remains usable after a reload.
   - expect: No stale loading state or broken chart placeholder appears.

### 4. Residual gaps and Done criteria

**Seed:** `tests/playwright/seed.spec.ts`

#### 4.1. Document the remaining browser gaps that are not yet covered

**File:** `tests/playwright/wiley-workspace-phase-1-2-gap-notes.spec.ts`

**Steps:**

1. -

   - expect: Reserve trajectory does not yet have browser coverage for the no-data fallback or for clicking the export buttons end to end.
   - expect: Affordability does not yet prove multiple MHI thresholds or exact heatmap cell semantics beyond one manual input change.
   - expect: Neither phase currently has a dedicated browser proof for a shell-level overview tile launch if that becomes a required regression surface.

#### 4.2. Define the Done acceptance criteria for the two phase milestone

**File:** `tests/playwright/wiley-workspace-phase-1-2-acceptance.spec.ts`

**Steps:**

1. -

   - expect: The shell exposes exact links and direct routes for /wiley-workspace/reserve-trajectory and /wiley-workspace/affordability.
   - expect: Reserve trajectory renders #reserve-trajectory-panel, #reserve-trajectory-chart, the three KPI cards, and the export controls without a blank or loading state.
   - expect: Affordability renders #affordability-dashboard-panel, the manual MHI input, the gauge, the class-impact chart, and the heatmap, and a lower MHI changes the KPI and status band.
   - expect: Both routes survive a refresh and keep their visible state without regressing the workspace shell.
