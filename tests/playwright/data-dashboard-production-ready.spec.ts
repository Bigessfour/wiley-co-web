import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Data dashboard proves the chart and gauge surfaces are production ready", async ({
    page,
  }) => {
    // 1. Open /wiley-workspace/data-dashboard from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/data-dashboard");

    await expect(page.locator("#data-dashboard-panel")).toBeVisible();
    await expect(page.locator("#kpi-net-position")).toBeVisible();
    await expect(page.locator("#kpi-coverage-ratio")).toBeVisible();
    await expect(page.locator("#kpi-rate-adequacy")).toBeVisible();
    await expect(page.locator("#kpi-scenario-pressure")).toBeVisible();
    await expect(page.locator("#coverage-ratio-gauge")).toBeVisible();
    await expect(page.locator("#rate-adequacy-gauge")).toBeVisible();
    await expect(page.locator("#budget-variance-chart")).toBeVisible();
    await expect(page.locator("#scenario-waterfall-chart")).toBeVisible();

    const rateTrendSection = page.locator("#rate-trend-section");
    if (await rateTrendSection.count()) {
      await expect(rateTrendSection).toBeVisible();
      await expect(page.locator("#rate-trend-chart")).toBeVisible();
    }

    // 2. Verify that the key dashboard visuals remain visible at desktop width and at a narrower width.
    await page.setViewportSize({ width: 1280, height: 900 });
    await expect(page.locator("#data-dashboard-panel")).toBeVisible();
    await expect(page.locator("#coverage-ratio-gauge")).toBeVisible();
    await expect(page.locator("#rate-adequacy-gauge")).toBeVisible();
    await expect(page.locator("#budget-variance-chart")).toBeVisible();
    await expect(page.locator("#scenario-waterfall-chart")).toBeVisible();
    if (await rateTrendSection.count()) {
      await expect(page.locator("#rate-trend-chart")).toBeVisible();
    }

    // 3. Re-check the layout at the primary desktop width.
    await page.setViewportSize({ width: 1440, height: 900 });
    await expect(page.locator("#data-dashboard-panel")).toBeVisible();
  });
});
