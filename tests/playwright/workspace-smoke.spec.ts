import { expect, test } from "@playwright/test";

test("workspace overview renders", async ({ page }) => {
  await page.goto("/wiley-workspace");

  await expect(page.locator("#workspace-load-status")).toContainText(
    "Workspace ready.",
  );
  await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
});
