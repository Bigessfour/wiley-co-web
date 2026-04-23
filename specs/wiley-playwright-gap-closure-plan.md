# Wiley Playwright Gap Closure Plan

## Application Overview

Playwright-only plan for closing the browser coverage gaps identified in the 2026-04-22 gap analysis. The plan focuses on replacing skip-based paths with real assertions, stabilizing the live QuickBooks routing flow, and keeping the workspace shell, decision support, customer viewer, and dashboard proofs valid across Chromium and WebKit.

## Test Scenarios

### 1. Strict Pass-Rate Repair

**Seed:** `tests/playwright/seed.spec.ts`

#### 1.1. QuickBooks routing real flow commits against the live API

**File:** `tests/playwright/quickbooks-routing-real.spec.ts`

**Steps:**

1. Open /wiley-workspace/quickbooks-import from a clean browser session with the local API reachable.
   - expect: The workspace shell loads without a browser-close interruption.
   - expect: The QuickBooks Import panel is visible and usable.

2. Seed one routing rule through the live API and verify the rule is reflected in the editor UI.
   - expect: The saved rule name is visible in the routing card.
   - expect: The target enterprise and priority match the seeded configuration.

3. Upload a representative QuickBooks export and analyze it.
   - expect: The preview grid renders routed rows.
   - expect: The status moves to a preview-ready or duplicate-detected state without network refusal errors.

4. Commit the import and refresh history.
   - expect: The commit confirmation appears.
   - expect: The history lists the uploaded file and the routed scope summary.
   - expect: No ECONNREFUSED or skipped-browser workaround is needed.

#### 1.2. Workspace baseline save and reload preserve the active state

**File:** `tests/playwright/workspace-baseline-save.spec.ts`

**Steps:**

1. Open /wiley-workspace and confirm the shell loads normally or from documented fallback state.
   - expect: The dashboard, status card, and primary navigation are visible.
   - expect: Fallback messaging, if present, explains that the API is unavailable rather than timing out.

2. Change a baseline-driving value on Break-Even or Rates and save it through the workspace shell.
   - expect: The save action becomes enabled.
   - expect: The saved-state banner or status reflects the edited values.

3. Reload the route and revisit the same panel.
   - expect: The edited baseline is still present after reload.
   - expect: Breadcrumbs and panel state remain synchronized.

4. Verify the shell continues to operate when the API is in fallback mode.
   - expect: The user can keep working locally.
   - expect: The workspace does not fail with a skip or browser-close path.

### 2. Browser Parity Without Skips

**Seed:** `tests/playwright/workspace-syncfusion-controls.spec.ts`

#### 2.1. Data dashboard and Jarvis visual proofs run on WebKit without skipping

**File:** `tests/playwright/workspace-syncfusion-controls.spec.ts`

**Steps:**

1. Open the data dashboard in a non-Chromium browser.
   - expect: The dashboard panel is visible.
   - expect: KPI cards, gauges, and charts render instead of being skipped.

2. Open Decision Support and inspect the Jarvis surface or the documented fallback.
   - expect: If secure Jarvis is disabled, the fallback copy is asserted explicitly.
   - expect: If Jarvis is enabled, the chat surface and runtime status are visible.

3. Keep the visual proof Chromium-only only for the screenshot assertion itself, not for the browser run.
   - expect: The test does not rely on a project skip to avoid WebKit.
   - expect: The non-Chromium execution still contributes to the strict pass-rate gate.

#### 2.2. Decision support always proves either Jarvis or the documented fallback

**File:** `tests/playwright/decision-support-production-ready.spec.ts`

**Steps:**

1. Open /wiley-workspace/decision-support from a clean session.
   - expect: The decision-support panel is visible.
   - expect: The breadcrumb or panel heading identifies the Decision Support surface.

2. If the secure assistant is available, assert the chat UI controls and runtime status.
   - expect: The question input is visible.
   - expect: Conversation and recommendation history are visible.
   - expect: Ask and reset controls are enabled.

3. If the secure assistant is not available, assert the documented fallback text instead.
   - expect: The panel shows the server-side Semantic Kernel assistant copy.
   - expect: The test still passes without any skip or fixme path.

#### 2.3. Customer viewer search and edit flow remains stable after reload

**File:** `tests/playwright/customer-viewer-production-ready.spec.ts`

**Steps:**

1. Open the Customer Viewer panel and verify the search, filter, and grid surfaces render.
   - expect: The summary, filters, and directory panels are visible.
   - expect: The customer grid loads instead of timing out or skipping.

2. Search or filter the customer list and open the editor dialog.
   - expect: The dialog opens with accessible inputs.
   - expect: Validation and cancel paths are reachable.

3. Save or cancel the dialog, then reload the route.
   - expect: The panel returns to a stable idle state.
   - expect: The expected search or filter context remains consistent after reload.

4. Run the same proof in WebKit without a browser-based skip.
   - expect: The browser run still exercises the customer viewer.
   - expect: The pass rate is not protected by a project skip.

### 3. Navigation and Recovery

**Seed:** `tests/playwright/workspace-shell-production-ready.spec.ts`

#### 3.1. Workspace shell deep links stay valid across reload and sidebar collapse

**File:** `tests/playwright/workspace-shell-production-ready.spec.ts`

**Steps:**

1. Open the shell from a blank session and navigate through the core panel links once.
   - expect: Each route displays its intended panel.
   - expect: Breadcrumbs and headings update to match the selected surface.

2. Collapse the left rail and reload the page on one or more routes.
   - expect: The navigation toggle state remains consistent.
   - expect: The current panel stays visible after reload.

3. Confirm the shell continues to surface documented fallback messaging when the API is unavailable.
   - expect: The overview remains usable in fallback mode.
   - expect: The test does not depend on a skip path to remain green.
