# Playwright UI Test Strategy

## Application Overview

This strategy uses the Playwright.dev guidance as the quality bar, while inventorying the current Wiley Playwright suite as the baseline. The repository already covers workspace smoke, shell navigation, panel-level proofs for break-even/rates/scenario/customer viewer/QuickBooks import/trends/data dashboard/decision support, plus Syncfusion render and visual-stability checks. The next layer of coverage should add docs-style discovery, search accuracy, deep-link integrity, cross-language consistency, and visual regressions for any documentation or content-heavy surface the app grows into. The plan also assumes Chromium, Firefox, and WebKit should be exercised in CI with traces recorded on first retry.

## Test Scenarios

### 1. Current Coverage Baseline

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. Fresh load and primary navigation stay intact

**File:** `tests/playwright/workspace-shell-production-ready.spec.ts`

**Steps:**

1. Open the workspace home page from a blank browser session.
   - expect: The overview shell loads without errors.
   - expect: The workspace summary, status card, and primary navigation are visible.

2. Use the primary navigation to open each core workspace route once, then return to the overview.
   - expect: Break-Even, Rates, Scenario Planner, Customer Viewer, Trends, Decision Support, Data Dashboard, and QuickBooks Import each render their panel headers.
   - expect: Breadcrumbs update when routes change.
   - expect: Returning to Overview restores the landing state.

3. Resize to a narrower desktop viewport and re-check the shell.
   - expect: The primary navigation remains visible.
   - expect: The overview remains usable at the smaller width.

#### 1.2. Core panel proofs stay data-accurate

**File:** `tests/playwright/break-even-production-ready.spec.ts`

**Steps:**

1. Open the Break-Even panel and change the cost and volume inputs.
   - expect: The panel recalculates the break-even figures.
   - expect: The KPI cards update to the edited values.
   - expect: The chart and gauge remain visible after the edit.

2. Open the Rates panel and change the editable rate.
   - expect: The current-rate KPI updates immediately.
   - expect: The comparison view remains readable and aligned.

3. Open the Scenario panel and add a cost item.
   - expect: Derived totals update from the new scenario item.
   - expect: The panel remains visually stable.

#### 1.3. Unique flows and Syncfusion surfaces remain stable

**File:** `tests/playwright/workspace-syncfusion-controls.spec.ts`

**Steps:**

1. Open the Customer Viewer and exercise the add/edit dialog flow.
   - expect: The editor dialog opens with accessible labels and validation messages.
   - expect: Canceling or closing the dialog returns the panel to its idle state.

2. Open the QuickBooks Import panel and verify the preview and commit surfaces.
   - expect: The upload affordance is visible.
   - expect: The preview grid and commit controls stay disabled until a file is ready.

3. Open the Decision Support, Data Dashboard, and Trends panels.
   - expect: Jarvis surfaces, charts, and gauges render without layout regressions.
   - expect: The visual-regression snapshots continue to match the current baseline.

#### 1.4. Workspace baseline save stays covered at the shell level

**File:** `tests/playwright/workspace-baseline-save.spec.ts`

**Steps:**

1. Open the Break-Even panel, edit the cost and volume inputs, and save through the workspace shell action.
   - expect: The Save baseline button is enabled from the workspace shell.
   - expect: The saved status and workspace reload message update after the click.
   - expect: The API payload reflects the edited baseline values.

### 2. Documentation Discovery and Navigation Expansion

**Seed:** `tests/playwright/workspace-shell-production-ready.spec.ts`

#### 2.1. Get Started journey reaches the first actionable instructions

**File:** `tests/playwright/seed.spec.ts`

**Steps:**

1. Open the home page from a blank session and activate the main call to action that starts the user journey.
   - expect: The first documentation or setup destination opens.
   - expect: The user lands on the installation or first-step content rather than a dead end.

2. Verify the destination page title, primary heading, and first code block or instruction panel.
   - expect: The page describes the setup path clearly.
   - expect: The first code or instruction block is visible and readable.

#### 2.2. Sidebar navigation and deep links remain intact

**File:** `tests/playwright/workspace-shell-production-ready.spec.ts`

**Steps:**

1. Open a deep-linked documentation page and inspect the sidebar and table of contents.
   - expect: The current section is highlighted.
   - expect: Nested navigation is visible and accessible.
   - expect: The page breadcrumb or section marker matches the deep link.

2. Click a nested section and then navigate back to the parent section.
   - expect: The content updates to the selected subsection.
   - expect: The anchor or route remains stable across the navigation change.

#### 2.3. Search returns the intended documentation result

**File:** `tests/playwright/seed.spec.ts`

**Steps:**

1. Open the documentation search UI and search for a core keyword such as locators or auto-waiting.
   - expect: Relevant results appear near the top.
   - expect: The selected result opens the correct page or section.

2. Repeat the search with a second core keyword and confirm the result set still matches the query.
   - expect: The search ranking remains accurate for the second keyword.
   - expect: No stale or unrelated results dominate the list.

### 3. Language and Visual Consistency

**Seed:** `tests/playwright/workspace-syncfusion-controls.spec.ts`

#### 3.1. Language switcher updates snippets consistently

**File:** `tests/playwright/seed.spec.ts`

**Steps:**

1. Switch the page between Node.js, Python, Java, and .NET examples.
   - expect: The displayed code snippet updates for each selected language.
   - expect: The snippet contents remain consistent with the surrounding explanation.

2. Return to the original language and verify the snippet and tab state reset cleanly.
   - expect: The original code block is restored.
   - expect: The language selection stays in sync with the visible snippet.

#### 3.2. Complex diagrams and theme variants do not regress visually

**File:** `tests/playwright/workspace-syncfusion-controls.spec.ts`

**Steps:**

1. Capture the page state that includes the most complex diagram or component graphic.
   - expect: The graphic renders without clipping, overlap, or missing labels.

2. Toggle between light and dark theme modes and compare the key layout regions.
   - expect: The page remains readable in both themes.
   - expect: Spacing, contrast, and iconography remain stable across the theme change.
