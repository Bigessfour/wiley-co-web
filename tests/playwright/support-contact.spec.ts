import { expect, test } from "@playwright/test";

test("support page tells a clerk who to call", async ({ page }) => {
  await page.goto("/support");
  const support = page.locator("#staff-support");
  await expect(support).toBeVisible();
  await expect(support).toContainText("Who to call");
  await expect(support).toContainText("(719) 829-4974");
  await expect(support.locator('a[href="mailto:clerk@townofwiley.gov"]')).toBeVisible();
  await expect(support).toContainText("office network");
  await expect(support).toContainText("Coming soon");
  await expect(page.locator('a.app-nav-link[href="/support"]')).toBeVisible();
});
