import { expect, test } from "@playwright/test";
import { waitForWorkspacePanel } from "./support/workspace";

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

  const status = page.locator("#document-export-status");
  await expect(status).toBeVisible();
  await expect(status).toHaveAttribute("aria-live", "polite");

  await expect(page.locator("#export-archived-scenario-excel-button")).toBeVisible();
  await expect(page.locator("#export-archived-scenario-pdf-button")).toBeVisible();
});

test("home hub cards match sidebar destinations", async ({ page }) => {
  const sidebarHrefs = [
    "/wiley-workspace",
    "/wiley-workspace/break-even",
    "/wiley-workspace/apartment-config",
    "/wiley-workspace/rates",
    "/wiley-workspace/quickbooks-import",
    "/wiley-workspace/scenario",
    "/wiley-workspace/customers",
    "/wiley-workspace/affordability",
    "/wiley-workspace/debt-coverage",
    "/wiley-workspace/capital-gap",
    "/wiley-workspace/reserve-trajectory",
    "/wiley-workspace/trends",
    "/wiley-workspace/decision-support",
    "/wiley-workspace/data-dashboard",
  ];

  await page.goto("/");
  await expect(page.locator(".home-hub-grid")).toBeVisible({ timeout: 60_000 });

  for (const href of sidebarHrefs) {
    await expect(
      page.locator(`.home-hub-card[href="${href}"]`),
      `home hub missing sidebar href ${href}`,
    ).toBeVisible();
  }
});
