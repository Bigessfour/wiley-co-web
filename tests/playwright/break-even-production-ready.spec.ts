import { expect, test } from "@playwright/test";
import {
  breakEvenPanelSpinbuttons,
  expandBreakEvenApartmentAccordion,
  gotoWorkspacePanel,
  setNumericInputValue,
} from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Break-even panel renders four quadrants and apartment roll-up cards", async ({
    page,
  }) => {
    await gotoWorkspacePanel(page, "/wiley-workspace/break-even");

    await expect(page.locator("#break-even-panel")).toBeVisible();
    await expect(page.locator("#break-even-quadrant-panel")).toBeVisible();
    await expect(page.locator("#break-even-quadrant-grid")).toBeVisible();
    await expect(
      page.locator("#break-even-quadrant-grid > section"),
    ).toHaveCount(4);
    await expect(page.locator("#break-even-input-row")).toBeVisible();
    await expandBreakEvenApartmentAccordion(page);
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

    const breakEvenSpinners = breakEvenPanelSpinbuttons(page);
    await setNumericInputValue(breakEvenSpinners.nth(0), "24000");
    await setNumericInputValue(breakEvenSpinners.nth(1), "400");

    await expect
      .poll(
        async () => await page.locator("#break-even-kpi-grid").textContent(),
        { timeout: 30000 },
      )
      .toMatch(/Total Costs[^\$]*\$24,000/);
    await expect
      .poll(
        async () => await page.locator("#break-even-kpi-grid").textContent(),
        { timeout: 30000 },
      )
      .toMatch(/Projected Volume\s*400/);
    await expect
      .poll(
        async () => await page.locator("#break-even-kpi-grid").textContent(),
        { timeout: 30000 },
      )
      .toMatch(/Recommended Rate\s*\$60\.00/);

    await expect(page.locator("#break-even-quadrant-grid")).toContainText(
      /Break-Even\s*\$60\.00/i,
    );
    await expect(page.locator("#break-even-quadrant-grid")).toContainText(
      "Water Utility",
    );
    await expect(page.locator("#break-even-quadrant-grid")).toContainText(
      "Apartments",
    );

    await page.setViewportSize({ width: 1440, height: 900 });
    await expect(page.locator("#break-even-panel")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.locator("#break-even-kpi-grid")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.locator("#break-even-chart-card")).toBeVisible({
      timeout: 10000,
    });
    await expect(page.locator("#break-even-quadrant-grid")).toBeVisible();
    await expandBreakEvenApartmentAccordion(page);
    await expect(page.locator("#apartment-config-panel")).toBeVisible();
  });
});
