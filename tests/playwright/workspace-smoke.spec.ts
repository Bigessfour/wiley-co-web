import { expect, test } from "@playwright/test";
import { expandDocumentCenter, waitForWorkspacePanel } from "./support/workspace";

test("index.html boot shell includes Wiley static headline", async ({
  request,
}) => {
  const response = await request.get("/");
  expect(response.ok()).toBeTruthy();
  const body = await response.text();
  expect(body).toContain('id="wiley-static-boot-headline"');
  expect(body).toContain("Starting Wiley Widget");
  expect(body).toContain('id="app"');
});

test("workspace overview renders", async ({ page }) => {
  await page.goto("/wiley-workspace");

  await waitForWorkspacePanel(page, "#workspace-overview-dashboard");
  await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
});

test("document center exposes archived snapshot exports and live status", async ({
  page,
}) => {
  await page.goto("/wiley-workspace");
  await waitForWorkspacePanel(page, "#workspace-document-center");
  await expandDocumentCenter(page);

  const status = page.locator("#document-export-status");
  await expect(status).toBeVisible();
  await expect(status).toHaveAttribute("aria-live", "polite");

  await expect(page.locator("#export-archived-scenario-excel-button")).toBeVisible();
  await expect(page.locator("#export-archived-scenario-pdf-button")).toBeVisible();
});

test("town hub lists app cards and workspace entry points", async ({ page }) => {
  await page.goto("/");
  await expect(page.locator(".home-hub.town-hub")).toBeVisible({ timeout: 60_000 });
  await expect(page.locator(".home-hub-grid.town-hub-apps")).toBeVisible();

  // Default Town Hub app tiles (config can override URLs; titles stay stable).
  for (const title of ["Wiley Widget", "Apartments", "Rural Water", "Trash Truck", "TIKR"]) {
    await expect(
      page.locator(".home-hub-card strong", { hasText: title }),
      `town hub missing app card ${title}`,
    ).toBeVisible();
  }

  await expect(page.locator('a.home-hub-primary[href="/wiley-workspace"]')).toBeVisible();
  await expect(
    page.locator('a.home-hub-secondary[href="/wiley-workspace/reference-data"]'),
  ).toBeVisible();
  await expect(page.locator(".town-hub-summary")).toBeVisible();
});
