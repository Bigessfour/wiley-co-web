# Wiley Workspace Phase 6 Capital Gap Proof Plan

## Application Overview

Phase-6 final browser proof for CapitalGapPanel. The proof should stay inside the Capital Needs vs. Rate Revenue Gap Analysis surface, use the existing Wiley workspace shell selectors, and verify the panel heading, KPI cards or summary strip, the SfChart with stacked columns plus a cumulative line, and the rendered totals driven by CapitalGapService-tagged budget items. Keep the scope tight: no unrelated exports, dialogs, or extra navigation flows.

## Test Scenarios

### 1. Capital Gap Phase 6

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. Capital gap panel renders the live gap-analysis surface and reflects tagged budget totals

**File:** `tests/playwright/capital-gap-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/capital-gap from a blank browser session and wait for the workspace shell to settle.
   - expect: #workspace-dashboard is visible.
   - expect: #workspace-navigation-list and #workspace-status-card are visible.
   - expect: The breadcrumb and panel context identify the Capital Needs vs. Rate Revenue Gap Analysis surface.

2. Verify the CapitalGap panel heading and summary strip or KPI cards render with live data.
   - expect: #capital-gap-panel is visible.
   - expect: The heading clearly identifies Capital Needs vs. Rate Revenue Gap Analysis.
   - expect: The summary strip or KPI cards show concrete totals instead of a placeholder or loading state.

3. Verify the chart surface is mounted as the intended stacked-column plus cumulative-line visualization.
   - expect: #capital-gap-chart is visible.
   - expect: The chart description or visible labels indicate stacked columns for the component totals and a cumulative gap line.
   - expect: The legend or series labels remain readable and distinct.

4. Cross-check that CapitalGapService-tagged budget items are reflected in the rendered totals.
   - expect: At least one visible breakdown row or subtotal names the CapitalGapService-tagged items.
   - expect: The rendered total matches the aggregate of the tagged items shown in the panel.
   - expect: The KPI or summary totals change in the same direction as the tagged-item subtotal, not as a static placeholder.

5. Re-check the same route at a narrower desktop viewport.
   - expect: The KPI strip and chart remain visible at the smaller desktop width.
   - expect: No clipping, overlap, or collapsed placeholder replaces the panel content.
   - expect: The shell remains usable with the route still anchored on the capital-gap panel.

6. Reload the route once as a resilience check.
   - expect: The workspace shell returns without a blank screen or fallback error banner.
   - expect: The panel heading, KPI strip, and chart reappear after reload.
   - expect: The live totals remain consistent after refresh.
