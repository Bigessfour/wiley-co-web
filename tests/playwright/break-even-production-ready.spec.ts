import { expect, test } from "@playwright/test";
import {
  breakEvenPanelSpinbuttons,
  gotoWorkspacePanel,
  setNumericInputValue,
} from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Break-even panel recalculates correctly and keeps a clean layout", async ({
    page,
  }) => {
    // 1. Open /wiley-workspace/break-even from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/break-even");

    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#break-even-kpi-grid")).toBeVisible();
    await expect(page.locator("#break-even-input-row")).toBeVisible();
    await expect(page.locator("#break-even-chart-card")).toBeVisible();
    await expect(page.locator("#break-even-panel")).not.toContainText(
      /pending/i,
    );

    // 2. Change total costs and projected volume through the numeric editors.
    const breakEvenSpinners = breakEvenPanelSpinbuttons(page);
    await setNumericInputValue(breakEvenSpinners.nth(0), "24000");
    await setNumericInputValue(breakEvenSpinners.nth(1), "400");

    await expect(page.locator("#break-even-kpi-grid")).toContainText(
      /Total Costs\s*\$24,000/,
    );
    await expect(page.locator("#break-even-kpi-grid")).toContainText(
      /Projected Volume\s*400/,
    );
    await expect(page.locator("#break-even-kpi-grid")).toContainText(
      /Recommended Rate\s*\$60\.00/,
    );

    // 3. Re-check the layout at desktop width so the proof stays visual without a brittle baseline image.
    await page.setViewportSize({ width: 1440, height: 900 });
    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#break-even-kpi-grid")).toBeVisible();
    await expect(page.locator("#break-even-chart-card")).toBeVisible();
  });
});
