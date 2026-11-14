import { expect, test } from "@playwright/test";
import { waitForWorkspacePanel } from "./support/workspace";

const workspaceSurfaces: Array<[string, string]> = [
  ["/wiley-workspace", "#workspace-overview-dashboard"],
  ["/wiley-workspace/break-even", "#break-even-panel"],
  ["/wiley-workspace/rates", "#rates-panel"],
  ["/wiley-workspace/scenario", "#scenario-panel"],
  ["/wiley-workspace/affordability", "#affordability-dashboard-panel"],
  ["/wiley-workspace/debt-coverage", "#debt-coverage-panel"],
  ["/wiley-workspace/capital-gap", "#capital-gap-panel"],
  ["/wiley-workspace/reserve-trajectory", "#reserve-trajectory-panel"],
  ["/wiley-workspace/trends", "#trends-panel"],
  ["/wiley-workspace/decision-support", "#decision-support-panel"],
  ["/wiley-workspace/data-dashboard", "#data-dashboard-panel"],
  ["/wiley-workspace/apartment-config", "#apartment-config-panel"],
  ["/wiley-workspace/quickbooks-import", "#quickbooks-import-panel"],
  ["/wiley-workspace/customers", "#customer-viewer-panel"],
  ["/wiley-workspace/reference-data", "#shared-contract-reference-data-panel"],
  ["/budget-dashboard", "#workspace-overview-dashboard"],
];

test("operator surfaces render", async ({ page }) => {
  test.setTimeout(12 * 60_000);
  const failures: string[] = [];

  await page.goto("/");
  try {
    await expect(page.locator(".home-hub.town-hub")).toBeVisible({ timeout: 90_000 });
    await expect(
      page.locator('button.home-hub-card[data-external-url="http://mr-storage:8082"]'),
    ).toBeVisible();
  } catch (error) {
    failures.push(`/: ${error instanceof Error ? error.message : String(error)}`);
  }

  await page.goto("/authentication/login");
  try {
    await expect(page.getByRole("heading", { name: "Staff sign-in" })).toBeVisible({
      timeout: 60_000,
    });
  } catch (error) {
    failures.push(
      `/authentication/login: ${error instanceof Error ? error.message : String(error)}`,
    );
  }

  await page.goto("/Error");
  try {
    await expect(page.getByRole("heading", { name: "Error" })).toBeVisible({ timeout: 30_000 });
  } catch (error) {
    failures.push(`/Error: ${error instanceof Error ? error.message : String(error)}`);
  }

  for (const [route, selector] of workspaceSurfaces) {
    await page.goto(route);
    try {
      await waitForWorkspacePanel(page, `${selector} >> nth=0`, 60_000);
      const copies = await page.locator(selector).count();
      if (copies > 1) {
        test.info().annotations.push({
          type: "duplicate-id",
          description: `${route} rendered, and ${copies} elements share ${selector}`,
        });
      }
    } catch (error) {
      failures.push(`${route} ${selector}: ${error instanceof Error ? error.message : String(error)}`);
    }
  }

  expect(failures, failures.join("\n")).toEqual([]);
});
