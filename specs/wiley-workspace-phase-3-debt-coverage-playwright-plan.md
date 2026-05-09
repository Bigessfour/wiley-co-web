# Wiley Workspace Phase 3 Debt Coverage Playwright Plan

## Application Overview

Phase-3-only browser plan for the new Debt Service Coverage Ratio and Bond Covenant Dashboard. The workspace panel is assumed to be named DebtCoveragePanel and routed through the Wiley workspace shell at /wiley-workspace/debt-coverage. Use stable shell selectors and panel ids, and keep browser proofs focused on route/deep-link integrity, first render, DSCR gauge state, waterfall rendering, and covenant-threshold changes. Component tests should prove the panel's render contracts and data/state transitions; browser tests should prove the live route, shell integration, visible ids, and user-facing state changes.

## Test Scenarios

### 1. Shell route proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. Debt coverage route is exposed from the workspace shell and deep-links cleanly

**File:** `tests/playwright/wiley-workspace-shell-navigation.spec.ts`

**Steps:**

1. Open /wiley-workspace from the seeded shell and inspect the workspace navigation.
   - expect: The workspace shell is visible and settled.
   - expect: #workspace-navigation-list includes a stable Debt Service Coverage Ratio or Debt Coverage link.
   - expect: The link href resolves to /wiley-workspace/debt-coverage.

2. Click the Debt Coverage link, then open /wiley-workspace/debt-coverage directly from the same browser session.
   - expect: The browser lands on /wiley-workspace/debt-coverage without a blank shell.
   - expect: #workspace-breadcrumb reflects Debt Coverage or Debt Service Coverage Ratio.
   - expect: #debt-coverage-panel is visible after both shell navigation and direct deep-linking.

3. Reload the deep-linked route once.
   - expect: The workspace shell remains visible after reload.
   - expect: #debt-coverage-panel and the breadcrumb remain present.
   - expect: No generic loading shell or error banner replaces the panel.

### 2. Panel render proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 2.1. DebtCoveragePanel renders the DSCR gauge and waterfall chart

**File:** `tests/playwright/wiley-workspace-debt-coverage.spec.ts`

**Steps:**

1. Open /wiley-workspace/debt-coverage from a clean session.
   - expect: #debt-coverage-panel is visible.
   - expect: The panel heading clearly identifies the Debt Service Coverage Ratio dashboard.
   - expect: The summary area shows the expected covenant and coverage context.

2. Verify the first-render chart surfaces inside the panel.
   - expect: #debt-coverage-dscr-gauge is visible.
   - expect: #debt-coverage-waterfall-chart is visible.
   - expect: Any covenant summary cards or threshold labels remain visible alongside the charts.

3. Check the panel in its initial state without changing any inputs.
   - expect: The gauge shows a readable DSCR value and status band.
   - expect: The waterfall chart stays mounted and does not collapse into a placeholder.
   - expect: The panel remains usable at the default desktop viewport.

### 3. Covenant threshold change proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 3.1. Changing the covenant threshold updates the gauge and covenant state

**File:** `tests/playwright/wiley-workspace-debt-coverage-threshold.spec.ts`

**Steps:**

1. Open /wiley-workspace/debt-coverage and locate the covenant-threshold control.
   - expect: #debt-coverage-threshold-input is visible.
   - expect: The current threshold value is readable before the edit.

2. Change the threshold to a stricter value and tab out or otherwise commit the edit.
   - expect: The covenant status or breach label updates to the new threshold band.
   - expect: #debt-coverage-dscr-gauge reflects the edited threshold state.
   - expect: The waterfall chart remains visible and continues to render against the updated covenant context.

3. Change the threshold again to prove the update is repeatable.
   - expect: The gauge and covenant status change again in the opposite direction or to the new target state.
   - expect: The panel keeps the workspace shell intact while the threshold is edited.
   - expect: No full-page reload is required for the update to appear.

### 4. Done criteria and verification split

**Seed:** `tests/playwright/seed.spec.ts`

#### 4.1. Phase 3 is Done only when component and browser proofs both hold

**File:** `tests/playwright/wiley-workspace-phase-3-acceptance.spec.ts`

**Steps:**

1. Confirm the component-test contract for DebtCoveragePanel before relying on browser coverage.
   - expect: Component tests should verify DebtCoveragePanel renders the DSCR gauge and waterfall chart with stable data binding.
   - expect: Component tests should verify covenant-threshold recalculation and status-band transitions against mocked state.
   - expect: Component tests should verify the panel's internal render contract without needing route wiring or shell navigation.

2. Confirm the browser-test contract for the live workspace route.
   - expect: Browser tests should verify /wiley-workspace/debt-coverage is reachable from the shell and by deep-link.
   - expect: Browser tests should verify #debt-coverage-panel, #debt-coverage-dscr-gauge, #debt-coverage-waterfall-chart, and #debt-coverage-threshold-input are present in the live page.
   - expect: Browser tests should verify covenant-threshold edits are visible to the user and keep the workspace shell stable.

3. Treat the phase as Done only after the live route, initial render, gauge proof, waterfall proof, and threshold-change proof all pass against the workspace shell conventions.
   - expect: There is no blank or fallback shell on the new route.
   - expect: The dashboard remains stable through refresh and threshold edits.
   - expect: The final browser proofs use stable ids rather than brittle text-only assertions.
