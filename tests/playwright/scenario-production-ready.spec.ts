import { expect, test } from "@playwright/test";
import {
  enterNumericValue,
  gotoWorkspacePanel,
  prepareForVisualSnapshot,
  readCurrencyValueByLabel,
} from "./support/workspace";

test.describe("Core Panel Proof", () => {
  test("Scenario planner adds rows and keeps the derived totals correct", async ({
    page,
  }) => {
    // 1. Open /wiley-workspace/scenario from a blank session.
    await gotoWorkspacePanel(page, "/wiley-workspace/scenario");

    const panel = page.locator("#scenario-panel");
    const grid = page.locator("#scenario-grid");
    const editStatus = page.locator("#scenario-edit-status");

    await expect(panel).toBeVisible();
    await expect(grid).toBeVisible();
    await expect(editStatus).toBeVisible();

    const initialScenarioCostTotal = await readCurrencyValueByLabel(
      panel,
      "Scenario Cost Total",
    );
    const initialScenarioBreakEven = await readCurrencyValueByLabel(
      panel,
      "Scenario Break-Even",
    );
    const scenarioItemName = `Live test ${Date.now()}`;

    // 2. Add a new scenario row with a distinct name and numeric cost.
    await page.getByRole("button", { name: "Add" }).click();
    const dialog = page
      .getByRole("dialog")
      .filter({ has: page.locator('input[name="Name"]') })
      .filter({ has: page.locator('input[name="Cost"]') });

    await expect(dialog).toBeVisible();
    await dialog.locator('input[name="Name"]').fill(scenarioItemName);
    await enterNumericValue(dialog.locator('input[name="Cost"]'), "1234");
    await dialog.getByRole("button", { name: "Save" }).click();

    await expect(grid).toContainText(scenarioItemName);
    await expect
      .poll(() => readCurrencyValueByLabel(panel, "Scenario Cost Total"))
      .toBe(initialScenarioCostTotal + 1234);
    await expect
      .poll(
        async () =>
          (await readCurrencyValueByLabel(panel, "Scenario Break-Even")) >
          initialScenarioBreakEven,
      )
      .toBe(true);

    // 3. Capture a screenshot of the updated scenario panel.
    await page.setViewportSize({ width: 1440, height: 900 });
    await prepareForVisualSnapshot(page);

    await expect(panel).toHaveScreenshot("scenario-panel.png", {
      animations: "disabled",
      caret: "hide",
      scale: "css",
      maxDiffPixelRatio: 0.02,
    });
  });
});
