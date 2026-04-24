import { expect, test } from "@playwright/test";
import { enterNumericValue, gotoWorkspacePanel } from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Break-even panel renders four quadrants and apartment roll-up cards", async ({
    page,
  }) => {
    // 1. Open /wiley-workspace/break-even from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/break-even");

    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#break-even-quadrant-panel")).toBeVisible();
    await expect(page.locator("#break-even-quadrant-grid")).toBeVisible();
    await expect(
      page.locator("#break-even-quadrant-grid > section"),
    ).toHaveCount(4);
    await expect(page.locator("#break-even-input-row")).toBeVisible();
    await expect(page.locator("#apartment-config-panel")).toBeVisible();
    await expect(page.locator("#break-even-panel")).not.toContainText(
      /pending/i,
    );

    await expect(page.locator("#break-even-chart-water-utility")).toBeVisible();
    await expect(
      page.locator("#break-even-chart-wiley-sanitation-district"),
    ).toBeVisible();
    await expect(page.locator("#break-even-chart-trash")).toBeVisible();
    await expect(page.locator("#break-even-chart-apartments")).toBeVisible();
    await expect(page.locator("#apartment-config-panel")).toContainText(
      "2 Bedroom",
    );
    await expect(page.locator("#apartment-config-panel")).toContainText(
      "3 Bedroom",
    );
    await expect(page.locator("#apartment-config-panel")).toContainText(
      /Total Units\s*16/,
    );
    await expect(page.locator("#apartment-config-panel")).toContainText(
      /Monthly Revenue\s*\$8,000/,
    );
    await expect(page.locator("#apartment-config-panel")).toContainText(
      /Effective \$\/Customer\s*\$200\.00/,
    );

    // 2. Change total costs and projected volume through the numeric editors.
    const breakEvenInputs = page.locator("#break-even-input-row input");
    await enterNumericValue(breakEvenInputs.nth(0), "24000");
    await enterNumericValue(breakEvenInputs.nth(1), "400");

    await expect(page.locator("#break-even-quadrant-grid")).toContainText(
      /Break-even\s*\$60\.00/,
    );
    await expect(page.locator("#break-even-quadrant-grid")).toContainText(
      "Water Utility",
    );
    await expect(page.locator("#break-even-quadrant-grid")).toContainText(
      "Apartments",
    );

    // 3. Re-check the layout at desktop width so the proof stays visual without a brittle baseline image.
    await page.setViewportSize({ width: 1440, height: 900 });
    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#break-even-quadrant-grid")).toBeVisible();
    await expect(page.locator("#apartment-config-panel")).toBeVisible();
  });
});
