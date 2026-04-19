import { expect, test } from "@playwright/test";
import { waitForWorkspaceShell } from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Workspace overview opens the data dashboard panel", async ({
    page,
  }) => {
    await page.goto("/wiley-workspace");

    await waitForWorkspaceShell(page);

    await page
      .locator("#overview-data-dashboard")
      .getByRole("button", { name: "Open Data Dashboard" })
      .click();

    await expect(page.locator("#data-dashboard-panel")).toBeVisible();
    await expect(page.locator("#workspace-breadcrumb")).toContainText(
      "Data Dashboard",
    );
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
  });
});
