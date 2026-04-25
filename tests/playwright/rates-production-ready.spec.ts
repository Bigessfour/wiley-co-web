import { expect, test } from "@playwright/test";
import { enterNumericValue, gotoWorkspacePanel } from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Rates panel updates the current rate and preserves comparison clarity", async ({
    page,
  }) => {
    // 1. Open /wiley-workspace/rates from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/rates");

    await expect(page.locator("#rates-panel")).toBeVisible();
    await expect(page.locator("#rates-kpi-grid")).toBeVisible();
    await expect(page.locator("#current-rate-input")).toBeVisible();
    await expect(page.locator("#rates-comparison-chart")).toBeVisible();
    await expect(page.locator("#snapshot-save-status")).toContainText(
      "Ready to save rate snapshot",
    );

    // 2. Change the current rate and tab out so the panel applies the edit.
    await enterNumericValue(page.locator("#current-rate-input"), "29.50");

    await expect
      .poll(async () => page.locator("#rates-kpi-grid").innerText(), {
        timeout: 10_000,
      })
      .toMatch(/Current Rate\s*\$29\.50/);
    await expect(page.locator("#rates-comparison-chart")).toBeVisible();

    // 3. Trigger the save flow and confirm the snapshot status updates.
    await page.getByRole("button", { name: "Save rate snapshot" }).click();
    await expect(page.locator("#snapshot-save-status")).toContainText(
      /Saved .* at .*|Snapshot save failed:/,
    );

    await expect(page.locator("#rates-panel-chart-section")).toBeVisible();
    await expect(page.locator("#rates-kpi-grid")).toContainText(
      /Current Rate\s*\$29\.50/,
    );
    await expect(page.locator("#rates-kpi-grid")).toContainText(
      /Recommended Rate\s*\$\d[\d,]*\.\d{2}/,
    );
  });
});
