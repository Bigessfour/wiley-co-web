# Wiley Workspace New Panels Playwright Plan

## Application Overview

Deterministic Playwright coverage for the new Wiley Workspace enterprise surfaces, focusing on the Break-Even quadrants, the apartment configuration surface, and stable shell navigation backed by the live workspace API.

## Test Scenarios

### 1. Workspace Shell And Navigation

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. Shell navigation reaches each new enterprise panel route

**File:** `tests/playwright/wiley-workspace-shell-navigation.spec.ts`

**Steps:**

1. Start from the seeded Wiley Workspace shell and verify the left navigation and workspace navigation links are visible.
   - expect: The shell renders the workspace header and navigation rail.
   - expect: The new routes for Break-Even, QuickBooks Import, Scenario Planner, Customer Viewer, Trends, Decision Support, and Data Dashboard are present as stable links.

2. Click the Break-Even navigation link, then return to the workspace overview and click the QuickBooks Import link.
   - expect: The Break-Even panel loads without a blank state.
   - expect: The QuickBooks Import panel loads and shows its enterprise context, upload, and routing sections.

3. Switch to the Scenario Planner, Customer Viewer, Trends, Decision Support, and Data Dashboard links in sequence.
   - expect: Each route resolves to its intended workspace panel.
   - expect: No route falls back to a generic loading shell or error banner.

4. Confirm the active route updates without a full page reload or loss of the workspace shell.
   - expect: The workspace shell stays intact across navigation.
   - expect: The active panel changes while the header and rail remain visible.

### 2. Break-Even Quadrants

**Seed:** `tests/playwright/seed.spec.ts`

#### 2.1. Break-Even renders four enterprise quadrants with stable KPI cards

**File:** `tests/playwright/wiley-workspace-break-even-quadrants.spec.ts`

**Steps:**

1. Navigate directly to the Break-Even route from the seeded shell.
   - expect: The Break-Even panel title is visible.
   - expect: The panel renders four enterprise quadrants in a 2x2 layout for Water, Sewer, Trash, and Apartments.

2. Assert each quadrant exposes the enterprise label, a chart surface, and a KPI summary card.
   - expect: Water, Sewer, Trash, and Apartments each have their own visible chart region.
   - expect: Each quadrant shows KPI text for current rate, monthly balance, and break-even rate.

3. Verify the chart series semantics by checking the rendered legend or series labels where exposed.
   - expect: Revenue is represented as the positive line, expenses as the red dynamic line, and break-even as the dashed reference line.
   - expect: The chart surface remains visible after initial render and does not collapse into a placeholder.

4. Change the global TotalCosts and ProjectedVolume inputs and observe the quadrants update.
   - expect: All four quadrants refresh from the shared global inputs.
   - expect: The break-even rates and KPI summaries change in response to the input update.

### 3. Apartment Configuration

**Seed:** `tests/playwright/seed.spec.ts`

#### 3.1. Apartment editor exposes editable unit types and live roll-up totals

**File:** `tests/playwright/wiley-workspace-apartment-config.spec.ts`

**Steps:**

1. Open the Apartments configuration surface from the Break-Even area or the separate apartment panel entry point used by the workspace shell.
   - expect: The apartment editor is visible as a separate panel surface.
   - expect: The unit-type grid shows rows for the default apartment breakdown.

2. Verify the unit-type grid exposes Name, UnitCount, and MonthlyRent columns with toolbar actions for add, delete, and update.
   - expect: The grid shows stable column headers for the editable apartment fields.
   - expect: Add, delete, and update toolbar actions are present and enabled when the grid is ready.

3. Edit an existing unit row and add a new unit row, then save or commit the change through the configured workflow.
   - expect: The live totals update after the edit.
   - expect: The total units, total monthly revenue, and effective $/customer are recalculated immediately.

4. Confirm the apartment configuration persists through the configured callback or reload path.
   - expect: The edited apartment unit types remain present after a workspace refresh.
   - expect: The apartment totals match the bedroom-weighted normalization rule.

### 4. QuickBooks Import Regression

**Seed:** `tests/playwright/seed.spec.ts`

#### 4.1. QuickBooks import keeps route context aligned with all enterprise options

**File:** `tests/playwright/wiley-workspace-quickbooks-import-regression.spec.ts`

**Steps:**

1. Open the QuickBooks Import route from the seeded shell.
   - expect: The import panel loads with enterprise and fiscal year selectors, upload controls, preview, and routing tabs.

2. Verify the enterprise selector can represent the full enterprise set without falling back to a Water-only default.
   - expect: The control exposes the configured enterprise options from the workspace snapshot.
   - expect: Trash and Apartments are not missing from the workspace-level enterprise set.

3. Switch routing tabs and confirm the panel still renders preview and routing sections without errors.
   - expect: The routing and history tabs remain interactive.
   - expect: The import panel continues to render its assistant and status sections after tab switches.
