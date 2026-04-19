import { expect, test } from "@playwright/test";
import { waitForWorkspaceShell } from "./support/workspace";

test("workspace overview renders", async ({ page }) => {
  await page.goto("/wiley-workspace");

  await waitForWorkspaceShell(page);
  await expect(page.locator("#workspace-overview-dashboard")).toBeVisible();
});
