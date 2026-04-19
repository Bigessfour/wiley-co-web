import { expect, test } from "@playwright/test";

test.describe("Wiley workspace seed", () => {
  test("overview and agent target panels render", async ({ page }) => {
    await page.goto("/wiley-workspace");

    await expect(page.locator("#workspace-load-status")).toContainText(
      "Workspace ready.",
    );
    await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();

    await page.goto("/wiley-workspace/customers");
    await expect(page.locator("#customer-viewer-panel")).toBeVisible();
    await expect(page.locator("#customer-directory-status")).toBeVisible();

    await page.goto("/wiley-workspace/quickbooks-import");
    await expect(page.locator("#quickbooks-import-status-headline")).toHaveText(
      "Ready",
    );
    await expect(
      page.locator("#quickbooks-import-status-message"),
    ).toContainText("Choose a QuickBooks export to begin.");
  });
});
