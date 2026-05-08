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
    await gotoWorkspacePanel(page, "/wiley-workspace/break-even");

    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#break-even-kpi-grid")).toBeVisible();
    await expect(page.locator("#break-even-input-row")).toBeVisible();
    await expect(page.locator("#break-even-chart-card")).toBeVisible();
    await expect(page.locator("#break-even-panel")).not.toContainText(
      /pending/i,
    );

    const breakEvenSpinners = breakEvenPanelSpinbuttons(page);
    await setNumericInputValue(breakEvenSpinners.nth(0), "24000");
    await setNumericInputValue(breakEvenSpinners.nth(1), "400");

    await expect
      .poll(
        async () => await page.locator("#break-even-kpi-grid").textContent(),
        { timeout: 30000 },
      )
      .toMatch(/Total Costs[^\$]*\$24,000/);
    await expect
      .poll(
        async () => await page.locator("#break-even-kpi-grid").textContent(),
        { timeout: 30000 },
      )
      .toMatch(/Projected Volume\s*400/);
    await expect
      .poll(
        async () => await page.locator("#break-even-kpi-grid").textContent(),
        { timeout: 30000 },
      )
      .toMatch(/Recommended Rate\s*\$60\.00/);

    await page.setViewportSize({ width: 1440, height: 900 });
    await expect(page.locator("#break-even-panel")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.locator("#break-even-kpi-grid")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.locator("#break-even-chart-card")).toBeVisible({
      timeout: 10000,
    });
  });
});
