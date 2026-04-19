# Wiley Workspace Browser-Depth Plan

Seed reference: tests/playwright/seed.spec.ts

## Goal

Cover browser-specific states that the current C# suite does not exercise deeply:

- fallback-safe workspace shell status
- customer viewer degraded or empty-state interactions
- QuickBooks unsupported upload and reset behavior

## Scenarios

### 1. Workspace shell fallback-safe status

Route: /wiley-workspace

Why: prove the workspace shell stays usable after startup and current-state resolution, even when the app is working from fallback or browser-restored state.

Assertions:

- the workspace status card is visible
- the workspace load status resolves to a ready state
- startup and current-state labels no longer show pending text
- the overview cards remain visible and actionable

### 2. Customer viewer degraded or empty directory

Route: /wiley-workspace/customers

Why: cover browser interactions around filter reset and refresh when the live utility-customer API is empty or unavailable.

Assertions:

- the directory status resolves away from the loading state
- the search input accepts text
- Clear filters resets the search box
- Refresh completes with either a live-success or degraded-status message
- the degraded error banner renders when the live API load fails

### 3. QuickBooks unsupported upload flow

Route: /wiley-workspace/quickbooks-import

Why: exercise the browser file input, unsupported extension handling, and reset behavior that component tests do not cover end-to-end.

Assertions:

- the idle state disables Analyze and Commit before file selection
- an unsupported file can be injected through the browser upload control
- Analyze reports the unsupported-file state and blocks commit
- Reset returns the panel to the idle ready state
