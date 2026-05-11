import { expect, test } from "@playwright/test";
import {
  gotoWorkspacePanel,
  readCurrencyValueByLabel,
} from "./support/workspace";

/**
 * Council use case (May 2026): model incremental annual dollars for new positions
 * in the Scenario Planner. The app treats each line as an annual cost adder on top
 * of the enterprise's current total costs (see State/RateCalculator.cs).
 *
 * Tonight's workflow (manual):
 * 1. Refresh QuickBooks-derived totals when the import is current (per enterprise FY).
 * 2. For Water, Sewer, and Apartments (each enterprise in the header combobox), repeat:
 *    - Select enterprise and confirm total costs / projected volume match finance.
 *    - Open Scenario Planner and load the New Hire Impact Template.
 * 3. Read "Scenario Break-Even" / "Scenario Delta" for the story; save scenario when API allows.
 *
 * This test proves the quick template path, arithmetic, council packet label, and PDF download.
 */
test.describe("Council staffing scenario (scenario planner)", () => {
  test("new hire impact template updates scenario cost total and council packet export", async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1366, height: 900 });
    await gotoWorkspacePanel(page, "/wiley-workspace/scenario");

    const metrics = page.locator("#scenario-metrics-panel");
    const grid = page.locator("#scenario-grid");
    const initialScenarioBreakEven = await readCurrencyValueByLabel(
      metrics,
      "Scenario Break-Even",
    );

    await page
      .getByRole("button", {
        name: "Load New Hire Impact Template (Clerk + Field Employee)",
      })
      .click();
    await expect(page.locator("#scenario-name-input")).toHaveValue(
      "HIRES-2026-Q2-001 – PT Clerk + FT Field",
    );
    await expect(grid).toContainText("PT City Clerk");
    await expect(grid).toContainText("FT Field Employee");

    await expect
      .poll(() => readCurrencyValueByLabel(metrics, "Scenario Cost Total"))
      .toBe(24_583);

    await expect
      .poll(
        async () =>
          (await readCurrencyValueByLabel(metrics, "Scenario Break-Even")) >
          initialScenarioBreakEven,
      )
      .toBe(true);

    await expect(
      page.getByRole("button", { name: "Export Council Rate Packet" }).first(),
    ).toBeVisible();

    const downloadPromise = page.waitForEvent("download");
    await page
      .getByRole("button", { name: "Export Council Rate Packet" })
      .first()
      .click();
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/-rate-packet\.pdf$/);
  });
});
