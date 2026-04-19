# Wiley Workspace Front End Production-Ready Proof Plan

## Application Overview

The current Playwright suite already proves the workspace shell, a handful of core panel interactions, and two visual baselines. This plan closes the remaining gaps with balanced panel coverage, explicit negative-path checks, and screenshot-backed visual proof so the front end can be treated as production-ready for correctness and UX.

## Test Scenarios

### 1. Workspace Shell

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. Workspace shell and navigation stay usable on a fresh load

**File:** `tests/playwright/workspace-shell-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace in a fresh browser session.
   - expect: The workspace shell should finish loading without pending startup text.
   - expect: The overview dashboard should be visible and the top-level shell status should show a ready state.

2. Use the left navigation to visit every panel route once, then return to the overview.
   - expect: Each route should open with the correct panel title and breadcrumb.
   - expect: The shell layout should remain readable and aligned while navigating.

3. Resize to a narrower desktop width and confirm the shell still fits the viewport.
   - expect: Primary navigation and shell status should remain usable.
   - expect: No key labels, cards, or buttons should overlap or clip.

### 2. Core Panel Proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 2.1. Break-even panel recalculates correctly and keeps a clean layout

**File:** `tests/playwright/break-even-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/break-even from a blank session.
   - expect: The break-even panel should render its KPI grid, inputs, and chart surface.
   - expect: The panel should not show a loading or pending state after startup.

2. Change total costs and projected volume through the numeric editors.
   - expect: The KPI cards should recalculate to the expected values.
   - expect: The recommended rate should update to the mathematically correct result.

3. Capture a desktop screenshot of the updated panel.
   - expect: The chart, KPI cards, and numeric editors should stay visually balanced.
   - expect: Labels and values should remain legible with no clipping or overlap.

#### 2.2. Rates panel updates the current rate and preserves comparison clarity

**File:** `tests/playwright/rates-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/rates from a blank session.
   - expect: The current-rate editor, comparison chart, and save status area should render.
   - expect: The panel should show the current persisted rate and not a pending state.

2. Change the current rate and tab out so the panel applies the edit.
   - expect: The current-rate KPI should reflect the new value.
   - expect: The comparison chart should remain visible and still match the selected context.

3. Trigger the save flow and confirm the snapshot status updates.
   - expect: The panel should report a successful save or a clear save failure message.
   - expect: The updated state should remain visually tidy and readable after the save action.

#### 2.3. Scenario planner adds rows and keeps the derived totals correct

**File:** `tests/playwright/scenario-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/scenario from a blank session.
   - expect: The scenario summary cards and editable grid should render.
   - expect: The route should show the current scenario context without pending text.

2. Add a new scenario row with a distinct name and numeric cost.
   - expect: The new row should appear in the grid.
   - expect: Scenario total cost and break-even values should update to the expected derived values.

3. Capture a screenshot of the updated scenario panel.
   - expect: The summary row, grid, and editor controls should remain aligned.
   - expect: The panel should still read as a polished KPI-and-grid workspace.

#### 2.4. Data dashboard proves the chart and gauge surfaces are production ready

**File:** `tests/playwright/data-dashboard-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/data-dashboard from a blank session.
   - expect: The dashboard should render its KPI cards, gauges, and chart regions.
   - expect: No chart region should appear empty, broken, or hidden.

2. Verify that the key dashboard visuals remain visible at desktop width and at a narrower width.
   - expect: The gauges and charts should remain readable at both widths.
   - expect: Card spacing and labels should continue to look intentional, not crowded.

3. Capture a screenshot of the dashboard at the primary desktop width.
   - expect: The dashboard should present as a coherent executive view.
   - expect: Chart labels and value labels should remain crisp and unclipped.

#### 2.5. Trends panel renders and reflows cleanly

**File:** `tests/playwright/trends-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/trends from a blank session.
   - expect: The trends panel and projection chart should render.
   - expect: The panel should not show any loading placeholder or broken chart state.

2. Resize the viewport once and re-check the chart region.
   - expect: The trend chart should remain visible and appropriately sized.
   - expect: The panel heading and chart labels should stay legible after the resize.

3. Capture a screenshot of the trends panel.
   - expect: The chart should feel intentionally framed and balanced.
   - expect: The panel should not show clipped axes, legends, or titles.

### 3. Unique Interaction Proof

**Seed:** `tests/playwright/seed.spec.ts`

#### 3.1. Customer viewer handles search, filter reset, and degraded directory states

**File:** `tests/playwright/customer-viewer-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/customers from a blank session.
   - expect: The customer directory grid and status area should render.
   - expect: The panel should resolve away from any loading state.

2. Enter a search term, then clear filters and refresh the directory.
   - expect: The search box should accept text and clear back to empty.
   - expect: The refresh action should leave the directory in a usable ready or degraded state, not a stuck loading state.

3. If the live directory is unavailable, verify the error banner messaging.
   - expect: The error banner should explain that the live utility-customer directory could not be loaded or refreshed.
   - expect: The panel should still remain interactive enough for recovery actions.

#### 3.2. Customer viewer editor proof covers add and edit dialog behavior

**File:** `tests/playwright/customer-viewer-editor.spec.ts`

**Steps:**

1. Open /wiley-workspace/customers and launch the add-customer dialog.
   - expect: The dialog should open with all expected input controls visible.
   - expect: The form should expose clear labels and a calm, readable layout.

2. Populate the dialog, save once, then reopen the record for editing.
   - expect: The save flow should complete or show a precise validation message.
   - expect: The edit dialog should preserve field values and remain keyboard accessible.

3. Delete or cancel the record flow as applicable, then close the dialog.
   - expect: The close behavior should be predictable and not trap focus.
   - expect: The dialog should dismiss cleanly without leaving broken overlay state.

#### 3.3. QuickBooks import rejects unsupported uploads and resets cleanly

**File:** `tests/playwright/quickbooks-import-negative.spec.ts`

**Steps:**

1. Open /wiley-workspace/quickbooks-import from a blank session.
   - expect: The file picker, idle status text, and disabled actions should render.
   - expect: The panel should clearly show that no file has been selected yet.

2. Upload a non-CSV/non-Excel file through the browser file input and run Analyze.
   - expect: The panel should switch to an unsupported-file status.
   - expect: Commit should remain blocked and the assistant context should still indicate no preview has been loaded.

3. Press Reset and confirm the panel returns to its idle state.
   - expect: The status headline should return to Ready.
   - expect: Analyze and Commit should be disabled again until a valid file is selected.

#### 3.4. QuickBooks import supports preview, duplicate checks, and commit confirmation

**File:** `tests/playwright/quickbooks-import-preview.spec.ts`

**Steps:**

1. Upload a valid QuickBooks CSV or Excel export and run Analyze.
   - expect: The preview grid should load with parsed rows.
   - expect: The assistant context should summarize the selected file and row count.

2. If the file is not a duplicate, open the commit confirmation dialog and cancel once.
   - expect: The commit dialog should appear with a clear confirmation message.
   - expect: Cancel should dismiss the dialog without changing the loaded preview.

3. If the file is marked as a duplicate, confirm the panel blocks commit.
   - expect: The commit action should stay disabled or be rejected with a clear duplicate message.
   - expect: The duplicate state should be obvious in the status area.

#### 3.5. Decision support proves both assistant-enabled and fallback states

**File:** `tests/playwright/decision-support-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/decision-support from a blank session.
   - expect: The decision support route should render with a clear title and breadcrumb.
   - expect: Either the Jarvis chat surface or the fallback Semantic Kernel guidance should be visible.

2. If the assistant surface is present, ask one short question and then reset the thread.
   - expect: The question input, conversation history, and recommendation history should respond as expected.
   - expect: The reset action should clear the interaction state cleanly.

3. If only fallback guidance is available, verify the fallback copy and actions remain understandable.
   - expect: The panel should explain why the interactive assistant is unavailable.
   - expect: The fallback state should still read as a deliberate, polished UX rather than a failure page.
