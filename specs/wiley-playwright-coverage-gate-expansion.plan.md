# Wiley Playwright Coverage Gate Expansion

## Application Overview

Playwright expansion plan for the Wiley.co workspace shell and high-value panel routes. The goal is to add browser-proof coverage where the current suite is weakest: shell persistence, QuickBooks import, scenario/rate edits, customer viewer workflows, affordability, decision support, trends, data dashboard, debt coverage, and capital gap. The plan prioritizes stable, auditable selectors already used by the suite so the new tests improve confidence without depending on speculative ids or duplicate flows.

## Test Scenarios

### 1. Shell and Route Persistence

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. Workspace shell loads the main chrome and keeps route state stable across navigation

**File:** `tests/playwright/workspace-shell-production-ready.spec.ts`

**Steps:**

1. Open the workspace from a blank session and wait for the shell to settle.
   - expect: The page shows `#workspace-dashboard`, `#workspace-navigation-card`, `#workspace-status-card`, and `#workspace-breadcrumb`.
   - expect: The primary navigation and document center are visible without console or render errors.

2. Navigate through the main routes once: Break-Even, Rates, Scenario Planner, Customer Viewer, Trends, Decision Support, Data Dashboard, and QuickBooks Import.
   - expect: Each route renders its panel id or panel header: `#rates-panel`, `#scenario-panel`, `#customer-viewer-panel`, `#trends-panel`, `#decision-support-panel`, `#data-dashboard-panel`, and `#quickbooks-import-panel` or the route-specific equivalent.
   - expect: The breadcrumb updates to match the active route after each navigation.

3. Return to Overview, collapse the left navigation rail, reload the page, and verify the persisted state is restored.
   - expect: The collapsed navigation state survives the reload.
   - expect: The overview shell still loads and the workspace status card remains populated.

#### 1.2. Left-nav persistence and route highlighting survive a reload on a deep link

**File:** `tests/playwright/wiley-workspace-shell-navigation.spec.ts`

**Steps:**

1. Seed the left navigation as collapsed, open a deep link such as Break-Even or Customer Viewer, and wait for the workspace shell.
   - expect: The shell opens on the requested route instead of resetting to the default overview.
   - expect: The active link in `#workspace-navigation-card` matches the route that was opened.

2. Reload the page without changing route and inspect the shell state again.
   - expect: The left navigation remains collapsed after reload.
   - expect: The breadcrumb and active panel still reflect the same route.

3. Switch to another route and then back again.
   - expect: The route switch updates the panel without losing shell chrome.
   - expect: The active navigation state follows the current route and does not stick to the old panel.

### 2. Transactional Workflow Coverage

**Seed:** `tests/playwright/seed.spec.ts`

#### 2.1. QuickBooks import accepts a valid export, previews it, and gates commit correctly

**File:** `tests/playwright/quickbooks-import-preview.spec.ts`

**Steps:**

1. Open QuickBooks Import and upload a valid CSV export, then click Analyze file.
   - expect: The status changes from Ready/File selected into a preview-ready state.
   - expect: The preview grid renders the imported rows and the assistant context summary reflects the selected file.

2. Inspect the commit button and the confirmation dialog behavior.
   - expect: Commit remains blocked until the preview is ready or the file is flagged as safe to commit.
   - expect: If commit becomes available, the confirmation dialog opens and cancels cleanly without losing the loaded preview.

3. Verify the panel summary and duplicate-check messaging after analysis.
   - expect: The status message explains the preview result or duplicate result clearly.
   - expect: The panel stays usable after analysis and does not leave the commit flow in an indeterminate state.

#### 2.2. QuickBooks import rejects unsupported uploads and resets back to idle

**File:** `tests/playwright/quickbooks-import-negative.spec.ts`

**Steps:**

1. Open QuickBooks Import from a blank session and confirm the idle state.
   - expect: The panel shows Ready, the analyze button is disabled, and the commit button is disabled.
   - expect: The assistant context says no preview has been loaded yet.

2. Upload a non-CSV/non-Excel file and click Analyze file.
   - expect: The panel reports an unsupported file type.
   - expect: Commit remains disabled and no preview grid is created.

3. Click Reset panel and confirm the workflow returns to the starting state.
   - expect: The status headline returns to Ready.
   - expect: The buttons and assistant summary return to their initial idle behavior.

#### 2.3. Scenario planner updates derived totals when items are added or edited

**File:** `tests/playwright/scenario-production-ready.spec.ts`

**Steps:**

1. Open Scenario Planner and confirm the grid and edit status are visible.
   - expect: The scenario grid renders with the active scenario rows.
   - expect: The edit status indicates the panel is ready.

2. Add a scenario item and then edit its cost or name.
   - expect: The grid reflects the new or updated scenario item.
   - expect: The derived scenario totals and rate pressure values update after the edit.

3. Save or apply the scenario and verify the status message.
   - expect: The panel reports a successful save/apply state or a clear failure message if the API is unavailable.
   - expect: The scenario state remains visible after the action.

#### 2.4. Rates panel keeps the comparison view readable after current-rate edits

**File:** `tests/playwright/rates-production-ready.spec.ts`

**Steps:**

1. Open Rates and wait for the KPI grid, current rate input, and comparison chart.
   - expect: `#rates-panel`, `#rates-kpi-grid`, `#current-rate-input`, and `#rates-comparison-chart` are visible.
   - expect: The snapshot status shows the initial ready state.

2. Change the current rate and tab out so the panel applies the edit.
   - expect: The current-rate KPI updates to the new value.
   - expect: The comparison chart remains visible and the recommended rate remains formatted correctly.

3. Trigger the save snapshot flow.
   - expect: The snapshot status updates to a saved or failed-save message that matches the API response.
   - expect: The panel remains stable after the save action.

#### 2.5. Customer viewer supports search, filter reset, and editor validation

**File:** `tests/playwright/customer-viewer-production-ready.spec.ts`

**Steps:**

1. Open Customer Viewer and verify the directory, status, and action buttons.
   - expect: `#customer-viewer-panel`, `#customer-directory-grid`, and `#customer-directory-status` are visible.
   - expect: Refresh, Clear filters, and Add customer are available unless the panel is busy.

2. Enter a search term, clear it, and refresh the directory.
   - expect: The search box accepts the query and the directory updates accordingly.
   - expect: Clearing filters restores the default customer view and the status text updates without stale loading text.

3. Exercise the error or empty-directory state.
   - expect: If the API is unavailable or returns no rows, the panel shows the expected warning or empty-state messaging.
   - expect: The panel does not crash or trap the user in a disabled state.

#### 2.6. Customer editor dialog validates required fields and can save or cancel cleanly

**File:** `tests/playwright/customer-viewer-editor.spec.ts`

**Steps:**

1. Open the Add customer or Edit customer dialog from the directory.
   - expect: The editor dialog opens with accessible labels and the main form controls are visible.
   - expect: The dialog is scoped to the customer panel and not the entire page.

2. Submit an invalid or incomplete form and inspect validation feedback.
   - expect: Required-field validation messages appear next to the relevant controls.
   - expect: The save action remains blocked until the form is valid.

3. Cancel the dialog or save a valid record and confirm the panel returns to idle.
   - expect: Cancel closes the dialog without leaving partial edits behind.
   - expect: A successful save refreshes the directory and updates the row set.

### 3. Analytics and AI Coverage

**Seed:** `tests/playwright/seed.spec.ts`

#### 3.1. Affordability dashboard renders the summary, gauges, and heatmap with seeded data

**File:** `tests/playwright/wiley-workspace-affordability.spec.ts`

**Steps:**

1. Open the Affordability route from a blank session.
   - expect: `#affordability-dashboard-panel`, `#affordability-gauge`, and `#affordability-class-impact-chart` are visible.
   - expect: The panel summary includes the affordability status and bill-burden language.

2. Change the Monthly MHI input to a new numeric value.
   - expect: The MHI input accepts the edited value.
   - expect: The affordability KPI values and status text update to match the new input.

3. Confirm the heatmap and the empty-data fallback remain stable.
   - expect: The heatmap or supporting affordability visual remains visible after the edit.
   - expect: If the dataset is empty, the panel still shows a graceful fallback instead of failing to render.

#### 3.2. Decision support renders one assistant surface on the direct route and falls back cleanly when AI is unavailable

**File:** `tests/playwright/decision-support-production-ready.spec.ts`

**Steps:**

1. Open the Decision Support route directly from a blank session.
   - expect: `#decision-support-panel` is visible and the workspace breadcrumb reads Decision Support.
   - expect: Only the intended assistant surface is rendered for the route and the shell does not mount a duplicate visible Jarvis rail on top of it.

2. Ask a short question when the AI endpoint is available.
   - expect: The assistant answer updates in the panel.
   - expect: The runtime status and recommendation history reflect the conversation.

3. Repeat the same route with AI unavailable or the fallback path active.
   - expect: The panel shows its fallback messaging rather than hanging or crashing.
   - expect: The route stays usable and the assistant chrome still renders predictably.

#### 3.3. Trends panel renders the chart when data exists and shows an empty state when it does not

**File:** `tests/playwright/trends-production-ready.spec.ts`

**Steps:**

1. Open Trends with projection rows present.
   - expect: `#trends-panel` is visible.
   - expect: The projection chart or chart container is visible and the breadcrumb shows Trends.

2. Open Trends with no projection data available.
   - expect: The panel shows the no-data empty state instead of a blank shell.
   - expect: The empty state explains that projection rows are missing and the route remains stable.

3. Return to the populated state and confirm the panel still renders without stale artifacts.
   - expect: The chart container or panel body returns to the normal visual state.
   - expect: The route transition does not leave duplicate charts or broken markup behind.

#### 3.4. Data dashboard renders every section with data and degrades gracefully when optional series are missing

**File:** `tests/playwright/data-dashboard-production-ready.spec.ts`

**Steps:**

1. Open Data Dashboard with the seeded customer, scenario, projection, and rate comparison data.
   - expect: `#data-dashboard-panel` is visible.
   - expect: The expected dashboard sections render, including the rate comparison, waterfall, trend, and customer mix areas.

2. Open the panel again with optional data removed or minimized.
   - expect: The core KPIs remain visible.
   - expect: Sections that depend on optional series hide or simplify instead of failing.

3. Verify the panel still shows the selected enterprise and fiscal year context.
   - expect: The workspace breadcrumb and dashboard context remain aligned with the active selection.
   - expect: No stale section remains from the richer-data render.

### 4. Risk and Recovery Coverage

**Seed:** `tests/playwright/seed.spec.ts`

#### 4.1. Debt coverage renders threshold editing and stays usable with fallback data

**File:** `tests/playwright/wiley-workspace-debt-coverage.spec.ts`

**Steps:**

1. Open Debt Coverage from a blank session.
   - expect: `#debt-coverage-panel` is visible.
   - expect: The gauge, waterfall, and threshold editing controls are present.

2. Change the threshold input and confirm the dashboard updates.
   - expect: The displayed status or coverage indicator reacts to the new threshold.
   - expect: The chart area remains visible after the edit.

3. Repeat the route with API data unavailable.
   - expect: The panel shows a stable fallback state rather than crashing.
   - expect: The threshold controls and summary chrome remain usable.

#### 4.2. Capital gap renders the summary and breakdown, then handles an empty or failing API response

**File:** `tests/playwright/capital-gap-production-ready.spec.ts`

**Steps:**

1. Open Capital Gap from a blank session.
   - expect: `#capital-gap-panel` is visible.
   - expect: The chart summary and breakdown sections are visible with the seeded enterprise data.

2. Inspect the chart container and the revenue-gap figures.
   - expect: The chart container remains attached and readable.
   - expect: The dollar values in the summary match the selected enterprise.

3. Repeat the panel load with no capital-gap data or a failing API response.
   - expect: The panel shows a controlled fallback state instead of a hanging chart.
   - expect: The route remains navigable and the workspace shell stays healthy.
