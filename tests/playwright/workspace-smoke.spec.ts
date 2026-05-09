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
