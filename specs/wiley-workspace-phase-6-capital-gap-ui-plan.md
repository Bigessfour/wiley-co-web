# Wiley Workspace Phase 6 Capital Gap UI Plan

## Application Overview

Phase-6 final UI slice for `CapitalGapPanel`. Keep the scope narrow: Capital Needs vs. Rate Revenue Gap Analysis only. The panel should render a production-ready capital gap story with a stacked-column plus cumulative-line `SfChart`, and it should consume existing budget items tagged as `Capital` by `CapitalGapService` rather than introducing a new budget source.

## UI Structure

### 1. Panel shell

Use the same Wiley workspace panel pattern as the other production-ready surfaces:

1. A strong heading that names the analysis clearly.
2. A short supporting sentence that explains the capital-needs versus rate-revenue comparison.
3. A compact KPI strip or summary cards above the chart.
4. A single main chart card that holds the stacked columns and cumulative line.
5. A small breakdown section below the chart for tagged capital items and subtotals.

### 2. Chart shape

Model the chart on the existing `SfChart` patterns already used in the workspace:

1. Use category or time-based x-axis labels depending on the budget-item aggregation chosen by the service.
2. Use a currency-formatted y-axis.
3. Render stacked columns for the capital-needs or capital-bucket components.
4. Overlay a cumulative line that shows the running revenue-gap position.
5. Keep the legend visible and the tooltip shared so the chart can explain each step cleanly.

### 3. Service-to-UI mapping

The UI should read from the service output in a way that is easy to prove in tests:

1. `CapitalGapService` tags the relevant budget items as `Capital`.
2. The panel summarizes those tagged items in the KPI strip.
3. The chart series should derive directly from the same tagged aggregation.
4. Any visible totals should reconcile with the tagged-item subtotal rather than a synthetic placeholder.

### 4. Accessibility and responsiveness

Use an explicit `AccessibilityDescription` on the chart and keep the panel responsive enough to survive a narrower desktop viewport without overlap or clipping. The narrow-width proof should still keep the heading, KPI strip, and chart readable.

## Acceptance Shape

The panel is ready for the final browser proof when it can show:

1. The `CapitalGapPanel` heading and explanatory copy.
2. A live KPI strip or summary cards.
3. An `SfChart` with stacked columns and a cumulative line.
4. A breakdown of the capital-tagged totals returned by the service.
5. Stable rendering after a reload and at a narrower desktop width.
