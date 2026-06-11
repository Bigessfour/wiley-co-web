import { expect, test } from "@playwright/test";
import {
  seedBudgetDashboardLayout,
  waitForWorkspacePanel,
} from "./support/workspace";

test("index.html boot shell includes Wiley static headline", async ({
  request,
}) => {
  const response = await request.get("/");
  expect(response.ok()).toBeTruthy();
  const body = await response.text();
  expect(body).toContain('id="wiley-static-boot-headline"');
  expect(body).toContain("Starting Wiley Widget");
  expect(body).toContain('id="app"');
  expect(body).toContain("js/wiley-wasm-boot.js");
});

test("workspace overview renders", async ({ page }) => {
  await page.goto("/wiley-workspace");

  await waitForWorkspacePanel(page, "#workspace-overview-dashboard");
  await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
});

test("budget dashboard route renders executive anchor and KPI row", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await seedBudgetDashboardLayout(page);
  await page.goto("/budget-dashboard");

  const dashboard = page.locator("#workspace-overview-dashboard");
  await waitForWorkspacePanel(page, "#workspace-overview-dashboard");
  await expect(dashboard).toBeVisible();

  const viewportHeight = 1080;
  const controlIds = [
    "kpi-net-position",
    "kpi-coverage-ratio",
    "kpi-rate-adequacy",
    "kpi-scenario-pressure",
    "budget-variance-chart",
    "scenario-waterfall-chart",
    "rate-trend-chart",
  ];

  for (const id of controlIds) {
    const control = page.locator(`#${id}`);
    await expect(control).toBeVisible();
    const box = await control.boundingBox();
    expect(box, `${id} should have a layout box`).not.toBeNull();
    expect(
      box!.y,
      `${id} should not be above the viewport`,
    ).toBeGreaterThanOrEqual(0);
    expect(
      box!.y + box!.height,
      `${id} should fit within a 1080p viewport without scrolling`,
    ).toBeLessThanOrEqual(viewportHeight);
  }
});
