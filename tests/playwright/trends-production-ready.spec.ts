import { expect, test } from "@playwright/test";
import { gotoWorkspacePanel } from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Trends panel renders and reflows cleanly", async ({ page }) => {
    // 1. Open /wiley-workspace/trends from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/trends");

    await expect(page.locator("#trends-panel")).toBeVisible();
    await expect(page.locator("#trends-chart-region")).toBeVisible();
    await expect(page.locator("#trends-projection-chart")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText("Trends");

    // 2. Resize the viewport once and re-check the chart region.
    await page.setViewportSize({ width: 1280, height: 900 });
    await expect(page.locator("#trends-panel")).toBeVisible();
    await expect(page.locator("#trends-chart-region")).toBeVisible();
    await expect(page.locator("#trends-projection-chart")).toBeVisible();

    // 3. Re-check the layout at desktop width so the proof stays visual without a brittle baseline image.
    await page.setViewportSize({ width: 1440, height: 900 });
    await expect(page.locator("#trends-panel")).toBeVisible();
    await expect(page.locator("#trends-chart-region")).toBeVisible();
    await expect(page.locator("#trends-projection-chart")).toBeVisible();
  });
});
